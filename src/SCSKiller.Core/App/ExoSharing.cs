// Exo Shaders - unofficial fork of SCSKiller, based on SCSKiller (https://github.com/BlueHeisenberg/SCSKiller).
// Copyright (C) 2026 Exo Shaders contributors. GPL-3.0-or-later (see LICENSE, LICENSE-EXCEPTION.txt).

using SCSKiller.Core.Games;

namespace SCSKiller.Core.App;

/// <summary>What Exo's page may know of the Patreon sign-in and of sharing. Never a token, the device id or the data folder's
/// files: <see cref="Account"/> and <see cref="Sharing"/> keep those to themselves, and their tokens go only in the
/// Authorization header of requests to the API's routes.</summary>
public sealed record SharingStatus(bool SignedIn, bool SigningIn, bool Busy, bool Supporter, string? Benefits, DateTimeOffset? Until, string? Problem,
    bool DownloadsOn, bool UploadOn, string? UploadProblem);

/// <summary>What sharing a game's recording would send now (<see cref="ScsKiller.SharePreview"/>), for the page to show before the
/// switch goes on. <see cref="Meta"/>: the upload's header in full; null while its build isn't indexed, when nothing goes.</summary>
public sealed record SharePreviewReport(long Records, long Pipelines, long StateObjects, long Bytes, UploadMeta? Meta);

public static class ExoSharing
{
    /// <summary>The upload switch's disclosure, as the code sends it (<see cref="Sharing"/>, <see cref="UploadMeta"/>, <see cref="OutcomeReport"/>).</summary>
    public static readonly string[] UploadSends =
    [
        "A random upload device token, registered once. It isn't your Patreon sign-in, which is never sent with an upload.",
        "Your recordings in hash-only form: pipeline state, shader and library hashes, root signatures and ray tracing state objects. No shader code.",
        "For each: the store's game id and build, the build's content hash, the engine, GPU vendor, GPU model and driver version, the app's version, and the name and hash of its upscaler DLLs.",
        "Upscaler pipeline packs this PC filled from its recordings, for the GPU vendor.",
        "After a compile, one report of its first launch, with no token or device: how many pipelines still compiled, their keys by how long they took, and the driver's cache hit check.",
    ];

    public static SharingStatus Status(Account? account, bool downloadsOn, bool uploadOn, Community? community, Sharing? sharing) => new(
        account?.SignedIn == true, account?.SigningIn == true, account?.Busy == true, account?.HasDb == true, account?.Status?.Benefits, account?.Status?.Until,
        // the server is SCSKiller's: the brand pass in the service would call it this program's
        Plain(account?.Problem ?? community?.Problem), downloadsOn, uploadOn, Plain(sharing?.Problem));

    static string? Plain(string? text) => text?.Replace("SCSKiller server", "community server").Replace("SCSKiller", "the community server's app");
}

public sealed partial class ScsKiller
{
    /// <summary>What <see cref="Sharing.ShareAsync"/> would send of this game's own recording: only its recording.db, never a
    /// download. Reads no shader. Null for a Riot game.</summary>
    public SharePreviewReport? SharePreview(string gameId)
    {
        var g = Find(gameId).Game;
        if (GameFiles.IsRiot(g)) return null;
        var rec = Store.LoadGame(gameId);
        var all = File.Exists(RecordingPath(gameId)) ? Planning.PsoDb.Read(RecordingPath(gameId)).Where(r => r.Tag != 'D').ToList() : [];
        List<Planning.PsoDb.Rec> kept;
        try { kept = Planning.HashOnly.Canonical(all, local: true, out _, Planning.HashOnly.MaxEntryStateObjectRefs); }
        catch (InvalidDataException) { kept = []; }
        var version = g.Store == Core.Store.Manual ? null : g.Version;
        var meta = rec.IndexContentHash is { Length: 40 } hash && !ShaderIndex.IsEmpty(hash) && kept.Count > 0 ? UploadMetaOf(g, rec, version, hash) : null;
        return new(kept.Count, kept.Count(r => r.Tag is 'G' or 'C' or 'S'), kept.Count(r => Planning.PsoDb.IsStateObject(r.Tag)), kept.Sum(r => (long)r.Payload.Length), meta);
    }
}
