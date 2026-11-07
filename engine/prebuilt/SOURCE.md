# Prebuilt native files

Built from this repository's `proxy\` at main (b2ddba2; the proxy source is the 1.4.6 merge plus Exo's warm watchdog commits
20cf05e, 5159066, d95a936, 0f05b12) with `-DSCSK_VERSION=1.4.6-exo.1`, Visual Studio 18 Build Tools (MSVC 14.51, its bundled
CMake 4.3), Release:

```
cmake -S proxy -B <x64 folder> -A x64 -DSCSK_VERSION=1.4.6-exo.1
cmake -S proxy -B <x86 folder> -A Win32 -DSCSK_VERSION=1.4.6-exo.1
cmake --build <folder> --config Release
```

The tests (AppTests, OverlayIniTests) run the test-hook build in `proxy\build` instead, configured with
`-DSCSK_VERSION=0.0.0-internal.0`: that turns on SCSK_TEST_HOOKS, and it is never shipped.

Earlier builds of these files (1.4.4) lagged the source and are replaced by this build. These carry Exo's own change, the
recorder log's count of creates over 20 ms. A version other than `0.0.0-internal*` leaves `SCSK_TEST_HOOKS` out, as a
release's does. `native\x86\d3d9.dll` comes from the Win32 folder, everything else from the x64 one.
publish-service.ps1 copies them into engine\out\service in the app's layout.

Not code-signed: SCSKiller's signature came with its release, and these are Exo's builds.

| File | SHA-256 | From |
| --- | --- | --- |
| native/VkLayer_scskiller.dll | 2a8f112500340b6bc9534a020163bfd7446570c6008d8d69ed5caf3a96883bcc | built here |
| native/d3d12.dll | f91dd41d051315273fc1aa2f868334e1d3b7e09b0e79068db7d25d6d0269e50a | built here |
| native/d3d9.dll | ede0e18588c68abf05d3b2409a470498ba90d3de679d3d46d60c005db40c529d | built here |
| native/fossilize-replay.exe | 004ee4bc5078bb00b3bedd35c1fa998be0d6d8263194da72e5234132724e9fa9 | built here |
| native/preload/dinput8.dll | caf024dce758dc51f3e33d921efbac5a10e4c3315d39d83d6e1f0fc675bceff0 | built here |
| native/preload/version.dll | af160caca1b001e7c2e94d38a059fc1cb41a8e8a743e4ea3749209e18207a477 | built here |
| native/preload/winmm.dll | e2c764a685f002f133e44711b872c2859108e4ba800cca23313ef80c0cab235d | built here |
| native/scskiller_ags.dll | 443b65218fa344812d3b317dc65b79ee0c2d0e45cee19de69223f4af2e5151b6 | built here |
| native/scskiller_warm.exe | b657df1df34bb2a9ade27a6538a6198de91f4fe6b566ca617b048f0fa8bff67b | built here |
| native/segheap/scskiller_warm.exe | 74c772fd192fe807a27facdd73c598b2f94f2bcf959a49c8aeb74445c0e2d72f | built here |
| native/x86/d3d9.dll | 758e6cb9e4710c68219c23e270d48fb3a1f601c3e89ffd38781ef36f2df7a38c | built here |
| native/amd_ags_x64.dll | 482b241ea855fdd59aaa7687c385ce4165b5340857e6a0e6d15ba27ecb65ee70 | AMD's, unchanged |
| zlib-ng2.dll | 454be2f3d10f804ace577198401431db5e95d0286b59589bc28a40085388e7c2 | SCSKiller 1.4.2's release, unchanged since 1.3.0 |