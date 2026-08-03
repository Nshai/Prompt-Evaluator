# Installer project

Builds an MSI for the AI Prompt Evaluator desktop app.

## Why this is a `.csproj`

`AiPromptEvaluator.Installer.csproj` is a plain SDK-style project, so it loads in
**Visual Studio 2022 and Visual Studio 2026 without the WiX Visual Studio
extension**. (A `.wixproj` needs the HeatWave extension installed before VS can
open it at all.) The project compiles no code — it publishes the app, then calls
the WiX CLI, which is restored as a local dotnet tool from
[`.config/dotnet-tools.json`](../../.config/dotnet-tools.json) at the repository
root. `Package.wxs` holds the actual MSI definition.

## Build

From the repository root:

```powershell
dotnet build src/installer/AiPromptEvaluator.Installer.csproj -c Release
```

The MSI is written to `src/installer/bin/Release/AiPromptEvaluator.msi`.
Building the solution builds it too. No manual `dotnet tool restore` is needed —
the build target runs it.

## What the MSI does

- Installs per-machine into `%ProgramFiles%\AI Prompt Evaluator` (requires elevation)
- Adds a Start Menu shortcut
- Supports major upgrades, so re-running a newer MSI replaces the old install
- Blocks downgrades with a clear error message

## Options

Override these on the command line, e.g. `-p:InstallerSelfContained=true`:

| Property                     | Default   | Purpose                                                       |
| ---------------------------- | --------- | ------------------------------------------------------------- |
| `ProductVersion`             | `1.0.0.0` | MSI `ProductVersion`. Bump it for upgrades to be detected.     |
| `InstallerSelfContained`     | `false`   | `true` bundles the .NET runtime — larger MSI, no prerequisite. |
| `InstallerRuntimeIdentifier` | `win-x64` | Publish RID.                                                   |
| `InstallerArchitecture`      | `x64`     | MSI package architecture. Keep in sync with the RID.           |

## Prerequisite on the target machine

With the default `InstallerSelfContained=false`, the target machine needs the
**.NET 8 Desktop Runtime (x64)**. Build with `-p:InstallerSelfContained=true` to
remove that requirement at the cost of a much larger MSI.
