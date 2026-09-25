# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Before changing gameplay, read `../Docs/CLAUDE_PROJECT_BRIEF.md`. It defines the intended player experience, game loop, progression, chase behaviour, art and audio direction and product constraints. Treat it as requirements; inspect the code and `Assets/Scenes/SchoolLayout.unity` for implementation detail. Each feature also has a short note in `../Docs/*.md` (e.g. `SchoolRun.md` for the loop and the item route).

## Project

CONFISCATED! is a short first-person comedy-horror chase game (British primary school, caretaker antagonist). Unity 6000.5.5f1 (URP, new Input System, AI Navigation). The Unity project is this folder; `../Docs/`, `../Backups/` and the art sources live in the parent. **It is not a git repo**: before scene-changing or multi-file work, copy the files to `../Backups/<yyyy-mm-dd>_<topic>/`.

Main scene: `Assets/Scenes/SchoolLayout.unity`. Loop: classroom phone confiscation -> newsletter errand -> recover 5 belongings -> leave by the main entrance; a caretaker catch ends the run. `VisualTest.unity` is the old office chapter.

## Build, run and test

There is no CLI build or test runner. Everything is done from the Unity editor menus under `Confiscated/...` (about 140 editor scripts in `Assets/Editor/Confiscated/`), usually driven through the Unity MCP tools.

- **The scene is generated, not hand-edited.** `SchoolLayoutBuilder` builds the school from `SchoolPlan.cs` (plan pixels, 9 px = 1 m); `SchoolRunSetup.Build` (menu `School Run/Build Chase Loop and Dress School`) then adds the run and chains the other installers. Extend a builder rather than editing the scene by hand. **An installer that is not chained into `SchoolRunSetup.Build` silently vanishes on the next rebuild**, so chain every new one in and give it a presence check in a smoke test.
- **Tests are armed, then played.** An `Arm ... Test` menu writes a marker file under `Temp/`; on the next Play an `[InitializeOnLoad]` test runs, writes a report (usually under `../Docs/`, e.g. `Docs/SchoolRun/Validation.txt`) and exits Play. Reports are overwritten in place, so compare the file's mtime with when you armed it before trusting a trailing PASS/FAIL. `School Run/Arm Integration Test` (`SchoolRunSmokeTest`) is the main regression signal; it warps the player between checkpoints, so it is an audit and not a human playthrough. Give each new feature its own armed test.
- **Check `Unity_ManageEditor GetState` and confirm `IsPlaying: false` before any script edit or `Assets/Refresh`.** The user playtests in the editor between messages, and a recompile during Play wipes statics (`GameManager.Instance` goes null) and ruins their session. After editing, refresh with the `Assets/Refresh` menu item, then wait until `Library/ScriptAssemblies/Assembly-CSharp.dll` is newer than the edited script and check the console. Other bridge quirks: every tool parameter is required (pass empty/0), the `RunCommand` relay dies after 180 s idle, and entering Play can stall bridge calls.

## Runtime architecture

- **Singletons** (`GameManager`, `SchoolRunController`, `HudController`, `ComicDialogue`) hold the state; scripts check `Instance != null` and `IsPlaying` before acting. `SchoolPeriodController` runs the opening. `QuickOpening` is true whenever a `SchoolRunController` exists, so in the main scene the worksheet branches never run: only the phone incident, confiscation and the errand instruction do.
- **Noise is pub/sub.** `NoiseEvents.Emit(pos, radius, source)` is heard by `CaretakerAI` (which drops into Investigate unless chasing), the chatterbox (source `"clockwork toy"`) and the sound-ring UI. Add new attention-getters by emitting a new source string.
- **Caretaker** (`CaretakerAI`): Patrol / Investigate / Chase / Search / Frozen with cone + linecast sight. `CaretakerPassCheck` is only active in the errand phase, before `RoundStarted`; once the round starts he chases on sight. Other NPCs are simple single-rule components (`ChatterboxStudent`, `DinnerTrolleyPatrol`).
- **Dialogue.** `HudController.SetStatus("Speaker: text")` routes to the modal `ComicDialogue` balloon (timeScale 0) except for chase barks; otherwise it is plain HUD text.
- **Audio lines.** `ComicDialogue.recordedLines` maps the exact written line to a clip in `Resources/Audio`; the text must match the spoken string character for character, so change both together. The caretaker has his own 3D voice channel, `CaretakerAI.Say(clip, priority)`. Recorded voices are mono mp3 (Unity cannot import `.m4a`); lines without a clip fall back to placeholder babble. Runtime audio sources and similar helpers are created lazily in code so they survive scene rebuilds.
- **Art.** Textures in `Assets/Art/Textures/T_*.png` keep their GUIDs: new art overwrites the PNG only. The user supplies generated art (requests are handed over as lists like `../Docs/ASSET_REQUESTS_Codex_Batch3.md`) and 3D pickups from Tripo (see `PickupModelSetup` for the FBX quirks). Corridor walls are URP Lit with world-space UVs.

## Conventions

- Match the existing compact style (short single-line members, terse names) and comment density.
- The user often queues several requests while a long turn runs: keep a visible list and finish each.
