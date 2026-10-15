//******************************************************************************
//
// Copyright (C) SAAB AB
//
// All rights, including the copyright, to the computer program(s) 
// herein belong to Saab AB. The program(s) may be used and/or
// copied only with the written permission of Saab AB, or in
// accordance with the terms and conditions stipulated in the
// agreement/contract under which the program(s) have been
// supplied. 
//
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
//
// File			: Initializer.cs
// Module		:
// Description	: Bindings stub for Gizmo Messages
// Author		: Anders Modén
// Product		: GizmoBase 2.12.326
//
// NOTE:	Gizmo3D is a high performance 3D Scene Graph and effect visualisation 
//			C++ toolkit for Linux, Mac OS X, Windows, Android, iOS and HoloLens for  
//			usage in Game or VisSim development.
//
//
// Revision History...
//
// Who	Date	Description
//
// AMO	180607	Created file        (2.9.1)
//
//******************************************************************************

// ----------- Some defines ---------------------------------

//#define SHOW_MEMORY         // Show memory used in PlotViz
//#define SHOW_FPS            // Show FPS in PlotViz
//#define SHOW_TRACERS        // Show perf in PlotViz

// ------------------------ Code ----------------------------

using GizmoSDK.GizmoBase;
using UnityEngine;
using Saab.Foundation.Unity.MapStreamer;


namespace Saab.Unity.Initializer
{
    public class Initializer : MonoBehaviour
    {
        //private DebugCommandStation station=null;

        private PerformanceTracer tracer;

#if UNITY_ANDROID

        private AndroidJavaObject multicastLock;

#endif //UNITY_ANDROID

        void SetupJavaBindings()
        {
#if UNITY_ANDROID

            AndroidJavaClass playerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");

            if (playerClass == null)
                return;

            AndroidJavaObject activity = playerClass.GetStatic<AndroidJavaObject>("currentActivity");

            if (activity == null)
                return;

            AndroidJavaObject assetManager = activity.Call<AndroidJavaObject>("getAssets");

            if (assetManager == null)
                return;

            Message.Send("GizmoSDK", MessageLevel.DEBUG, $"assetManager {assetManager}");

            SerializeAdapter.SetAssetManagerHandle(System.IntPtr.Zero, assetManager.GetRawObject());
#endif //UNITY_ANDROID
        }

        void EnableMulticastState()
        {
#if UNITY_ANDROID

            AndroidJavaObject activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");

            var wifiManager = activity.Call<AndroidJavaObject>("getSystemService", "wifi");
                           
            multicastLock = wifiManager.Call<AndroidJavaObject>("createMulticastLock", "lock");

            multicastLock.Call("setReferenceCounted", true);

            multicastLock.Call("acquire");

            Message.Send(Message.GIZMOSDK, MessageLevel.DEBUG, "MultiCast Lock acquired");

            //station = new DebugCommandStation("udp::45456?nic=${wlan0}&blocking=no");

            //Thread thread = new Thread(new ThreadStart(WorkThreadFunction));
            //thread.Start();

#endif //UNITY_ANDROID
        }

        //private void WorkThreadFunction()
        //{
        //    while (station != null && station.Exec())
        //        System.Threading.Thread.Sleep(10);
        //}
                

        private void Awake()
        {


            GizmoSDK.GizmoBase.Platform.Initialize();
              
            Message.OnMessage += Message_OnMessage;

            // Disable screen dimming
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

#if SHOW_MEMORY
            // Set to tru to enable memory tracing. Heavy load
            MemoryControl.DebugMem(true);   // Enable trace of allocated memory
#endif

#if UNITY_ANDROID
            GizmoSDK.GizmoBase.Monitor.InstallMonitor("udp::45454?nic=${wlan0}");

            // Initialize application ragistry
            KeyDatabase.SetDefaultRegistry($"/data/data/{Application.identifier}/files/gizmosdk.reg");
#else
            GizmoSDK.GizmoBase.Monitor.InstallMonitor();
#endif //UNITY_ANDROID

            Message.SetMessageLevel(MessageLevel.PERF_DEBUG);

            
            // Set local xml config
            KeyDatabase.SetLocalRegistry("config.xml");

            // Enable multicast state listener
            EnableMulticastState();
                        

#region -------- Test Related stuff in init --------------------

            //SetupJavaBindings();

            //test();

#endregion

            // Set up scene manager camera

            SceneManager scenemanager = FindFirstObjectByType<SceneManager>();
            CameraControlBase cameracontrol = FindFirstObjectByType<CameraControlBase>();

            scenemanager.MapUrl = KeyDatabase.GetDefaultUserKey("SceneManager/MapUrl", scenemanager.MapUrl);

            //scenemanager.MapUrl()

            scenemanager.SceneManagerCamera = cameracontrol;

        }

        private void Message_OnMessage(string sender, MessageLevel level, string message)
        {
            // Just to route some messages from Gizmo to managed unity

            switch (level & MessageLevel.LEVEL_MASK)
            {
                case MessageLevel.MEM_DEBUG:
                case MessageLevel.PERF_DEBUG:
                case MessageLevel.DEBUG:
                case MessageLevel.TRACE_DEBUG:
                    Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, message);
                    break;

                case MessageLevel.NOTICE:
                    Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, message);
                    break;

                case MessageLevel.WARNING:
                    Debug.LogWarning(message);
                    break;

                case MessageLevel.FATAL:
                    Debug.LogError(message);
                    break;

                case MessageLevel.ASSERT:
                    Debug.LogAssertion(message);
                    break;

                case MessageLevel.ALWAYS:
                    Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, message);
                    break;
            }
        }

        private int _counter = 0;

        private PerformanceTracer _tracer;

        private double _frameDurationTime = 0;

        private double _frameTime = 0;

        private void Update()
        {

            double time = GizmoSDK.GizmoBase.Time.SystemSeconds;

            if (_frameTime>0)
                _frameDurationTime = 0.999 * _frameDurationTime + 0.001 * (time - _frameTime);

            _frameTime = time;


            // Example of getting performance graphical output

#if SHOW_TRACERS

            if (_counter == 10)
            {
                tracer = new PerformanceTracer();

                tracer.AddAll();

                tracer.Run();
            }

#endif // SHOW_TRACERS


            _counter++;

            // Exemple of getting allocate dmemory in native parts

#if SHOW_MEMORY
            if (_counter % 30 == 0)
            {
                //System.GC.Collect();
                //System.GC.WaitForPendingFinalizers();

                GizmoSDK.GizmoBase.Monitor.AddValue("mem", MemoryControl.GetAllocMem());

                GizmoSDK.GizmoBase.Monitor.AddValue("internal", MemoryControl.GetAllocMem(0, 0, false, true));

                GizmoSDK.GizmoBase.Monitor.AddValue("dyn", MemoryControl.GetAllocMem(66666));

                GizmoSDK.GizmoBase.Monitor.AddValue("tex", Image.GetRegisteredImageData());
            }
#endif //SHOW_MEMORY

#if SHOW_FPS
            if (_counter % 30 == 0)
            {
                GizmoSDK.GizmoBase.Monitor.AddValue("fps", 1 / _frameDurationTime);
            }
#endif //SHOW_FPS

        }
    }

}
