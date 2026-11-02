Streaming Map Demo
==================

<B>A Saab Dynamics demo of the Streaming Binary Data Architecture</B>

This is a generic streaming API for 3D engines (Unity,Unreal...) that allows fast streaming updates of huge 3D datasets located in cloud, from disc or procedural.

This GIT repository will allow you to take a look at a Unity demo, written by the Open Saab Development community using the GizmoSDK code base from Saab Dynamics, Training & Simulation. Take a look at https://youtu.be/m2NsE8NBrB0

The open source and open documentation in here are to be considered as LGPL code and can be used in your own projects.

The binaries are licensed by Saab Dynamics. If you are interested in using this for commercial purposes, please contact 

anders.moden@saabgroup.com




Setup
=====

To run, open the Install_Gizmo.sln under directory 'vs17\Install_Gizmo' and select 'debug/release for each x64/ARM64 etc' solution and build. Select only one configration at a time and build this for all platforms. This will fetch nuget packages for all components and update the Assets/Plugin folder with correct binaries. The solution can deploy both Debug and Release versions to be used in development. Only one "deploy" version at a time should be used. Dont mix both Debug and Release versions. To clear the plugin folder, run the script 'cleanup_plugins.bat' that will clean the Assets/Plugin folder. Use VS2019 to build the InstallPackages or use VS2017 and install support for .NET Core 3.1 
Dont use ARM and ARM64 builds in parallell or rename and configure dlls properly so Unity can select the right ones.

You could also simply just run the corresponding .bat script (build_x64, build_x64_d, etc..) and every thing will be setup correctly.


Running the demo
================
Open the unity project found under the under directory 'projects\com.saab.map-streamer' and Press the "play" button in the Unity editor. 

<b>keybinds:</b>
* WASD to move around
* space, ctrl move up and down
* Arrow keys to rotate the view
* shift increase speed of movement

<b><u>Note Win64 only right now</u></b>
Good Luck !

## Other Demos

To Integrate weather effects like rain, snow, clouds, lightning, and fog in Unity, refer common component [CSWEnvironment](https://saab.ghe.com/saab/CSWEnvironment).

For Example Demos of **drone navigation**, **tanks and vehicles**, **ballistics**, or **drone-to-drone communication** **etc**, refer [CSWExamples](https://saab.ghe.com/saab/CSWExamples).


Technology Info
===============

The demo is based on a 3D scenegraph written in native C++ that manages the logistics for loading/unloading data, LOD levels and transitions between LOD depending on what data is currently loaded. It uses up to 16 parallel threads to load data from multiple URL based datasources dynamically.
The demo uses a large double precision coordinate system and a ROI (Region Of Interest) subsystem that translates HUGE coordinates into local islands of single precision data. 

The system handles geocentric coordinate systems as well as flat UTM and other conic projections and provides a uniform WGS84 API to control all objects and queries.

The SceneGraph API also allows a fast intersector query to be performed to find ground features and clamp object to the ground.

The demo shows an example of SBD maps (Streaming Binary Data) that are quad or octree based spatial data in 3D. The format allows very large databases (entire globe) to be divided on multiple servers and that can have details down to (mm) in resolution.

![Screenshot](https://gizmosdk.blob.core.windows.net/maps/stock/thumb.png)

_[A screenshot showing how feature/height data can be used to present trees with accurate position and height]_  
