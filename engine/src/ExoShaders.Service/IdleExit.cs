// Exo Shaders - unofficial fork of SCSKiller, based on SCSKiller (https://github.com/BlueHeisenberg/SCSKiller).
// Copyright (C) 2026 Exo Shaders contributors
//
// This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public
// License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later
// version, with the additional permission in LICENSE-EXCEPTION.txt. It is distributed WITHOUT ANY WARRANTY; see LICENSE.
//
// The service runs only while something it was asked to do goes on. Exo starts it only when you press Compile or Refresh
// on its page (never for the page opening, a driver or game update, a game exiting or the PC idling); after exitWhenIdle
// it exits by itself once its last scan, compile and the passes after them have ended, and leaves its last state
// (exo-snapshot.json) for the page to show until it runs again.

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
            _log.Report($"[exo] nothing to do for {after / 1000} s: exiting until Compile or Refresh is pressed");
            await QuitAsync("idle", null);
            return;
        }
    }

    /// <summary>
    /// Something goes on: a request is answered, a scan, the started queue or a compile of it, the settings policy or the
    /// new-shaders check, an elevated driver change, or the engine's own passes after a scan (settings refresh, server
    /// lists, community downloads, recorder indexes, recording migration). The page being shown is not: it reads the
    /// state the service leaves. "When idle" work (a plan check, a precompiled-database rebuild) keeps it only while it
    /// runs, which is only while the PC is really idle (PcIdle); waiting or paused for that, it doesn't, and goes with it.
    /// </summary>
    private bool Busy()
    {
        ScsKiller k = _k;
        if (Volatile.Read(ref _inflight) > 0 || Scanning || k.QueueRunning || k.LocalAsdBuilding) return true;
        if (k.Compiling && (!k.RunningWhenIdle || k.IdleTime() >= k.IdleAfter)) return true;
        if (Volatile.Read(ref _policyPending) == 1 || _policyGate.CurrentCount == 0 || Volatile.Read(ref _newShadersPending) == 1
            || _newShadersGate.CurrentCount == 0 || Volatile.Read(ref _cacheReading) == 1 || Volatile.Read(ref _elevated) > 0) return true;
        return !k.SettingsRefresh.IsCompleted || !k.StutterUpdate.IsCompleted || !k.CommunitySync.IsCompleted
            || !k.RecorderIndexing.IsCompleted || !k.RecordingMigration.IsCompleted;
    }

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
