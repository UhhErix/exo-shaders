// Exo Shaders - unofficial fork of SCSKiller, based on SCSKiller (https://github.com/BlueHeisenberg/SCSKiller).
// Copyright (C) 2026 Exo Shaders contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public
// License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later
// version, with the additional permission in LICENSE-EXCEPTION.txt. It is distributed WITHOUT ANY WARRANTY; see LICENSE.
//
// The service runs only while something needs it. Exo starts it for its page, a compile, a check or work that waits for
// the PC to be idle; after exitWhenIdle it exits by itself once nothing has needed it for that long, and leaves its last
// state (exo-snapshot.json) for the page to show until it runs again.

using System.Text.Json;
using SCSKiller.Core;
using SCSKiller.Core.App;

namespace ExoShaders.Service;

internal sealed partial class Host
{
    /// <summary>The page's state while the service isn't running, in the data folder.</summary>
    public const string AtRestFile = "exo-snapshot.json";

    private static readonly TimeSpan IdleCheck = TimeSpan.FromSeconds(5);

    private long _exitAfterMs;   // 0: never
    private long _activeAt = Environment.TickCount64;
    private int _inflight, _idleWatching;
    private volatile bool _idleWorkAtExit;

    /// <summary>exitWhenIdle: exit (reason <c>idle</c>) once nothing has needed the service for <paramref name="seconds"/>; 0: never.</summary>
    public void ExitWhenIdle(int seconds)
    {
        Interlocked.Exchange(ref _exitAfterMs, seconds <= 0 ? 0 : Math.Clamp(seconds, 5, 3600) * 1000L);
        Touch();
        if (seconds > 0 && Interlocked.Exchange(ref _idleWatching, 1) == 0) _ = Task.Run(IdleWatch);
    }

    /// <summary>A request, or the page shown or hidden: the wait starts again.</summary>
    private void Touch() => Volatile.Write(ref _activeAt, Environment.TickCount64);

    private async Task IdleWatch()
    {
        CancellationToken ct = _stopping.Token;
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(IdleCheck, ct); }
            catch (OperationCanceledException) { return; }
            long after = Interlocked.Read(ref _exitAfterMs);
            if (after == 0 || Quitting) continue;
            if (Busy()) { Touch(); continue; }
            if (Environment.TickCount64 - Volatile.Read(ref _activeAt) < after) continue;
            _log.Report($"[exo] nothing to do for {after / 1000} s: exiting until Exo needs the engine" + (IdleWork() ? " (work waits for the PC to be idle)" : ""));
            await QuitAsync("idle", null);
            return;
        }
    }

    /// <summary>
    /// Something runs or is about to: the page is shown, a request is answered, a scan, a compile or a plan check runs or
    /// the started queue has more, the settings policy or the new-shaders check, an elevated driver change, the engine's own
    /// passes after a scan (settings refresh, server lists, community downloads, recorder indexes, recording migration),
    /// or work waits for the PC to be idle while the user is away and no game runs (it starts now).
    /// </summary>
    private bool Busy()
    {
        ScsKiller k = _k;
        if (!_uiHidden || Volatile.Read(ref _inflight) > 0 || Scanning || k.Compiling || k.QueueRunning) return true;
        if (Volatile.Read(ref _policyPending) == 1 || _policyGate.CurrentCount == 0 || Volatile.Read(ref _newShadersPending) == 1
            || _newShadersGate.CurrentCount == 0 || Volatile.Read(ref _cacheReading) == 1 || Volatile.Read(ref _elevated) > 0) return true;
        if (!k.SettingsRefresh.IsCompleted || !k.StutterUpdate.IsCompleted || !k.CommunitySync.IsCompleted
            || !k.RecorderIndexing.IsCompleted || !k.RecordingMigration.IsCompleted) return true;
        return IdleWork() && k.IdleTime() >= k.IdleAfter && !k.Games.Any(g => g.Playing);
    }

    /// <summary>Work that waits for the PC to be idle: queued "when idle" items (new games, plan checks) and precompiled
    /// shader databases to rebuild. Exo starts the service again for it once the PC is idle (<c>exiting.idleWork</c>).</summary>
    private bool IdleWork() => _k.Queue.Any(q => q.Stage == QueueStage.Waiting) || _k.LocalAsdPending;

    /// <summary>The page's state at rest (no queue, no scan) for <see cref="AtRestFile"/>. Never throws.</summary>
    private void SaveAtRest()
    {
        string path = Path.Combine(_k.Store.DataDir, AtRestFile), tmp = path + "." + Environment.ProcessId + ".tmp";
        try
        {
            File.WriteAllBytes(tmp, Protocol.Brand(JsonSerializer.SerializeToUtf8Bytes(Snapshot(fixedRev: 0, atRest: true), Protocol.Json)));
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception e)
        {
            _log.Report("[exo] the last state wasn't saved: " + e.Message);
            try { File.Delete(tmp); } catch { }
        }
    }
}
