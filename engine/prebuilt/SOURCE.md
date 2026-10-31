# Prebuilt native files

Taken unchanged from SCSKiller 1.4.2's release (github.com/BlueHeisenberg/SCSKiller, release v1.4.2,
`SCSKiller.App-1.4.2-stable-full.nupkg`, SHA-256 7a5f82a10b2ed19e6d7d8a0d6d32e54dded82a5166783a15a83545022ce51adb, the
same hash as the release's `releases.stable.json`; `lib\app\`), downloaded 2026-10-30. Their source is public: `proxy\`
holds upstream v1.4.2's (v1.4.3 changes no native file), merged with the fork's in `exo-shaders-upstream-1.4.3`. So these
are the release builds of the source in this repository, and the alpha-only state this folder had before is over.
publish-service.ps1 copies them into engine\out\service in the app's layout.

What changed since the 1.4.1-alpha.2 files this folder held before:

- `native\d3d9.dll` and `native\x86\d3d9.dll` are new: the DirectX 9 recorder for 64- and 32-bit games.
- Every other file but amd_ags_x64.dll and zlib-ng2.dll was rebuilt (1.4.2's recorder, traversal fix, warm and Vulkan
  layer).

| File | SHA-256 | From |
| --- | --- | --- |
| native/VkLayer_scskiller.dll | 44e8568a966c702ea456a147163b018821648e6c7a37c48f48daa42b42501da8 | SCSKiller 1.4.2 |
| native/amd_ags_x64.dll | 482b241ea855fdd59aaa7687c385ce4165b5340857e6a0e6d15ba27ecb65ee70 | SCSKiller 1.4.2 (AMD's, unchanged) |
| native/d3d12.dll | 042b6c1eb874ba4cd9c131b3739e19096c88d32536ceb09dca9126ae79b8314e | SCSKiller 1.4.2 |
| native/d3d9.dll | e21f0d9a29a21950db402cf80070c46b538a68693659c0640b022651e9d8b844 | SCSKiller 1.4.2 |
| native/x86/d3d9.dll | b82e6bc1479dfc210af597758fe0df5a08280d6328630bd17e62e0081b3b40bd | SCSKiller 1.4.2 |
| native/fossilize-replay.exe | 709398a1f1bdd1dab303dd77afee4c5952fb4c4f454bde1a8696f24802ed97f1 | SCSKiller 1.4.2 |
| native/preload/dinput8.dll | f5b43e23e1186c64025ecb2943f339693b3bb2848e0f85837fc875e6c0fd535b | SCSKiller 1.4.2 |
| native/preload/version.dll | bb37bf6ae31110bccf7205993cd34b5127c3c5c61ad03aa6479aa2b3540f9fb0 | SCSKiller 1.4.2 |
| native/preload/winmm.dll | bdef732ea565b71014e6a8df360395e51238e611ca2e37b191f93a7d26247b1e | SCSKiller 1.4.2 |
| native/scskiller_ags.dll | 3b0079634a1504645461e9e0dc2dafd0debc179cd1eef8fddd19f2622d4d3319 | SCSKiller 1.4.2 |
| native/scskiller_warm.exe | e2850c132c993e5f92339ed207c16890943dd36601fdf4dc412d2d0cfd11c500 | SCSKiller 1.4.2 |
| native/segheap/scskiller_warm.exe | 6c9d065c31e493187a5dbc3dc4cc8e57c9bc9453aa0a577ced674c6f78a79cc4 | SCSKiller 1.4.2 |
| zlib-ng2.dll | 454be2f3d10f804ace577198401431db5e95d0286b59589bc28a40085388e7c2 | SCSKiller 1.4.2 (unchanged since 1.3.0) |
