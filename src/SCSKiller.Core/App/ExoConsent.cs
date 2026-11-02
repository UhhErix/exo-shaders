// Exo Shaders - unofficial fork of SCSKiller, based on SCSKiller (https://github.com/BlueHeisenberg/SCSKiller).
// Copyright (C) 2026 Exo Shaders contributors. GPL-3.0-or-later (see LICENSE, LICENSE-EXCEPTION.txt).

namespace SCSKiller.Core.App;

/// <summary>What the user agreed to in Exo, in exo-consent.json in the data folder. Apart from settings.json, whose
/// RecordAllGames an earlier Exo Shaders always wrote true without asking, and whose values a copy of the official app on the
/// same folder can write. A file that can't be read grants nothing. <see cref="OverridesCleared"/>: the recorder switches
/// from before there was any consent have been cleared, once.</summary>
public sealed record ExoConsent(bool RecorderAuto = false, bool ShareUpload = false, bool OverridesCleared = false)
{
    public const string File = "exo-consent.json";

    public static ExoConsent Load(string dataDir) => Community.Read<ExoConsent>(Path.Combine(dataDir, File)) ?? new();

    public void Save(string dataDir) => Community.Write(Path.Combine(dataDir, File), this);
}
