// Exo Shaders - unofficial fork of SCSKiller, based on SCSKiller (https://github.com/BlueHeisenberg/SCSKiller).
// Copyright (C) 2026 Exo Shaders contributors. GPL-3.0-or-later (see LICENSE, LICENSE-EXCEPTION.txt).
//
// The Patreon sign-in and the community database, as the official app wires them (App.Launch): downloads follow the sign-in,
// uploads are their own switch. Tokens stay inside Account and Sharing: nothing here reads one.

using SCSKiller.Core;
using SCSKiller.Core.App;

namespace ExoShaders.Service;

internal sealed partial class Host
{
    private Account? _account;

    private void WireSharing(ScsKiller k)
    {
        var account = _account = new Account(AppStore.DefaultDir);
        k.Community = new Community(AppStore.DefaultDir, account.GetDbTokenAsync) { TokenWith = account.GetTokenAsync };
        k.DbEntitled = () => account.SignedIn && (account.Status == null || account.HasDb);
        // read at each call, from Exo's consent: settings.json's ShareRecordings alone never sends anything
        k.Sharing = new Sharing(AppStore.DefaultDir, () => Consent.ShareUpload);
        bool hadDb = false;
        account.Changed += () =>
        {
            bool db = account.HasDb;
            if (db && !hadDb) k.StartCommunitySync();   // signed in or the membership turned active: at once, not at the next scan
            hadDb = db;
            MarkState();
        };
    }

    public object SharingDto()
    {
        SharingStatus s = ExoSharing.Status(_account, _k.Settings.UseCommunityDb, Consent.ShareUpload, _k.Community, _k.Sharing);
        return new
        {
            signedIn = s.SignedIn,
            signingIn = s.SigningIn,
            busy = s.Busy,
            supporter = s.Supporter,
            benefits = s.Benefits,
            until = s.Until is { } until ? Fmt.Iso(until) : null,
            problem = s.Problem,
            downloadsOn = s.DownloadsOn,
            uploadOn = s.UploadOn,
            uploadProblem = s.UploadProblem,
            uploadSends = ExoSharing.UploadSends,
        };
    }

    private Account SignInAccount => _account ?? throw new ServiceError("not available in safe mode");

    private async Task<object?> SharingMethod(string method, Params p)
    {
        switch (method)
        {
            case "sharing.status":
                return SharingDto();
            case "sharing.signIn":
                // opens the default browser and waits up to 5 minutes: Busy keeps the service until it ends; the state says how it went
                _ = SignInAccount.SignInAsync(_stopping.Token);
                return new { ok = true, started = true };
            case "sharing.cancelSignIn":
                SignInAccount.CancelSignIn();
                return Ok;
            case "sharing.refresh":
                await SignInAccount.RefreshAsync(_stopping.Token);
                return SharingDto();
            case "sharing.signOut":
                await SignInAccount.SignOutAsync(_stopping.Token);
                _k.DismissCommunity();
                return SharingDto();
            case "sharing.setUpload":
            {
                bool on = p.ReqBool("on");
                if (Consent.ShareUpload != on) Consent = Consent with { ShareUpload = on };
                _log.Report($"[exo] sharing recordings: {(on ? "on" : "off")}");
                return SharingDto();
            }
            case "sharing.preview":
            {
                string id = p.ReqStr("id");
                Game(id);
                SharePreviewReport? r = await Task.Run(() => _k.SharePreview(id));
                return new
                {
                    records = r?.Records ?? 0, pipelines = r?.Pipelines ?? 0, stateObjects = r?.StateObjects ?? 0, bytes = r?.Bytes ?? 0, meta = r?.Meta,
                    sends = ExoSharing.UploadSends,
                };
            }
        }
        throw new ServiceError($"unknown method '{method}'");
    }
}
