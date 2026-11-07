# Exo Shaders privacy statement

**Exo Shaders is local-first. It has no Exo account and no ads. Usage data and crash reports are off until you choose to share them (see Help improve Exo below).**

Exo Shaders ships inside Exo Launcher and runs as its add-on. Launcher's [PRIVACY.md](https://github.com/UhhErix/ExoLauncher/blob/main/PRIVACY.md) lists every address Exo Launcher contacts. This statement covers what Exo Shaders itself does.

Last updated: 2026-11-06.

## What stays on this PC

Exo Shaders keeps its data in `%LocalAppData%\SCSKiller`, the folder SCSKiller uses:

- settings, the scan results, and each game's state (`games\<id>\state.json`);
- its compile records and the logs (`app.log`, and the one before it);
- recordings you make, and the limit you set for them;
- your sharing choices and your telemetry choices, the install ID and the queue of events waiting to be sent (`telemetry\`), and the last payloads for **View what's sent**.

Compiled shaders go into your graphics driver's own cache, not into Exo's folders. Recording is off until you turn it on. Then a small `d3d12.dll` sits in a game's folder and records which pipelines the game creates. It stays on this PC, and it refuses anti-cheat games. Frame times stay on this PC; they are not part of anything Exo sends.

## Help improve Exo

Exo asks you once, on its first launch and once after this update, with the same screen as Exo Launcher: **Help improve Exo**. It has two equal buttons, **Share app data** and **Don't share**, and a separate switch for **Send crash reports**. Nothing is ticked in advance, and closing the screen counts as **Don't share**. Settings > Privacy changes either choice any time, shows **View what's sent**, and runs **Delete my data**.

If you choose **Share app data**, Exo Shaders' events travel through Exo Launcher's sender, to Exo's own server. They hold:

- your Windows build, graphics card and driver version, and memory class;
- how many games were found, and for each game its store app ID, store, and whether its compile or recording finished, with ranges for shader counts and time;
- whether a game's shader cache was found ready, missing or cleared, and which graphics vendor it was;
- when a compile paused, and why (a game was running, you were using the PC, memory, or anti-cheat), and each anti-cheat game it left alone, by store app ID only;
- settings you change, as names and values, and errors, as kinds.

A **crash report** (its own switch) holds the error and its scrubbed stack, the end of the scrubbed log, Windows build, app version and your install ID, as described in Exo Launcher's statement.

| Collected (only if you choose to share) | Never collected |
| --- | --- |
| Windows build, GPU model and driver, memory class | Game names, folders and install paths; your user name (shown as `%USER%`) |
| Game counts; per game, a store app ID and the compile or recording result | File names, file paths and the contents of shaders or recordings |
| Shader-cache results (ready, missing, cleared) by graphics vendor | Your Steam, Epic or other store account, and anything in it |
| Pauses and their reasons; anti-cheat games skipped, by store app ID | IP addresses (dropped on arrival, never stored), precise location, advertising IDs |
| Setting names and values; error kinds, with scrubbed log lines | Hardware serials, MAC addresses, lasting IDs, fingerprints of your PC |

Events are sent at most every five minutes, in requests of up to 64 KB, to `reports.getexo.app`. The install ID is random, one per app on this PC, not tied to your account or hardware, and is replaced every 90 days or when you choose **Delete my data**. Usage events are kept 90 days and crash reports 30 days. After that only totals remain, with no install ID. Everything is stored on Cloudflare, on Exo's own account. No third-party analytics SDK is used, and nothing is sold or shared.

## Connections to SCSKiller's servers

Exo Shaders is a fork of SCSKiller, and it still uses SCSKiller's servers for the things below. They are not part of Help improve Exo.

- **Game lists and engine-support updates** from `api.scskiller.com` (fallback `api.scskiller.io`), after a scan. The updates are used only when signed with SCSKiller's own keys.
- **Community database and official recordings** from the same servers. They are downloaded only when you have signed in with Patreon and your membership entitles you.
- **Patreon sign-in** (optional), done on Patreon's own page in your browser. It is only needed for the community database and backer recordings.
- **Sharing your recordings** is off until you turn it on. When on, it uploads only the hash-only form of your recordings (no shader code), under an anonymous device identifier that is not linked to your Patreon account, as SCSKiller's policy describes.
- **Public GitHub-hosted files**: the Oodle and zlib-ng decompression libraries, and a game's public archive key where one is needed, fetched once the first time they are needed.

Exo Shaders does not run SCSKiller's optional daily active-install check, and it has no "Report a problem" sender: both are switched off in its code. It does not update itself either; Exo Launcher installs its updates.

## What Exo Shaders does not do

- No ads, no advertising or third-party analytics SDK, and nothing sold or shared.
- No upload of your game files, install folders, or the contents of a shader cache.
- No Exo account. Patreon's sign-in is optional and goes to Patreon.

## Questions

Open an issue at <https://github.com/UhhErix/ExoLauncher/issues>. Issues are public: leave out anything personal.
