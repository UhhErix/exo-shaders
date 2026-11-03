# Prebuilt native files

Built from this repository's `proxy\` (commit 058a1c1 and its source tree) with `-DSCSK_VERSION=1.4.4`, MSVC 19.44 (Visual Studio
2022 Build Tools), Release:

```
cmake -S proxy -B <x64 folder> -A x64 -DSCSK_VERSION=1.4.4
cmake -S proxy -B <x86 folder> -A Win32 -DSCSK_VERSION=1.4.4
cmake --build <folder> --config Release
```

SCSKiller's 1.4.2 release builds, which this folder held before, lag the source: v1.4.4 changes `traversal.h` (the
`hk_priority` fix), so they were never the build of this repository's source. These also carry Exo's own change, the
recorder log's count of creates over 20 ms. A version other than `0.0.0-internal*` leaves `SCSK_TEST_HOOKS` out, as a
release's does. `native\x86\d3d9.dll` comes from the Win32 folder, everything else from the x64 one.
publish-service.ps1 copies them into engine\out\service in the app's layout.

Not code-signed: SCSKiller's signature came with its release, and these are Exo's builds.

| File | SHA-256 | From |
| --- | --- | --- |
| native/VkLayer_scskiller.dll | 14dfa69f5fd78863f85052b68e0eb5ab2abdd65fc1efbe2f6a2d9ee4745cfc20 | built here |
| native/d3d12.dll | 9f59439d84a398429191a357c80f853576e9d90db5a5daf0d19f22aceb60e017 | built here |
| native/d3d9.dll | 5a0f7fb971048b4f18710e6abea33864793b8a34518b8d0a05dd77458d188c62 | built here |
| native/fossilize-replay.exe | cb0419314baf64ff3b348a25c57dba71550eff2a451b0fd9bf5274e319ea2c61 | built here |
| native/preload/dinput8.dll | bf835cc5b9a5c1d33a178c31c486ba1fd53e808c7c924b1bc2e00620e595bbd0 | built here |
| native/preload/version.dll | d1d2d700a1243c2595f65d6f5d556ba1d394afdd1d2c119d9dcdbbed5caab92f | built here |
| native/preload/winmm.dll | 01f09df3011281d9ee04838710abf8c3f2045d81ae71cafdfc8910283d0cadac | built here |
| native/scskiller_ags.dll | baa489c5a9a3fc8b1d26a0f0fde3689fed4e77d8aa7e813926e04f36fa4042bb | built here |
| native/scskiller_warm.exe | 7f3a9c2b1af45ddd406e5ae3dbf4e335d951bafaa3e352ac84cc03aaa2962126 | built here |
| native/segheap/scskiller_warm.exe | 1da1311bc8939fd22ca68cd20e20f3889af0a1b38eea8f43b7e31b30703fbd63 | built here |
| native/x86/d3d9.dll | 3f7bd9c40ef434306e65115ead0064af2c02f7210755d6e00d061ea77880277d | built here |
| native/amd_ags_x64.dll | 482b241ea855fdd59aaa7687c385ce4165b5340857e6a0e6d15ba27ecb65ee70 | AMD's, unchanged |
| zlib-ng2.dll | 454be2f3d10f804ace577198401431db5e95d0286b59589bc28a40085388e7c2 | SCSKiller 1.4.2's release, unchanged since 1.3.0 |
