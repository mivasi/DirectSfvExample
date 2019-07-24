# Prerequisites
* Inventor 2020 to build Inventor addin locally.

# Build steps
1. Clone the solution.
1. Open `DirectSVF.sln` in Visual Studio.
1. Open `Viewer\appsettings.user.json`.
1. Create an app at https://forge.autodesk.com.\
It should have access to **Design Automation API** and **Misc API**.
1. Copy **Client ID** and **Client Secret** to corresponding fields in `appsettings.user.json`.
1. Build the solution.
1. Set `Prepare` project active and run it. It will publish app bundle and activity.
1. Set `Viewer` project active and start it.\
=> It should open browser page.
1. Click **Browse...** to select IPT file.
1. Click **Show** to process the IPT file and view generated SVF file.
