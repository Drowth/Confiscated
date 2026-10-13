# Windows candidate — 13 October 2026

Candidate: `D:/Confiscated/Builds/Release_2026-10-13_201510/Windows`

Manual upload: `D:/Confiscated/Builds/Release_2026-10-13_201510/Confiscated-Windows.zip`

- Previous local playtest install: 1,027,551,873 bytes (980 MiB).
- Candidate install: 660,881,578 bytes (630.3 MiB), 35.7% smaller.
- Verified ZIP: 552,627,401 bytes (527.0 MiB). This is approximately the previous itch download size; the main reduction is installed disk space.
- 190 files verified by reading every ZIP entry and comparing SHA-256 with its source.
- ZIP SHA-256: `CC158BB82210DC8581454F03724D46866A5F73F534ED9F77E09702E1DB427C15`.

## Changes

Lossless LZ4HC build compression; no reduction of texture resolution, audio quality or game content. Removed the unused Unity AI Inference dependency and its stale App UI build-setting entry. Editor tools were retained after the user's scope clarification. Build reports, debug symbols and logs stay outside the upload folder. The packaging checks reject Claude/Codex documents, agent/skill files, prompts, source files, logs, development symbols and the Steam test App ID. A late build callback removes the performance-test package's generated metadata before player resources are packed.

The candidate includes the cameraman's front/rear walking textures and voices, the cage hinge/sign/shutter-independent metal chain sound fix, and recurring Copycat encounters from commit 3d7a4ef.

## Verification

- Final Unity build succeeded: zero build errors, 85 warnings (mostly deprecated Unity APIs and editor bridge messages; details in the candidate's BuildWarnings.txt).
- Final standalone candidate launched through the studio splash into the title menu without Steam running. Title Quit button closed the process cleanly.
- Earlier candidate with the same gameplay code reached the classroom and Mr Reed's opening conversation, and accepted dialogue input.
- OpeningRouteSmokeTest: fresh PASS on 13 October; phone delivery, trolley route, both dining entrances, distraction and office-key interaction checked in the hand-authored scene.
- ComfortPolishSmokeTest: fresh PASS after correcting its synthetic-input timing; settings, audio mixing, FOV, hints and sprint input checked. The earlier failed attempt was a short synthetic-input deadline, not a claimed gameplay pass.
- All five live NPC NavMeshAgents (caretaker, Mr Reed, PE coach, photographer, Copycat) were active and on the navigation mesh during editor inspection. No missing scripts in the school scene.
- Cage/Copycat deterministic smoke test passed earlier in this session; this did not cover a complete organic playthrough.
- No developer documents/logs in the upload-folder scan; PackedAssets.tsv also contains no generated performance-test metadata. Steam test App ID and DoNotShip folders excluded.

## Remaining checks

Steam tax verification is pending and no real App ID was available. SteamPipe installation, live leaderboards and Valve's store/build review have not been performed. The standalone startup log still records four initial NavMesh binding warnings and shadow-atlas downscaling; live editor checks show all agents bound afterwards. No complete normal/Dark Mode playthrough or minimum-spec/second-PC test was performed in this audit. Those are the remaining release checks, not grounds to claim full Steam approval or certified hardware support.

The itch page has not been changed and this candidate has not been uploaded. To upload the tested folder:

```powershell
& "$env:LOCALAPPDATA\Programs\butler\butler.exe" push "D:\Confiscated\Builds\Release_2026-10-13_201510\Windows" drowth/confiscated:windows-playtest
```
