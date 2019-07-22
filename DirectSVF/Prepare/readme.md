# Overview

This is a preparing project for running the SVF sample, which posts app bundle and activity for SVF sample to Design Automation services by using [.NET SDK](https://github.com/Autodesk-Forge/forge-api-dotnet-design.automation).

# Steps for test run
1. Build the whole solution.
2. (if necessary) Make `Prepare` as active project. Expand it.
3. Open `appsettings.json` and update `ClientId` and `ClientSecret` with real values.

# Usage

The console app can be used in two modes.
## Interaction
Launch the console and follow the instructions. The preparing step is command number "0" which helps to prepare the app bundle and activity for SVF sample to Design Automation. And there are some other commands which you can use in unteraction mode.
## Run selected command
Put number of the wanted command in command line and the console will executed it on start and exist right away. Useful if some repetetive task (like post work item) should be run frequently.