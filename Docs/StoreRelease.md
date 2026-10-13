# Windows store release

Use **Confiscated > Build > Windows Store Candidate** in the Unity editor, with Play stopped and the school scene saved. The school is hand-authored: never regenerate it to make a build.

The build uses lossless LZ4HC compression, includes StudioSplash followed by SchoolLayout, and writes a new timestamped directory under `../Builds/Release_*`. Artwork, texture resolution, voices and gameplay content are unchanged. The unused Unity AI Inference dependency has been removed; the game does not run inference models.

Editor tooling remains installed. Release housekeeping excludes generated performance-test metadata and checks the upload folder for Claude/Codex documents, agent/skill files, prompts, source files and logs. Unity AI Navigation and UnityEngine.AIModule are conventional NPC pathfinding dependencies and must remain. Package housekeeping does not change the provenance of artwork or voices, or the storefront AI disclosure.

Upload only the `Windows` directory. Build reports and `DoNotShip` symbols stay alongside it, outside the player folder. Keep the EXE, data folder, UnityPlayer and other generated runtime files together. Do not manually strip DLLs or resource files. `steam_appid.txt` must not be included in the shipped folder.

For a manual-upload ZIP, run `Tools/PackageStoreBuild.ps1 -BuildRoot <release directory>` from PowerShell. It validates the layout, excludes development files by rejecting contaminated folders, compresses the player and reads every ZIP entry back to verify its SHA-256 against the source. The ZIP, checksum and validation report are placed outside `Windows`.

## itch.io

The existing destination is `drowth/confiscated:windows-playtest`. Once the candidate has passed testing, use PowerShell:

```powershell
& "$env:LOCALAPPDATA\Programs\butler\butler.exe" push "D:\Confiscated\Builds\Release_YYYY-MM-DD_HHMMSS\Windows" drowth/confiscated:windows-playtest
```

Replace the timestamp with the tested build. Butler accepts the whole portable folder; no installer is required. Keep the file marked Windows. Test a freshly downloaded and extracted copy before directing creators to it.

## Steam — pending account approval

As confirmed on 13 October 2026, tax verification is pending and no real App ID has been supplied. A local Windows build does not establish Steam release readiness.

After approval, create/configure the application and depot, upload this Windows folder with SteamPipe, and set the Windows launch executable to `Confiscated.exe`. Test installing and launching through Steam with the actual App ID. Verify leaderboard submission/retrieval on a private test branch before advertising Steam leaderboards. The project-root `steam_appid.txt` is the development value 480; never copy it into a public depot.

Submit the store page and build for Valve review. Avoid claiming controller support, verified minimum hardware, Steam achievements or full spoken audio until each has actually been checked. Finish an ordinary and Dark Mode run from the downloaded build on another Windows PC before the full release.

References: [itch.io folder uploads](https://itch.io/docs/butler/pushing.html), [Steam review process](https://partner.steamgames.com/doc/store/review_process), [Steam releasing](https://partner.steamgames.com/doc/store/releasing).
