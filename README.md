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

To run, open the Install_Gizmo.sln under directory 'vs17\Install_Gizmo' and select either Debug or Release for x64. Select only one configuration at a time. This will fetch NuGet packages for all components and update the Assets/Plugin folder with the correct binaries. The solution can deploy both Debug and Release versions for development, but only one deployed version should be used at a time. Do not mix Debug and Release versions. To clear the plugin folder, run 'Clean.bat'.

The maintained Install_Gizmo solution uses Visual Studio 2022 format and can also be opened by a later compatible Visual Studio version. Install .NET Core 3.1 support where it is required by the build projects. Platform-specific builds, such as ARM64 targets, require their corresponding supported solution and package configuration and must not be mixed with x64 binaries in the same Unity plugin deployment.

You could also simply just run the corresponding .bat script (build_x64, build_x64_d, etc..) and every thing will be setup correctly.


CDW installation guides
=======================

The following guides apply to users who run CSWUnity from a Cloud Development
Workstation (CDW). They describe the complete CDW setup and Map Streamer demo
workflow:

1. [CDW installation and system requirements](tutorial/howto/CDW_Installation.md)
2. [Visual Studio installation](tutorial/howto/CDW_Visual_Studio_Installation.md)
3. [Unity installation](tutorial/howto/CDW_Unity_Installation.md)
4. [Map Streamer demo](tutorial/howto/CDW_map_streamer_DEMO.md)


Running the demo
================
Open the unity project found under the under directory 'projects\com.saab.map-streamer' and Press the "play" button in the Unity editor. 

The T&S BTA reference architecture in this repository uses Unity 2021.3.17f1. Other Saab projects can have different Unity-version requirements and should follow their own ProjectSettings/ProjectVersion.txt file and approved dependency set. New projects may prefer Unity 6000.3.21f1 LTS after compatibility with the required CSWUnity, GizmoSDK and package dependencies has been verified.

<b>keybinds:</b>
* WASD to move around
* space, ctrl move up and down
* Arrow keys to rotate the view
* shift increase speed of movement

<b><u>Note Win64 only right now</u></b>
Good Luck !


Technology Info
===============

The demo is based on a 3D scenegraph written in native C++ that manages the logistics for loading/unloading data, LOD levels and transitions between LOD depending on what data is currently loaded. It uses up to 16 parallel threads to load data from multiple URL based datasources dynamically.
The demo uses a large double precision coordinate system and a ROI (Region Of Interest) subsystem that translates HUGE coordinates into local islands of single precision data. 

The system handles geocentric coordinate systems as well as flat UTM and other conic projections and provides a uniform WGS84 API to control all objects and queries.

The SceneGraph API also allows a fast intersector query to be performed to find ground features and clamp object to the ground.

The demo shows an example of SBD maps (Streaming Binary Data) that are quad or octree based spatial data in 3D. The format allows very large databases (entire globe) to be divided on multiple servers and that can have details down to (mm) in resolution.

![Screenshot](https://gizmosdk.blob.core.windows.net/maps/stock/thumb.png)
_[A screenshot showing how feature/height data can be used to present trees with accurate position and height]_  