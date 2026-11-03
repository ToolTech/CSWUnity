# 4. Map Streamer

This section describes the complete workflow for setting up and running the **CSWUnity Map Streamer** demo after CDW, Visual Studio, and Unity are installed.
![Map Streamer demo workflow](MAP_STREAMER_IMAGES/01_Map_Streamer_Workflow.png)

*Figure: Overall Map Streamer workflow from installation and cloning through build and Unity demo execution.*


## 4.1 Install Git

1. Open the **Company Portal** application in the CDW.
2. Search for and install **Git**.
3. This installs:
   - Git Bash
   - Git CMD

## 4.2 Clone the CSWUnity Repository

1. Open a browser and go to:

   `https://saab.ghe.com`

2. Authenticate using Saab SSO.

3. Open **Git Bash**.

4. Navigate to the directory where you want to clone the repository.

5. Run:

`git clone https://saab.ghe.com/saab/CSWUnity.git`

6. Navigate into the repository:

`cd CSWUnity`

The repository contains files/folders similar to:

    CSWUnity
    │
    ├── Build_x64.bat
    ├── Build_x64_d.bat
    ├── README.md
    ├── projects
    ├── tutorial
    ├── vs17
    ├── com.saab.map-streamer
    └── Saab.Foundation.Map.Manager.Test

## 4.3 Open the Repository in File Explorer

From Git Bash, open the current repository folder in Windows File Explorer:

`explorer .`

Alternatively, navigate manually to the cloned `CSWUnity` folder.

## 4.4 Open the GizmoSDK Solution

In File Explorer, navigate to:

`CSWUnity/vs17/Install_Gizmo/`

Open:

`Install_Gizmo.sln`

The solution opens in Visual Studio.

## 4.5 Build the GizmoSDK Components

Building the solution:

- Downloads the required NuGet packages.
- Builds the required libraries.
- Generates plugin DLLs.
- Deploys the plugin binaries required by Unity.

The available build configurations are:

    Debug | x64
    Release | x64

Recommended usage:

- **Release | x64** — Recommended for running the demo.
- **Debug | x64** — Recommended for development and debugging.
![Visual Studio Release x64 configuration](MAP_STREAMER_IMAGES/02_Visual_Studio_x64_Configuration.png)

*Figure: Example Visual Studio build configuration using Release and x64.*


Use only one configuration at a time.

To build the solution in Visual Studio:

`Build -> Build Solution`

or press:

`Ctrl + Shift + B`
![Visual Studio build solution output](MAP_STREAMER_IMAGES/03_Build_Solution_Output.png)

*Figure: Example successful build output for the Install_Gizmo solution.*


## 4.6 Switching Between Debug and Release

If changing the build configuration from Debug to Release, or from Release to Debug:

1. Close Unity.
2. Run:

`clean.bat`

3. Reopen `Install_Gizmo.sln`.
4. Select the required configuration.
5. Build the solution again.

> Do not mix Debug and Release plugin DLLs.

## 4.7 Build Using Batch Files

The SDK can also be built using the batch files available in the CSWUnity repository.

For a Release build:

`build_x64.bat`

For a Debug build:

`build_x64_d.bat`

Before switching between build configurations, run:

`clean.bat`

## 4.8 Add the Map Streamer Project to Unity Hub

1. Open **Unity Hub**.
2. Select **Projects**.
3. Click **Add Project From Disk**.
4. Select:

`CSWUnity/projects/com.saab.map-streamer`
![Add Map Streamer project from disk](MAP_STREAMER_IMAGES/04_Add_Project_From_Disk.png)

*Figure: Select the com.saab.map-streamer project folder when adding the project in Unity Hub.*

> Ensure that the correct Unity project folder is selected.
![Incorrect Unity project directory example](MAP_STREAMER_IMAGES/05_Incorrect_Project_Directory.png)

*Figure: Example of an incorrectly selected project directory resulting in missing Assets.*


## 4.9 Open the Project in Unity

1. In Unity Hub, click the `com.saab.map-streamer` project.
2. Wait for Unity Editor to finish:
   - Importing assets
   - Compiling scripts
   - Resolving packages

3. In the Unity Project window, open:

`Assets -> Scenes -> ExampleScene`

4. Double-click **ExampleScene** (`Assets/Scenes/ExampleScene.unity`).

## 4.10 Run the Demo

Press the **Play (▶)** button at the top of the Unity Editor.

The streaming map should load in the scene.
![Map Streamer expected output](MAP_STREAMER_IMAGES/06_Map_Streamer_Expected_Output.png)

*Figure: Expected Map Streamer demo output in the Unity Editor.*


## 4.11 Navigation Controls

| Key | Action |
|---|---|
| `W` | Move Forward |
| `A` | Move Left |
| `S` | Move Backward |
| `D` | Move Right |
| `Space` | Move Up |
| `Ctrl` | Move Down |
| `Arrow Keys` | Rotate Camera |
| `Shift` | Increase Movement Speed |

## 4.12 Important Build Note

If the plugin build is changed from **Release to Debug** or from **Debug to Release**:

1. Close Unity.
2. Run `clean.bat`.
3. Rebuild using only the required configuration.
4. Reopen Unity.

This prevents native plugin mismatches and related Unity crashes.
