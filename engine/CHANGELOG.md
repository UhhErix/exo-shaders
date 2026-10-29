# Exo Shaders changelog

Exo Shaders is Exo Launcher's shader precompiler add-on, an unofficial fork of
[SCSKiller](https://github.com/BlueHeisenberg/SCSKiller) (GPL-3.0-or-later). SCSKiller's own changes are in the
repository's root `CHANGELOG.md`.

## [Unreleased]

**A compile's pipelines reach the game's Windows shader cache, and nothing useless stays**

- On NVIDIA from 621, the step that moves a compile's pipelines from its staged exe's D3DSCache folder into the game's
  own read every database and log of the D3DSCache folders made since, each into one array. One file over 2 GB, of any
  program, failed it with "Arithmetic operation resulted in an overflow" at every start and game exit: the pipelines
  stayed in folders named for the staged exe's path, which the game never opens (Beast of Reincarnation: 9.6 GB in
  two), the stranded check of 7 games failed the same way, and so did the Cache page. Those files are now read 4 MB
  at a time (`D3DSCache.ScanRows`), so the waiting folders go into their games' at the next start and are deleted.
  Reading this PC's D3DSCache for Beast of Reincarnation, the 1.4.1-alpha.2 engine's search threw after allocating
  499 MB; the new one finds both of its folders in about 0.1 s with 22 MB, and reads a real 2.4 GiB WAL in 1.1 s with
  4 MB.
- A folder the runtime made for another program, one of whose databases can't be read without its log (a game that
  quit with its newest pipelines still in the WAL), is that program's: no longer a doubt that kept a compiled game
  "the compiled pipelines go into the game's cache when the game exits" for good, and its log isn't read.
- After a driver update, a staged or waiting database an earlier driver compiled, which no driver reads, is deleted
  instead of merged into the game's folder (`D3DSCache.EarlierDriver`): its compiler differs from this driver's for
  the same GPU family. This driver's compiler is the one a warm on it recorded, or, before any warm on it, for a
  database older than the driver's install (NVIDIA's DriverStore folder), the one every database of that adapter
  written since names when they agree. Read on this PC after 621.01 -> 621.12: the dbs written since name
  1128470231318528, and both of Beast of Reincarnation's staged databases (9.6 GB, 1128466500354048) are deleted
  rather than written into its folder. A database written since the install is never one, nor one of another GPU
  family. A compile on this driver while the game's own database is still the earlier driver's waits for the game's
  next run and is never deleted: waiting databases go only by the same test, no longer by the game's database.

**SCSKiller 1.3.4 and 1.4.1-alpha.2 (engine 1.4.1-alpha.2)**

The engine merges SCSKiller's published 1.3.4 (the branch shares its history), in the 1.4 code's form where alpha.2 shows
how SCSKiller combined the two, and carries alpha.2's own changes, written from its binaries as alpha.1's were. The native
files are alpha.2's (`engine/prebuilt/SOURCE.md`): 1.3.4's compiles pass the compile program two arguments alpha.1's
refuses. As they reach Exo:

- A game with Advanced Shader Delivery compiles only what Windows didn't deliver. A pipeline that crashed or hung the
  GPU driver stays skipped on later compiles until the driver changes; one that hung is tried alone first, and one that
  hangs before the compile's workers start no longer stalls it. Progress keeps its total after a crash or hang.
- The game page says why pipelines couldn't be compiled (the driver refused them or their root signatures, or the
  engine couldn't build them), and the coverage ring shows what the last compile covers: pipelines found since show only
  in the line that offers to compile them.
- Plan checks after an update take at most a quarter of the RAM, up to 8 GB; a compile started by hand no longer waits
  for them, and a check that can't finish keeps the game's plan instead of running again at every start. The Library's
  line says what runs (`afterUpdateLine`), and handing a compile to the game's cache shows its progress.
- Unreal games with a recording compile all their pipelines again on NVIDIA (the affected games compile once more);
  Unreal 5.8 compute shaders with NVIDIA's extensions compile as the game creates them; a game partly readable without
  its AES key says it needs the key and compiles the rest; no extra root-signature layout for NVIDIA players without a
  recording.
- On NVIDIA, pipelines whose pixel shader sets the stencil reference aren't compiled (they always fail), a stopped
  compile's pipelines aren't compiled again after a plan change, and per-stage plans pair what one pipeline covers
  (ELDEN RING 39,814 to 31,773 pipelines, same coverage).
- A Plague Tale: Requiem compiles every combination in its files, the RE4 Chainsaw Demo compiles from its own root
  signatures on AMD, Resident Evil Requiem (demo) and Dragon's Dogma 2 Character Creator compile most of their
  pipelines, Crimson Desert about 98% of its combinations before its key is added, and Microsoft Flight Simulator 2024
  takes its compiled cache.
- DLSS games whose exe loads none of winmm, version or dinput8 (Dying Light: The Beast) get the recorder's preloader
  under another System32 name the exe imports, and no game reads Not supported because of DLSS.
- DirectX 9 games say compiling them waits for DirectX 9 support, DirectX 10 games read "DirectX 11 (runs on DirectX
  10)", and a small launcher beside a DirectX 9 exe reads as DirectX 9. BeamNG.drive isn't taken for a 32-bit game and
  is recorded on whichever API it runs.
- Vulkan games compiled into Steam's shader cache compile again after Steam clears it (NVIDIA and AMD), and on AMD a
  compile no longer replays every pipeline while that cache is intact.
- A game whose shader cache folder was moved with a junction gets its compile, a damaged cache file or an early driver
  update no longer loses a compile, and the hand-off after a session reads the game's whole cache once a week.
- The cache card's limit list has the NVIDIA app's sizes, 512 MB to Unlimited.
- Not in Exo: "Always record" and the recording sessions 1.3.4 offers for anti-cheat games and unknown steam_appid.txt
  apps (Exo never records an anti-cheat game: its policy keeps that off and the engine never arms one), and the official
  app's own screens.

**SCSKiller 1.4.1-alpha.1 (engine 1.4.1-alpha.1)**

SCSKiller's 1.4.0-beta.1 and 1.4.1-alpha.1 came to its Patreon alpha channel as binaries only: their source isn't public.
The engine carries them by studying those GPL binaries (their decompiled Core, the changelog they embed and the native
files' strings) and writing each change again in this fork's code; the native files are 1.4.1-alpha.1's own
(`engine/prebuilt/SOURCE.md`). This branch stays local until SCSKiller publishes 1.4's source. From SCSKiller's own
changelog (the root `CHANGELOG.md`), as they reach Exo:

- Traversal stutter fix (1.4.1-alpha.1, experimental): the recorder can place a DirectX 12 game's new resources in heaps
  it pools, which may reduce the big stutters when a game streams in a new area. Off unless chosen: the settings policy
  leaves it as it is (the official app's choice where there is one), `settings.setTraversal` turns it on for every game
  and `game.setTraversal` for one. It runs only where the recorder runs, and never in a game with anti-cheat, VAC
  included (SCSKiller allows those at the user's risk; Exo Shaders doesn't). Recommended for GPUs with more than 8 GB.
- A game that switches between DirectX 11 and 12 (or Vulkan) keeps both compiles: switching back no longer shows 0% and
  asks for everything again. The game page says which other API is compiled (`keptApisNote`).
- On NVIDIA, the pipelines a game ships (Unreal's pipeline cache) or a recording holds compile first, before the ones
  the planner pairs itself, and a game's compile size limit is no longer ignored on NVIDIA. Unreal games' shipped vertex
  declarations give the planner exact layouts where a recording has none.
- A recording whose root signatures the engine's rule rebuilds in half or more of the cases, but not all, compiles the
  rest with the rule's instead of leaving them out.
- A compile on a GPU driver that crashes on a large share of the pipelines stops after about a thousand of them instead
  of recovering for days; compiling again continues past them. Compiling again after a compile stopped on driver crashes
  no longer stops after a few pipelines on one thread (1.4.1-alpha.1's compile program reports what it passed over).
- Compiles are refused while the NVIDIA shader cache is turned off (Shader Cache Size 0), with how to turn it back on;
  the cache card says so (`cache.off`).
- A compile that waits shows why: paused until the PC is idle, or waiting for an earlier compile to go into its game's
  shader cache.
- Games that load ReShade and RenoDX through Special K compile through that ReShade and its add-ons when it is certain
  which ReShade Special K loads; otherwise the page says what to change.
- DirectX 9 games (Half-Life 2) and OpenGL games say they aren't supported yet; FNA games say their shaders are built
  while they run. A Vulkan game whose launcher starts its renderer from a subfolder is detected as Vulkan, and the
  Vulkan recorder follows that renderer from the launch after it is first seen.
- On NVIDIA, DirectX 11 games run the recorder for the frame-time graph in regular play too ("Record in all compatible
  games"), recording nothing; their frame lines no longer claim "no stutters from shader compiles". Games that need a
  recording get the recorder with "Record in all compatible games" too.
- On NVIDIA, a game's disk space counts only the driver cache the game and its last compile use; keys no longer in use
  whose files are gone leave its record. With a compile size limit, each pipeline's Windows shader cache copy counts
  too (621 and later).
- Marvel Rivals' first compile estimates its cache from what other PCs measured (about twice the old figure). A plan
  five times the last compile's says so on the game's status. A Vulkan session in an anti-cheat game no longer says the
  game ran as administrator when that couldn't be told. Exo's API choice offers Vulkan wherever the GPU has a Vulkan compile.
- New native files (1.4.1-alpha.1): the recorder steps aside from frame generation's swap chain and works in Dragon's
  Dogma 2's Character Creator, frame times are measured with OptiScaler, HelixSR and DLSS, FSR or OptiScaler frame
  generation, a recording near its size limit keeps the pipelines with new shaders, and on AMD the recorder's AGS
  chain is renamed (amd_ags_x64_scskiller.dll), so it installs in Cyberpunk 2077 2.5 again; chains alpha.3's wrapper
  made go out and back in under the new name.
- Not taken (the official app's, not the engine's): the updater, alpha gate and toast registration as before, the
  traversal fix's one-time question, the Cache page's icons and "Open folder" picker, "Move to bottom" (`queue.move`
  already moves anywhere), the before and after image's version corner, and "Use shared shader hashes" per game (in the
  engine, but Exo Shaders never downloads them).
- Planner 47 (as SCSKiller's): every plan is rebuilt at the next compile or plan check; a compile is asked for only
  where the new plan adds pipelines.

**Starts on every NVIDIA PC**

- A display driver whose registry names its user-mode driver by file name alone (no folder) made the engine stop at
  start with "The path is empty" (SCSKiller #229, seen on a GTX 1070); that entry is skipped now, and one unreadable
  display key never stops the engine.

**Runs only while it's needed**

- With `exitWhenIdle` the service exits by itself once nothing has needed it for that long: no page shown, no request,
  scan, compile, plan check or background pass running, and no work waiting for the PC to be idle while the user is
  away. Exo Launcher starts it for its page, a compile, a check or idle-time work, and otherwise nothing of it runs.
  `exiting` says whether work waits for the PC to be idle (`idleWork`), and every clean exit leaves the page's state in
  `exo-snapshot.json` for the host to show until the service runs again.
- A request about a game waits for the first scan's list (a second or two after start), so a click on the page's last
  state while the service starts finds its game.
- The precompiled shader databases a driver update left behind are looked for once the games are listed, and only then
  does the service wait for the PC to be idle; a start with nothing to rebuild no longer checks for idle twice a second
  for as long as the user is at the PC.
- Blocking garbage collections with no background thread: a page visit's scan peaked at 142 MB instead of 186 MB.
- Games never compiled now compile while the PC is idle, as the settings policy said since 1.4.0-alpha.3
  (`compileNewGames`): nothing queued them.

**Precompiled shaders built on this PC**

- A compiled DirectX 12 game also gets Windows' precompiled shader database (Advanced Shader Delivery) built on the PC
  from its compile, with the graphics driver's own compiler, at idle priority after the compile. Exo registers it for
  the game's exe, and the game then loads those pipelines ready-made: not bound by the driver cache's size or cleanup.
  A driver update rebuilds it while the PC is idle. A game with a database from its store keeps the store's.
  Microsoft's compiler is downloaded the first time it's needed. Needs Windows 11 24H2 and a driver with Windows'
  compiler support (NVIDIA RTX 621.01 and newer).
- A game a store delivered precompiled shaders for, installed again from another store (or as another copy), gets
  them too: the store's database lists every pipeline the game builds, independent of driver and folder, so Exo builds
  this PC's database from it for that copy, with no compile or recording of its own. Matched by the exe's file name and
  the game's name together, never over a database of the copy's own.

**Statuses and times you can trust**

- An Unreal game some of whose files no key opens (Teamfight Tactics) reads "Ready · some files encrypted" and compiles
  the rest. It said "Not supported" while its compile was offered and ran. Only a game whose shaders all stay
  encrypted is not supported.
- A game says what it waits for. One whose launcher clears the shader cache at its first start of a new install or
  season (Marvel Rivals), or whose Vulkan shaders come with Steam's download at its first start, says "Start it once
  from Steam" instead of asking for a recording. One whose ray tracing alone needs a recording reads "Ready, ray tracing
  needs a recording", and Compile does the rest.
- Compile times are learned on this PC. A game's estimate is its own compile's rate on this driver; until it has one, a
  stand-in marked as rough: its own rate on another driver or from a stopped compile, its engine's on this driver, this
  PC's, or the vendor's default. Reading its shaders, planning and preparing its files count in the time. A stale game
  is estimated for its new pipelines, and after a game update for its new ones up to all of them, since its cache may no
  longer hold the rest. On NVIDIA 621 every estimate had fallen to 450 pipelines a second, because the rates compiles
  measured were looked up under another key: Counter-Strike 2 read 5 min, where its one cold compile took 1 h 22 min.
- A rewarm onto a cache that still held the game no longer counts as a compile: NVIDIA kept DirectX 11 caches across
  the 621 update, and VALORANT's 33 s rewarm made its 4-min compile read "under a minute". Such a rate is never kept as
  the game's, and its time isn't shown as the last compile's. A compile's time no longer counts the minutes it was
  paused while you played.

**Built on SCSKiller 1.3.0's own source**

- The engine is SCSKiller 1.3.0 built from its published source, in place of the engine recovered from the
  1.3.0-beta.2 program. The branch shares SCSKiller's history, so later SCSKiller releases merge in.
- Everything Exo Shaders added is carried over unchanged for Exo: the headless service and its protocol, the fixed
  settings policy, the quarter-of-RAM memory budget, the driver-update task, Exo's page, and the readers and launchers
  below. The native helpers are SCSKiller 1.3.0's own (`engine/prebuilt/SOURCE.md`).
- The game page follows 1.3.0's: Left out says how many shaders weren't seen in a recording yet, and with recording on
  it says to keep playing instead of asking for a recording.
- Games no longer mention the community database ("in the community database for Patreon supporters", "not in the
  community database yet"): it needs a Patreon sign-in, which Exo Shaders never has.
- A game whose new shaders wait to compile when the PC is idle is queued again after Exo restarts, and one held back
  by another compile is queued once that compile ends.
- On a PC with 64 GB of RAM or more, compiling gets a quarter of the RAM (SCSKiller 1.3.0's Auto), not 16 GB.
- Every game with the recorder is checked for anti-cheat at each pass again: skipping unchanged games could miss an
  update that installs anti-cheat deeper in the game folder.

**From SCSKiller 1.3.0**

- Fixes from the final release: Wuthering Waves from its own launcher is read as Unreal; a busy PC no longer loses a
  game's recorder for good; a big compile at its memory limit keeps its threads; The Last of Us Part II Remastered and
  other games with Sony's crash reporter compile for the game's own exe; Mafia: The Old Country and other games that
  start NVIDIA's crash reporting with shader debug info use their compiled shaders; STAR WARS: Galactic Racer compiles
  into the cache the game reads on AMD.
- Naughty Dog games, God of War and God of War Ragnarök, Ghost of Tsushima, Ratchet & Clank: Rift Apart, the Riot
  Client's games, and DirectX 11 games on AMD, as in 1.3.0.

**From SCSKiller 1.3.1 and 1.3.2 (engine 1.4.0-alpha.4)**

- The engine now carries SCSKiller 1.3.2 on top of 1.4.0-alpha.3's source: every change of 1.3.1 and 1.3.2 is in, in
  alpha.3's form where alpha.3 had reworked the same code (the root `CHANGELOG.md` lists theirs). The history is merged,
  so the next SCSKiller release merges in the same way. Nothing in the service's protocol changed.
- On NVIDIA from driver 621.01, DirectX 12 compiles no longer wait for Windows' shader cache to flush each write: about
  1.7 times as fast. The time estimate uses the rate measured on the current driver only.
- A compile counts as done only once its shaders are in the game's Windows shader cache, which is never written while the
  game runs. Shaders that couldn't go in yet go in when the game exits or the engine starts, and the game says so until
  then ("the compiled pipelines go into the game's cache when the game exits or SCSKiller starts", Warmed). A damaged
  Windows shader cache reads as Stale and compiles again.
- Games that get shaders through Advanced Shader Delivery in the Xbox app keep the pipelines compiled for them; a game's
  disk space counts the part of its compiled shaders Windows keeps in D3DSCache; older drivers' cache under
  AppData\LocalLow\NVIDIA\PerDriverVersion\DXCache is found; a path with ".exe" in a folder name, a junction or a
  subst drive no longer keeps a compile out of the cache.
- A compile no longer makes the PC unresponsive: its work, the graphics driver's too, runs below other programs, and
  reading a game's files follows the Threads setting.
- After a driver update, the pipelines that crashed the old driver are retried one at a time before the rest.
- Roblox is an anti-cheat game: the recorder stays out of it, and is taken out if it was in. The anti-cheat rules are
  unchanged otherwise: the engine only reads such a game's files.
- Unreal games whose store starts another exe beside the Shipping exe (STAR WARS Jedi: Fallen Order) compile for that
  exe after its next launch.

**SCSKiller 1.3.3's fixes**

- The engine (1.4.0-alpha.5) now carries SCSKiller 1.3.3 on top of 1.4.0-alpha.3's source, as it did 1.3.1 and 1.3.2.
  The service's protocol is unchanged: a compile's new steps keep the stage `warming` and name themselves in `stageText`.
- After a compile, copying it into the game's Windows shader cache, the driver saving its cache and the bookkeeping after
  it show as their own steps, with their progress or time, instead of sitting at 100%. The copy takes seconds when the
  game's cache already holds it, and a big cache isn't checked whole again while the game hasn't changed it (about an
  hour for 100 GB).
- A compile no longer shows Compiling forever: it ends about 30 seconds after its process exits when a leftover process
  holds its output, and a DirectX compile that makes no progress for 10 minutes ends with an error.
- A compile stopped by the game starting or by GPU driver crashes and hangs keeps its progress, and compiling again goes
  on past the spot where the driver kept failing. A compile no longer stops after a few hundred pipelines that crash the
  driver. After a compile that was stopped and continued, the next one on the same driver leaves out what it compiled.
- On NVIDIA, a rebuild after a launch that still compiled a few new pipelines compiles only those, and "the compile
  didn't help" clears once a later launch shows the compile in use.
- 32-bit games say they aren't supported yet. A game whose files hold no shaders Exo Shaders can compile says so after its
  first compile. A partly compiled game counts the pipelines the driver crashed on apart from the ones it refused. When
  the recorder can't go into a game, for example because an antivirus removed its file, the game page says why.
- Call of Duty HQ and Warzone count as anti-cheat games, also when added by hand. Anti-cheat games compile through their
  ReShade and RenoDX too, and reinstalling the same ReShade or RenoDX files no longer asks for a new compile.
- A game recorded at several texture filtering settings compiles the one it uses and the last other one, not each. On
  NVIDIA part of Gears of War: E-Day's pipelines now match the game's; on AMD God of War Ragnarök compiles into its own
  driver cache; Resident Evil Village leaves out the 18 pipelines whose shaders don't fit together. On NVIDIA from 621, a
  game moved to another folder or drive keeps its compiled shaders. Clear cache deletes the game's Windows shader cache
  on every driver, an anti-cheat game's too.
- A game's file stamps no longer include write times, so the same build reads the same after a reinstall. Each game's
  files are read once more and its plan is rebuilt; a compile is asked for only where the new plan adds pipelines.
- Already in Exo Shaders before: the start on drivers that list their files without a folder (SCSKiller #229), older
  Unreal Engine 4 games' memory limit, DLSS games recorded through the preloader, Ubisoft Connect games with the recorder.
  The community database's fixes come with the code but do nothing here: Exo Shaders never uses it.
- Waiting for newer native files, which came with 1.4.1-alpha.1's (above): these fixes are in the recorder and compile
  programs, and Exo Shaders shipped SCSKiller 1.4.0-alpha.3's until then. Before them: frame times stayed off with OptiScaler
  or another mod as dxgi.dll, with FSR mods that rename the game's FSR file (HelixSR), with DLSS and OptiScaler frame
  generation, and after a swap chain is re-created; the recorder doesn't step aside from frame generation's swap chain,
  and parts of it stay active where it doesn't run; Dragon's Dogma 2's Character Creator isn't recorded; on AMD the
  recorder can't go into Cyberpunk 2077 2.5, which ships a file under the name alpha.3's AGS wrapper uses (the install
  stops and leaves the game as it was); a very big plan such as Marvel Rivals with all its combinations can still fail
  after about 3 minutes while it loads; on AMD a DirectX 11 compile still leaves an unused DirectX 12 cache file; and the
  10-minute progress check sees whole pipelines only.

**New engine: Glacier 2**

- 007 First Light compiles without a recording: Exo Shaders reads IO Interactive's Glacier 2 archives (RPKG) and the
  shaders in the game's material effects, each with the root signature the game creates for it. It was "Not
  supported yet". Its ray tracing pipelines still come from a recording. Updates that patch the archives are read
  too: a patch's effects replace the ones it updates.

**Engine-support updates**

- Exo Shaders now reads SCSKiller's engine-support updates, as SCSKiller does when you are signed out: after a scan, at
  most about once a day, the rules that decide what a game compiles, game verdicts and Maximum suggestions, each used
  only when it is signed with SCSKiller's own key. The first one adds Black Myth: Wukong's engine.
- Official recordings SCSKiller publishes for everyone, and the shared upscaler pipelines, download without an
  account.
- Nothing is sent: no Report a problem, no recording sharing, no diagnostics, no daily check, no Patreon or backer
  sign-in and no community database.

**Carried over from the decompiled engine**

- Games from every launcher Exo reads: the Epic Games Launcher (also its registry data folder), Legendary and Heroic,
  GOG Galaxy (HKLM and HKCU), Exo's own gogdl installs and Heroic's GOG list, Amazon Games and Nile, Ubisoft Connect,
  Battle.net and EA (both registry views, Origin's folders), Xbox's ModifiableWindowsApps, and Exo's own library.
- Sekiro: Shadows Die Twice, Marvel's Spider-Man Remastered and Miles Morales (their asset archives), Unity games that
  compress shaders with zstd, and Unreal keys from a game's public key list when its exe is protected (Wuthering Waves).
- A faster planner (the same plans) and a lighter idle engine.
