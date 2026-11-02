# Prebuilt native files

Built from this repository's `proxy\` (commit eed9dd5 and its source tree) with `-DSCSK_VERSION=1.4.3`, MSVC 19.44 (Visual Studio
2022 Build Tools), Release:

```
cmake -S proxy -B <x64 folder> -A x64 -DSCSK_VERSION=1.4.3
cmake -S proxy -B <x86 folder> -A Win32 -DSCSK_VERSION=1.4.3
cmake --build <folder> --config Release
```

SCSKiller's 1.4.2 release builds, which this folder held before, lag the source: v1.4.3 changes `traversal.h` (the
`hk_priority` fix), so they were never the build of this repository's source. These also carry Exo's own change, the
recorder log's count of creates over 20 ms. A version other than `0.0.0-internal*` leaves `SCSK_TEST_HOOKS` out, as a
release's does. `native\x86\d3d9.dll` comes from the Win32 folder, everything else from the x64 one.
publish-service.ps1 copies them into engine\out\service in the app's layout.

Not code-signed: SCSKiller's signature came with its release, and these are Exo's builds.

| File | SHA-256 | From |
| --- | --- | --- |
| native/VkLayer_scskiller.dll | 5e1ac1bf46d7cceb56169152ddc3018a7ee742b86d85ae3ded31a7d793084134 | built here |
| native/d3d12.dll | 26cb5e23e546611a1ba649a2b513f1bbd41b1f4089a70db25ba0510396cda0c6 | built here |
| native/d3d9.dll | c4b4e458e4d9733bd78e85f275f178d3dfdfceb935950ca3ed45d6e7f20f1949 | built here |
| native/fossilize-replay.exe | eba4195737d3cb78d311988d4fe0bf3f1093039504474801922a734d8bb6a619 | built here |
| native/preload/dinput8.dll | 6bf8e43b79f604d835cf61df0db2d6bc277a777fed378ccda70cc0278f41ddf0 | built here |
| native/preload/version.dll | d4bf5eef0b9a5dbc47fad6278310d7c258246cda710ba63bfebd5e703b65e592 | built here |
| native/preload/winmm.dll | 1422dc62b2f073b1e84aa70b52d53f33226229f52b86bea0dd71c62a65ea65ae | built here |
| native/scskiller_ags.dll | b3e11075b7e837847722639bcffbaf9c6cd0fe4975059d6d157fea3d6a7608a1 | built here |
| native/scskiller_warm.exe | c361bbcf453ba3c77da4826153e4da38142f887c4920d8804739cc645f9cdbf5 | built here |
| native/segheap/scskiller_warm.exe | 67947710681f179414a119f6d1dd9d196bea3d7cd3f1e50e3ac8ba26d4872b34 | built here |
| native/x86/d3d9.dll | a4c05c5c15eee38cbb6d2d856a6e294f573a8709497489179ba513f99ec0c0ed | built here |
| native/amd_ags_x64.dll | 482b241ea855fdd59aaa7687c385ce4165b5340857e6a0e6d15ba27ecb65ee70 | AMD's, unchanged |
| zlib-ng2.dll | 454be2f3d10f804ace577198401431db5e95d0286b59589bc28a40085388e7c2 | SCSKiller 1.4.2's release, unchanged since 1.3.0 |
