# AI Prompt Evaluator

AI Prompt Evaluator is a Windows desktop utility for sending prompts to Anthropic models, optionally enriching the prompt with local document context, and displaying the response and estimated usage cost in the UI.

## Features

- Prompt input and response display in a WPF interface
- Optional local document folder ingestion for prompt context
- Anthropic model selection and configurable base URL / API key
- Configuration dialog for persistent app settings
- Estimated cost display while using the model
- Test project for core document context behavior
- Installer scaffold for packaging the app

## Project Structure

- src/AiPromptEvaluator - WPF application
- src/installer - WiX-based installer project
- tests/AiPromptEvaluator.Tests - xUnit test project
- AiPromptEvaluator.slnx - solution file for the repository

## Requirements

- Windows operating system
- .NET 8 SDK
- Visual Studio 2022 or VS Code with the C#/.NET workload

## Getting Started

1. Clone the repository.
2. Restore NuGet packages:
   ```powershell
   dotnet restore
   ```
3. Build the solution:
   ```powershell
   dotnet build AiPromptEvaluator.slnx -c Release
   ```
4. Run the app from the build output folder or from Visual Studio.

## Configuration

The app stores its configuration locally in the user profile under AppData. You can configure:

- Anthropic API key
- Base URL
- Available models
- Selected model
- Document folder
- Clarification prompt behavior

## Testing

Run the test suite with:

```powershell
 dotnet test tests/AiPromptEvaluator.Tests/AiPromptEvaluator.Tests.csproj -c Release
```

## Notes

The application targets .NET 8 for Windows and uses the Anthropic SDK for model interaction.
