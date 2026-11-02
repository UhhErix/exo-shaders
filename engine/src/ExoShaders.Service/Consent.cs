// Exo Shaders - unofficial fork of SCSKiller, based on SCSKiller (https://github.com/BlueHeisenberg/SCSKiller).
// Copyright (C) 2026 Exo Shaders contributors. GPL-3.0-or-later (see LICENSE, LICENSE-EXCEPTION.txt).
//
// What Exo asks the user once (ExoConsent), the per-game recorder switch, game.diagnostics and game.verify.

using SCSKiller.Core;
using SCSKiller.Core.App;

namespace ExoShaders.Service;

internal sealed partial class Host
{
    private ExoConsent? _consent;

    private ExoConsent Consent
    {
        get => _consent ??= ExoConsent.Load(_k.Store.DataDir);
        set
        {
            _consent = value;
            value.Save(_k.Store.DataDir);
            MarkState();
            RequestPolicy();   // through the engine's own setter: the recorders reconcile, sharing starts
        }
    }

    /// <summary>recorder.auto: every eligible game records from now on; off, recorders this engine placed leave.</summary>
    private void SetRecorderAuto(bool on)
    {
        if (Consent.RecorderAuto != on) Consent = Consent with { RecorderAuto = on };
        _log.Report($"[exo] recording every eligible game: {(on ? "on" : "off")}");
    }

    /// <summary>An earlier Exo Shaders recorded with no consent, and the engine made a recorder it found "on" for a game: those
    /// switches go back to the default once, before the policy turns RecordAllGames off, so what wasn't asked leaves.</summary>
    private void ClearOldRecorderSwitches()
    {
        if (Consent.OverridesCleared) return;
        foreach (GameState g in _k.Games)
            if (!g.Playing && _k.Store.LoadGame(g.Game.Id).Recorder == RecorderOverride.On) _k.SetRecorderOverride(g.Game.Id, RecorderOverride.Default);
        Consent = Consent with { OverridesCleared = true };
    }

    /// <summary>recorder.set: on or off for one game. Another copy's recorder stays that copy's unless takeOver says otherwise.</summary>
    private async Task SetGameRecorder(string id, bool on, bool takeOver)
    {
        GameState g = Game(id);
        if (on && (g.AnyAntiCheat || g.RecorderSkip is not null)) throw new ServiceError(new GameView(g, Context()).RefusalLine);
        if (on && g.RecorderForeign && !takeOver) throw new ServiceError("Another SCSKiller copy records this game. Turning it on here takes that over until that copy takes it back (takeOver: true).");
        await Act(id, () =>
        {
            if (on && g.RecorderForeign) _k.TakeOverRecorder(id);
            else _k.SetRecorderOverride(id, on ? RecorderOverride.On : RecorderOverride.Off);
        });
    }

    private static string VerdictKey(WarmVerdict v) => v switch { WarmVerdict.Helped => "helped", WarmVerdict.Partly => "partly", WarmVerdict.Missed => "missed", _ => "noData" };

    private static object DiagnosticsDto(GameDiagnosticsReport d) => new
    {
        creates = d.Last?.Creates ?? 0,
        known = d.Last?.Known ?? 0,
        tupleKnown = d.Last?.TupleKnown ?? 0,
        unknown = d.Last?.Unknown ?? 0,
        stateObjects = d.Last?.StateObjects ?? 0,
        stateObjectsWarmed = d.Last?.StateObjectsWarmed ?? 0,
        knownMedianMs = d.Last?.KnownMedianMs,
        unknownMedianMs = d.Last?.UnknownMedianMs,
        over20Share = d.Last?.Over20Share ?? 0,
        running = d.Last?.Running ?? false,
        firstLaunchKnownMedianMs = d.FirstLaunchKnownMedianMs,
        firstLaunchKnown = d.FirstLaunchKnown,
        lastPlayed = d.LastPlayed is { } at ? Fmt.Iso(at) : null,
        verdict = VerdictKey(d.Verdict),
        needsFirstLaunch = d.NeedsFirstLaunch,
        recorderForeign = d.RecorderForeign,
        verify = d.Verify is { } v ? new { at = Fmt.Iso(v.At), sampled = v.Sampled, hitRate = v.HitRate, playedHitRate = v.PlayedHitRate, unplayedHitRate = v.UnplayedHitRate, reaches = v.Reaches, cause = v.Cause } : null,
    };

    // =================================================================== the check (ScsKiller.VerifyAsync)

    private Task? _verify;
    private string? _verifyingId;
    private CancellationTokenSource? _verifyStop;

    private void StartVerify(string id)
    {
        Game(id);
        if (_k.VerifyRefusal(id) is { } why) throw new ServiceError(why);
        var stop = new CancellationTokenSource();
        (_verifyStop, _verifyingId) = (stop, id);
        MarkState();
        _verify = Task.Run(async () =>
        {
            try
            {
                VerifyRecord? found = await _k.VerifyAsync(id, stop.Token);
                GameDiagnosticsReport d = _k.Diagnose(id);
                _io.Event("notice", new { kind = "verifyDone", id, stopped = found is null, verdict = VerdictKey(d.Verdict), hitRate = found?.HitRate, reaches = found?.Reaches, cause = found?.Cause, needsFirstLaunch = d.NeedsFirstLaunch });
            }
            catch (Exception e)
            {
                _log.Report($"[exo] the check of {id} failed: {e.Message}");
                _io.Event("notice", new { kind = "verifyFailed", id, text = Fmt.Describe(e) });
            }
            finally
            {
                _verifyingId = null;
                stop.Dispose();
                MarkState();
            }
        });
    }

    /// <summary>Stops a running check gracefully, so the driver writes what it compiled, and waits for it.</summary>
    private async Task StopVerify()
    {
        if (_verify is not { IsCompleted: false } running) return;
        try { _verifyStop?.Cancel(); } catch (ObjectDisposedException) { }
        await Task.WhenAny(running, Task.Delay(TimeSpan.FromSeconds(45)));
    }
}
