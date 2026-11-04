# REA (Reverse Engineer Anything)

MCP server plus CLI for reverse engineering: native binaries, .NET assemblies, JavaScript/Electron
apps, websites, APKs, firmware and packet captures. MIT licensed, analysis runs locally.
Repo: https://github.com/morluto/rea | npm: `rea-agents` (6.8.0 as of 2026-11-04).

It is for inspecting things we do not have source for. It does nothing for authoring CONFISCATED!
from source; see "Where it actually helps us" below.

## Install (Windows dev box)

Needs Node 22.23.4+, 24.22.1+ or 26.12.1+. Run from anywhere:

    npx rea-agents setup

That registers the MCP server with Claude Code (writes `~/.claude.json`) and installs the matching
workflow skill files. Restart Claude Code afterwards, then verify:

    npx rea-agents doctor

Optional global CLI, for terminal use without the agent:

    npm install --global rea-agents
    rea --help
    rea update

Do not hand-write an `.mcp.json` for this. `setup` pins the skill version to the CLI version and
`doctor` reports drift between them; a hand-rolled registration bypasses that check.

## Analysis engines

- **No engine needed:** static JavaScript/Electron, managed .NET (PE/CLI) inspection, web bundle
  capture, APK manifest/resource decode, firmware and file-format inspection.
- **Native code needs one of:** Ghidra 12.1.x (`GHIDRA_INSTALL_DIR`), IDA (`REA_IDA_MCP_CONFIG`),
  Hopper (`HOPPER_LAUNCHER_PATH`, macOS) or Kuna (`REA_KUNA_COMMAND`). REA does not download them.
- **Android dex decompile needs:** `REA_JADX_MCP_JAR` pointing at a jadx-headless-mcp 0.7.1 jar.

`doctor` lists each engine's state and the exact env var to set.

## Where it actually helps us

1. **Release validation of our own build.** Point the managed tools at
   `Confiscated_Data/Managed/Assembly-CSharp.dll` in a Mono player to confirm what actually shipped
   (stripping, leftover debug/editor-only members, P/Invoke surface). `compare-managed-members`
   diffs two builds' declared members by exact type, name and signature, which suits the
   `Docs/ReleaseValidation-*.md` pass.
2. **Store/launcher integration checks.** Inspect the Steam redistributables and any native plugin
   we ship for declared imports and layout without running them.
3. **Studying another game's implementation** when no source is available.

Caveats:
- An IL2CPP player's `GameAssembly.dll` is native code, so it needs Ghidra or IDA, and REA does not
  advertise IL2CPP metadata reconstruction. For that specific job Il2CppDumper is still the better
  tool. (Inference: based on the command list, not tested.)
- Cloud Claude Code sessions have no analysis engines installed and no build artefacts in the repo,
  so run REA locally.
- Commercial engines (IDA Pro, Hopper, JEB) must be legally licensed; REA only drives them.

## Example terminal calls

    rea doctor
    rea inspect-artifact "D:\Builds\Confiscated\Confiscated_Data\Managed\Assembly-CSharp.dll"
    rea project-managed-application-graph --help
    rea analyze-javascript-application /absolute/path/to/extracted/app --json

Global flags worth knowing: `--format toon|json|yaml|md|jsonl`, `--token-limit <n>`,
`--filter-output <keys>`, `--schema`, `--llms-full`.
