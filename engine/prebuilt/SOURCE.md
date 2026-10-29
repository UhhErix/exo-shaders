# Prebuilt native files

Taken unchanged from SCSKiller 1.4.1-alpha.2 (the alpha feed's `SCSKiller.App-1.4.1-alpha.2-alpha-full.nupkg`, SHA-256
978de2901e70743e7889711dac611652fbd4eef695a9a871a216cd700da71913, `lib\app\`, as installed by the official app's updater on
2026-10-28). That alpha's source isn't public (no 1.4 tag, branch or release on github.com/BlueHeisenberg/SCSKiller, and
the package holds binaries only), so this branch stays local: it is never pushed or released until upstream publishes
1.4's source. `proxy\` holds the 1.3.x source of the DX12 files with 1.3.4's recorder and compile changes; the Vulkan
layer, fossilize-replay, the preload proxies, 1.4.0-beta.1's recorder changes and 1.4.1-alpha.1's traversal fix (the
`traversal_*` lines of the recorder's scskiller.ini, read by d3d12.dll and VkLayer_scskiller.dll) are in these binaries
only. publish-service.ps1 copies them into engine\out\service in the app's layout.

What changed since the alpha.1 files this folder held before (read from the binaries, not their source):

- scskiller_warm.exe (both): 1.3.4's `--known-keys` (items that crashed or hung this driver in an earlier compile,
  skipped and counted as `known`) and `--asd-name`/`--asd-engine`/`--asd-version`/`--asd-psdb` (the game's Advanced
  Shader Delivery, registered for the staged exe so the driver compiles only what it doesn't serve); `cached`, `known`
  and `culprits` in its progress, retry, done and error events, read by Warming/Warmer.cs. alpha.1's refused both
  arguments (its parser ends on an unknown one), so Core's 1.3.4 compiles need these files.
- d3d12.dll and scskiller_ags.dll: the exports the warm calls for them (`SCSKiller_CountCached`, `SCSKiller_Cached`,
  `SCSKiller_WarmKnown`, `SCSKiller_Known`), as in 1.3.4's `proxy\d3d12.def`.
- Every other file but amd_ags_x64.dll and zlib-ng2.dll was rebuilt too.

| File | SHA-256 | From |
| --- | --- | --- |
| native/VkLayer_scskiller.dll | 4e7695c2c8d638495f29b95a3b1c00c6a36cd26ec232bef02a156d336b122229 | SCSKiller 1.4.1-alpha.2 |
| native/amd_ags_x64.dll | 482b241ea855fdd59aaa7687c385ce4165b5340857e6a0e6d15ba27ecb65ee70 | SCSKiller 1.4.1-alpha.2 (AMD's, as before) |
| native/d3d12.dll | 55122abdcc081df148521fac4ae42d98f0468f8995de664d062a373cfcc40516 | SCSKiller 1.4.1-alpha.2 |
| native/fossilize-replay.exe | a527a5e1d1e08b011d3f6c59a59ac15084a983507ffc2c94b061b8da777dba17 | SCSKiller 1.4.1-alpha.2 |
| native/preload/dinput8.dll | 3b523fc250d28642efe6a0f744508fa311d5eeb2f24ea3eb6a4a6730d1db6039 | SCSKiller 1.4.1-alpha.2 |
| native/preload/version.dll | b01e7d9c9c713fe6196393195670d7af514af7c9e1622ea42fee8d215d351f4f | SCSKiller 1.4.1-alpha.2 |
| native/preload/winmm.dll | 47b378cc5a585e482b640b75307e7ea7a8619d12f05bc758ec6cbfbb8a20c33a | SCSKiller 1.4.1-alpha.2 |
| native/scskiller_ags.dll | a06e386e3afd08ba915bf1e21deeea31002a73d4d5a624f43d433a7704f2bf3a | SCSKiller 1.4.1-alpha.2 |
| native/scskiller_warm.exe | 222844faf7b11cedc68ddd8f4b6ae80863439df806e0f26e3f1f7cfd20ff3c69 | SCSKiller 1.4.1-alpha.2 |
| native/segheap/scskiller_warm.exe | 409774adf0712b84195fad884e2c103f19500e9909349766e1b5260692c3102d | SCSKiller 1.4.1-alpha.2 |
| zlib-ng2.dll | 454be2f3d10f804ace577198401431db5e95d0286b59589bc28a40085388e7c2 | SCSKiller 1.4.1-alpha.2 (unchanged since 1.3.0) |
