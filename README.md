# AI Prompt Evaluator

AI Prompt Evaluator is a Windows Forms desktop utility for sending prompts to
Anthropic models, optionally enriching the prompt with local document context,
and showing the response alongside a **per-component cost breakdown** on the main
screen.

## Features

- Prompt input and response display in a Windows Forms interface
- **Cost breakdown on the main screen** — input, cache write, cache read, and
  output tokens each shown with their token count, per-million rate, and dollar
  cost, plus a running total
- Re-prices the last response instantly when you switch models
- Optional local document folder ingestion for prompt context
- Model selection, configurable API key, API URL format, and max output tokens
- Configuration dialog with persistent app settings
- xUnit tests for the pricing/cost logic and document context builder
- MSI installer that opens in Visual Studio 2022 and 2026

## Cost breakdown

The four billed token categories are priced separately, using each model's
published rates:

| Component        | Rate                 |
| ---------------- | -------------------- |
| Input (uncached) | model input rate     |
| Cache write      | 1.25x the input rate |
| Cache read       | 0.1x the input rate  |
| Output           | model output rate    |

Rates live in [ModelPricing.cs](src/AiPromptEvaluator/ModelPricing.cs) and cover
the current Claude models. A model that is not in the table still gets an
estimate (Opus-tier rates), and the UI labels it as estimated rather than
implying it is exact.

## Project Structure

- [src/AiPromptEvaluator/](src/AiPromptEvaluator/) — Windows Forms application
- [src/installer/](src/installer/) — MSI installer project
- [tests/AiPromptEvaluator.Tests/](tests/AiPromptEvaluator.Tests/) — xUnit tests
- `AiPromptEvaluator.slnx` — solution file

## Requirements

- Windows
- .NET 8 SDK
- Visual Studio 2022 or 2026 with the .NET desktop development workload, or VS Code

## Getting Started

1. Clone the repository.
2. Build the solution:
   ```powershell
   dotnet build AiPromptEvaluator.slnx -c Release
   ```
3. Run the app:
   ```powershell
   dotnet run --project src/AiPromptEvaluator/AiPromptEvaluator.csproj
   ```
4. Open **Configuration...** and enter your Anthropic API key before running a prompt.

## Configuration

Settings are stored in the user profile under
`%LOCALAPPDATA%\AiPromptEvaluator\settings.json`:

- Anthropic API key
- API URL format (leave empty for the default; `{0}` is the API version, `{1}` the endpoint)
- Available models (comma-separated) and the selected model
- Max output tokens
- Document context folder
- Clarification prompt behavior

## Testing

```powershell
dotnet test tests/AiPromptEvaluator.Tests/AiPromptEvaluator.Tests.csproj -c Release
```

## Installer

```powershell
dotnet build src/installer/AiPromptEvaluator.Installer.csproj -c Release
```

Produces `src/installer/bin/Release/AiPromptEvaluator.msi`. See
[the installer README](src/installer/README.md) for options and the runtime
prerequisite.
