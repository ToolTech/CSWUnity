/*
 * Copyright (C) SAAB AB
 *
 * All rights, including the copyright, to the computer program(s)
 * herein belong to Saab AB. The program(s) may be used and/or
 * copied only with the written permission of Saab AB, or in
 * accordance with the terms and conditions stipulated in the
 * agreement/contract under which the program(s) have been
 * supplied.
 *
 * Information Class:          COMPANY RESTRICTED
 * Defence Secrecy:            UNCLASSIFIED
 * Export Control:             NOT EXPORT CONTROLLED
 */

using System;
using System.Globalization;
using System.IO;
using System.Text;
using Saab.Foundation.Map;
using Saab.Foundation.Unity.MapStreamer;
using Saab.Foundation.Unity.MapStreamer.GeodeticCamera;
using Saab.Unity.Extensions;
using UnityEngine;
using LatPos = GizmoSDK.Coordinate.LatPos;
using GizmoMatrix3 = GizmoSDK.GizmoBase.Matrix3;
using GizmoVector3 = GizmoSDK.GizmoBase.Vec3;
using GizmoVector3D = GizmoSDK.GizmoBase.Vec3D;

namespace Saab.MapStreamer.Benchmark
{
public sealed class MeshStreamingBenchmark : MonoBehaviour
{
    private enum RunState
    {
        Idle,
        WarmUp,
        Measuring,
        Complete,
        Error,
    }

    private enum CameraProgressionMode
    {
        Realtime,
        FixedSamplesPerLap,
    }

    private const float StandardEastRadiusMeters = 1200.0f;
    private const float StandardNorthRadiusMeters = 800.0f;
    private const float LookDownDegrees = 28.0f;
    private const float BankDegrees = 8.0f;
    private const int MaximumFrameSamples = 32768;
    private const int RouteLengthSegments = 2048;
    private const double MinimumExtentMeters = 10.0;
    private const string RunIdentity = "New";
    private const string ReportDirectoryName = "MapStreamerBenchmarks";

    [Header("Tests")]
    [SerializeField]
    [Tooltip("Ordered tests run once per iteration. Test references may be repeated.")]
    private BenchmarkTest[] _tests;

    [SerializeField, Min(1)]
    [Tooltip("Number of times to execute the complete ordered test sequence.")]
    private int _testIterations = 1;

    [SerializeField]
    [Tooltip("Reset and reload the map before each test.")]
    private bool _reloadMapBetweenTests;

    [Header("Scenario")]
    [SerializeField]
    private CameraProgressionMode _progressionMode =
        CameraProgressionMode.Realtime;

    [SerializeField, Min(0.1f)]
    [Tooltip("Seconds per complete figure-eight lap. Lower values mean higher speed.")]
    private float _lapDurationSeconds = 15.0f;

    [SerializeField, Min(1)]
    [Tooltip("Camera poses per lap in FixedSamplesPerLap mode. One pose is applied per frame.")]
    private int _stepsPerLap = 4096;

    [SerializeField, Min(1)]
    private int _measuredLaps = 5;

    [SerializeField]
    private bool _saveDiagnosticsToFile = true;

    [Header("Route")]
    [SerializeField, Min(1.0f)]
    private float _altitudeMeters = 100.0f;

    [SerializeField, Range(0.0f, 0.45f)]
    private float _mapMargin = 0.15f;

    private sealed class FrameStatistics
    {
        private readonly float[] _samples = new float[MaximumFrameSamples];
        private readonly float[] _sortedSamples = new float[MaximumFrameSamples];
        private int _count;
        private double _sumMilliseconds;

        public int Count => _count;
        public double AverageMilliseconds { get; private set; }
        public double P95Milliseconds { get; private set; }
        public double P99Milliseconds { get; private set; }
        public double WorstMilliseconds { get; private set; }
        public double AverageFps =>
            AverageMilliseconds > 0.0 ? 1000.0 / AverageMilliseconds : 0.0;

        public void Reset()
        {
            _count = 0;
            _sumMilliseconds = 0.0;
            AverageMilliseconds = 0.0;
            P95Milliseconds = 0.0;
            P99Milliseconds = 0.0;
            WorstMilliseconds = 0.0;
        }

        public void Sample(float frameDurationSeconds)
        {
            if (_count >= _samples.Length)
                return;

            var milliseconds = frameDurationSeconds * 1000.0f;
            _samples[_count++] = milliseconds;
            _sumMilliseconds += milliseconds;
        }

        public void Complete()
        {
            if (_count == 0)
                return;

            Array.Copy(_samples, _sortedSamples, _count);
            Array.Sort(_sortedSamples, 0, _count);
            AverageMilliseconds = _sumMilliseconds / _count;
            P95Milliseconds = Percentile(0.95);
            P99Milliseconds = Percentile(0.99);
            WorstMilliseconds = _sortedSamples[_count - 1];
        }

        private double Percentile(double percentile)
        {
            var index = (int)Math.Ceiling(percentile * _count) - 1;
            return _sortedSamples[Math.Max(0, Math.Min(index, _count - 1))];
        }
    }

    private sealed class RunResult
    {
        public readonly FrameStatistics Frames = new FrameStatistics();
        public MeshBuildDiagnostics Meshes;
        public TextureBuildDiagnostics Textures;
        public int CompletedLaps;
        public bool CleanMapReload;
        public string ValidityWarning;
        public string MapUrl;
        public double RouteScale;
        public double RouteLengthMeters;
        public double EastRadiusMeters;
        public double NorthRadiusMeters;
        public double MinimumBuildBudgetMilliseconds;
        public double MaximumBuildBudgetMilliseconds;
        public DateTime RunStartUtc;
        public DateTime RunStartLocal;
        public double TotalDurationSeconds;
        public double MeasuredDurationSeconds;
        public BenchmarkTestReport Test;

        public void Reset()
        {
            Meshes = default;
            Textures = default;
            CompletedLaps = 0;
            CleanMapReload = false;
            ValidityWarning = null;
            MapUrl = null;
            RouteScale = 0.0;
            RouteLengthMeters = 0.0;
            EastRadiusMeters = 0.0;
            NorthRadiusMeters = 0.0;
            MinimumBuildBudgetMilliseconds = 0.0;
            MaximumBuildBudgetMilliseconds = 0.0;
            RunStartUtc = default;
            RunStartLocal = default;
            TotalDurationSeconds = 0.0;
            MeasuredDurationSeconds = 0.0;
            Test = null;
            Frames.Reset();
        }
    }

    [Serializable]
    private sealed class BenchmarkReport
    {
        public int schemaVersion;
        public string runIdentity;
        public string runId;
        public string startedUtc;
        public string startedLocal;
        public string completedUtc;
        public string completedLocal;
        public SuiteReport suite;
        public BenchmarkTestReport test;
        public RuntimeReport runtime;
        public ScenarioReport scenario;
        public FrameReport frames;
        public BuildReport meshes;
        public BuildReport textures;
        public TextureManagerReport textureManager;
    }

    [Serializable]
    private sealed class SuiteReport
    {
        public string suiteRunId;
        public int iteration;
        public int iterationCount;
        public int testIndex;
        public int testsPerIteration;
        public int runIndex;
        public int totalRuns;
    }

    [Serializable]
    private sealed class RuntimeReport
    {
        public string unityVersion;
        public string applicationVersion;
        public string buildGuid;
        public string platform;
        public string operatingSystem;
        public string processor;
        public string graphicsDevice;
        public string dotNetRuntime;
        public string quality;
        public int qualityLevel;
        public int vSyncCount;
        public string fullScreenMode;
        public int screenWidth;
        public int screenHeight;
    }

    [Serializable]
    private sealed class ScenarioReport
    {
        public string mapUrl;
        public bool cleanMapReload;
        public string validityWarning;
        public string cameraProgressionMode;
        public string routeParameterization;
        public int warmUpLaps;
        public int measuredLaps;
        public double lapDurationSeconds;
        public int stepsPerLap;
        public double cameraSampleDistanceMeters;
        public int measuredCameraSamples;
        public double totalDurationSeconds;
        public double measuredDurationSeconds;
        public double cameraSamplesPerSecond;
        public double altitudeMeters;
        public double eastRadiusMeters;
        public double northRadiusMeters;
        public double routeScale;
        public double routeLengthMeters;
        public double mapMargin;
        public double lookDownDegrees;
        public double bankDegrees;
        public double minimumBuildBudgetMilliseconds;
        public double maximumBuildBudgetMilliseconds;
    }

    [Serializable]
    private sealed class FrameReport
    {
        public int count;
        public double averageMilliseconds;
        public double p95Milliseconds;
        public double p99Milliseconds;
        public double worstMilliseconds;
        public double averageFps;
    }

    [Serializable]
    private sealed class BuildReport
    {
        public long count;
        public long payloadBytes;
        public double totalBuildMilliseconds;
        public double averageBuildMilliseconds;
        public double worstBuildMilliseconds;
        public double itemsPerSecond;
        public double mebibytesPerSecond;
    }

    [Serializable]
    private sealed class TextureManagerReport
    {
        public int activeTextures;
        public long activeReferences;
        public int cachedTextures;
        public ulong cachedBytes;
        public ulong peakCachedBytes;
        public ulong maximumCachedBytes;
        public long created;
        public long reused;
        public long destroyed;
        public long discarded;
    }

    private readonly RunResult _result = new RunResult();
    private readonly double[] _routeCumulativeLengths =
        new double[RouteLengthSegments + 1];
    private GeodeticCameraControl _cameraControl;
    private SceneManager _sceneManager;
    private GizmoVector3D _routeCenter;
    private GizmoMatrix3 _routeOrientation;
    private float _eastRadiusMeters;
    private float _northRadiusMeters;
    private double _routeScale;
    private double _routeLengthMeters;
    private bool _routePrepared;
    private RunState _state;
    private double _stateStartTime;
    private double _runStartTime;
    private int _stateStep;
    private bool _suiteRunning;
    private int _suiteIterationIndex;
    private int _suiteTestIndex;
    private int _suiteRunIndex;
    private string _suiteRunId;
    private BenchmarkTest _activeTest;
    private string _progressText;
    private GUIStyle _progressStyle;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5))
            StartSuite();

        if (_suiteRunning && _state == RunState.Complete)
            StartNextTest();

        if (_state == RunState.WarmUp || _state == RunState.Measuring)
            UpdateFlight();

        if (_state == RunState.Measuring)
        {
            GetActiveResult().Frames.Sample(Time.unscaledDeltaTime);
        }

    }

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(_progressText))
            return;

        _progressStyle ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleRight,
            fontSize = 18,
        };
        GUI.Label(
            new Rect(8.0f, Screen.height - 40.0f, Screen.width - 16.0f, 32.0f),
            _progressText,
            _progressStyle);
    }

    private void StartSuite()
    {
        if (_state == RunState.WarmUp || _state == RunState.Measuring)
            return;

        if (_tests == null || _tests.Length == 0)
        {
            Fail("No benchmark tests are configured.");
            _state = RunState.Error;
            return;
        }

        for (var index = 0; index < _tests.Length; index++)
        {
            if (_tests[index] != null)
                continue;

            Fail($"Benchmark test {index + 1} is not assigned.");
            _state = RunState.Error;
            return;
        }

        _testIterations = Mathf.Max(1, _testIterations);
        _suiteIterationIndex = 0;
        _suiteTestIndex = 0;
        _suiteRunIndex = 0;
        _suiteRunId =
            $"{DateTime.UtcNow:yyyyMMddTHHmmss.fffffffZ}-" +
            $"{System.Diagnostics.Process.GetCurrentProcess().Id}-" +
            Guid.NewGuid().ToString("N");
        _suiteRunning = true;
        StartTest();
    }

    private void StartTest()
    {
        _activeTest = _tests[_suiteTestIndex];
        UpdateProgressText();
        _lapDurationSeconds = Mathf.Max(0.1f, _lapDurationSeconds);
        _measuredLaps = Mathf.Max(1, _measuredLaps);
        _stepsPerLap = Mathf.Clamp(
            _stepsPerLap,
            1,
            Math.Max(1, MaximumFrameSamples / _measuredLaps));
        _result.Reset();
        _runStartTime = Time.realtimeSinceStartupAsDouble;
        _result.RunStartUtc = DateTime.UtcNow;
        _result.RunStartLocal = DateTime.Now;
        if (_sceneManager == null)
            _sceneManager = FindFirstObjectByType<SceneManager>();
        if (_sceneManager == null)
        {
            StopSuiteWithError("SceneManager unavailable.");
            return;
        }

        try
        {
            _activeTest.SetUp(_sceneManager);
        }
        catch (Exception exception)
        {
            AbortActiveTest(
                $"Test {_activeTest.TestName} setup failed: {exception}");
            return;
        }

        if (!TryPrepareMapForTest(out var warning))
        {
            GetActiveResult().ValidityWarning = warning;
            AbortActiveTest(warning);
            return;
        }

        GetActiveResult().CleanMapReload = _reloadMapBetweenTests;
        GetActiveResult().MapUrl = _sceneManager.MapUrl;
        GetActiveResult().MinimumBuildBudgetMilliseconds =
            _sceneManager.Settings.MinBuildTime * 1000.0;
        GetActiveResult().MaximumBuildBudgetMilliseconds =
            _sceneManager.Settings.MaxBuildTime * 1000.0;
        if (_reloadMapBetweenTests)
        {
            GetActiveResult().ValidityWarning =
                "MapStreamer active and pooled textures were reset. Gizmo " +
                "native process caches are not observable.";
        }

        if (!TryPrepareRoute(out warning))
        {
            GetActiveResult().ValidityWarning = warning;
            AbortActiveTest(warning);
            return;
        }

        _result.RouteScale = _routeScale;
        _result.RouteLengthMeters = _routeLengthMeters;
        _result.EastRadiusMeters = _eastRadiusMeters;
        _result.NorthRadiusMeters = _northRadiusMeters;
        _state = RunState.WarmUp;
        _stateStartTime = Time.realtimeSinceStartupAsDouble;
        ApplyFlightPose(0.0);
        if (_state == RunState.Error)
            return;

        _stateStep =
            _progressionMode == CameraProgressionMode.FixedSamplesPerLap
                ? 1
                : 0;
        Debug.Log(
            $"Streaming benchmark: {_activeTest.TestName}, " +
            $"iteration {_suiteIterationIndex + 1}/{_testIterations}, " +
            $"test {_suiteTestIndex + 1}/{_tests.Length}; " +
            (_reloadMapBetweenTests ? "map reloaded; " : "") +
            "warm-up started. " +
            $"Route {_routeLengthMeters:F0} m, scale {_routeScale:F3}, " +
            GetProgressionDescription() + ".");
    }

    private void StartNextTest()
    {
        _suiteRunIndex++;
        _suiteTestIndex++;
        if (_suiteTestIndex >= _tests.Length)
        {
            _suiteTestIndex = 0;
            _suiteIterationIndex++;
        }

        if (_suiteIterationIndex >= _testIterations)
        {
            _suiteRunning = false;
            _activeTest = null;
            _progressText =
                $"{CreateTestSequenceText(-1)} " +
                $"Complete ({_testIterations}/{_testIterations})";
            Debug.Log(
                $"Streaming benchmark suite {_suiteRunId} complete: " +
                $"{_suiteRunIndex} runs.");
            return;
        }

        StartTest();
    }

    private void UpdateProgressText()
    {
        _progressText =
            $"{CreateTestSequenceText(_suiteTestIndex)} " +
            $"({_suiteIterationIndex + 1}/{_testIterations})";
    }

    private string CreateTestSequenceText(int activeTestIndex)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < _tests.Length; index++)
        {
            if (index > 0)
                builder.Append(' ');
            if (index == activeTestIndex)
                builder.Append('[');

            builder.Append(_tests[index].TestName);

            if (index == activeTestIndex)
                builder.Append(']');
        }

        return builder.ToString();
    }

    private bool TryPrepareMapForTest(out string warning)
    {
        warning = null;
        if (_sceneManager == null)
            _sceneManager = FindFirstObjectByType<SceneManager>();
        if (_sceneManager == null)
        {
            warning = "SceneManager unavailable; clean run was not started.";
            return false;
        }

        if (!_reloadMapBetweenTests)
        {
            if (MapControl.SystemMap?.CurrentMap != null)
                return true;

            warning = "Map unavailable; benchmark test was not started.";
            return false;
        }

        var mapUrl = _sceneManager.MapUrl;
        if (string.IsNullOrEmpty(mapUrl))
        {
            warning = "Map URL unavailable; clean run was not started.";
            return false;
        }

        _routePrepared = false;
        if (!_sceneManager.ResetMap())
        {
            warning = "Map reset failed; clean run was not started.";
            return false;
        }

        // Active textures have been released by ResetMap, so the pooled
        // textures can now be destroyed without touching live scene objects.
        Texture2DCache.Clear();
        if (!_sceneManager.LoadMap(mapUrl))
        {
            warning = "Map reload failed; clean run was not started.";
            return false;
        }

        return true;
    }

    private bool TryPrepareRoute(out string warning)
    {
        warning = null;
        if (_cameraControl == null)
            _cameraControl = FindFirstObjectByType<GeodeticCameraControl>();

        var map = MapControl.SystemMap;
        if (_cameraControl == null)
        {
            warning = "GeodeticCameraControl unavailable.";
            return false;
        }
        if (map?.CurrentMap == null)
        {
            warning = "Map unavailable after reload.";
            return false;
        }
        if (_routePrepared)
            return true;

        var southWest = map.SWExtent;
        var northEast = map.NEExtent;
        if (!TryGetRouteDimensions(
                southWest,
                northEast,
                out var center,
                out var eastRadius,
                out var northRadius,
                out var scale,
                out warning))
        {
            return false;
        }

        var routeCenter = new MapPos();
        if (!map.SetPosition(
                routeCenter,
                center,
                GroundClampType.GROUND,
                ClampFlags.WAIT_FOR_DATA))
        {
            routeCenter.node?.Dispose();
            routeCenter.node = null;
            warning = "Map center could not be clamped to terrain.";
            return false;
        }

        try
        {
            _routeCenter = routeCenter.GlobalPosition();
            _routeOrientation = routeCenter.local_orientation;
            _eastRadiusMeters = eastRadius;
            _northRadiusMeters = northRadius;
            _routeScale = scale;
            _routeLengthMeters = PrepareRouteArcLengths(
                eastRadius,
                northRadius);
            _routePrepared = true;
            return true;
        }
        finally
        {
            routeCenter.node?.Dispose();
            routeCenter.node = null;
        }
    }

    private bool TryGetRouteDimensions(
        LatPos southWest,
        LatPos northEast,
        out LatPos center,
        out float eastRadius,
        out float northRadius,
        out double scale,
        out string warning)
    {
        center = default;
        eastRadius = 0.0f;
        northRadius = 0.0f;
        scale = 0.0;
        warning = null;

        var latitudeDelta = northEast.Latitude - southWest.Latitude;
        var longitudeDelta = NormalizeLongitudeDelta(
            northEast.Longitude - southWest.Longitude);
        if (!IsFinite(latitudeDelta) ||
            !IsFinite(longitudeDelta) ||
            latitudeDelta <= 0.0 ||
            Math.Abs(longitudeDelta) <= double.Epsilon)
        {
            warning = "Map extents are missing or invalid.";
            return false;
        }

        var centerLatitude =
            southWest.Latitude + latitudeDelta * 0.5;
        var centerLongitude = NormalizeLongitude(
            southWest.Longitude + longitudeDelta * 0.5);
        var centerAltitude =
            (southWest.Altitude + northEast.Altitude) * 0.5;
        center = new LatPos(
            centerLatitude,
            centerLongitude,
            centerAltitude);

        GetMetersPerRadian(
            centerLatitude,
            centerAltitude,
            out var metersPerLatitudeRadian,
            out var metersPerLongitudeRadian);
        var halfNorthMeters =
            Math.Abs(latitudeDelta) * metersPerLatitudeRadian * 0.5;
        var halfEastMeters =
            Math.Abs(longitudeDelta) * metersPerLongitudeRadian * 0.5;
        var usableFraction = 1.0 - Mathf.Clamp(_mapMargin, 0.0f, 0.45f);
        var usableEastMeters = halfEastMeters * usableFraction;
        var usableNorthMeters = halfNorthMeters * usableFraction;
        if (usableEastMeters < MinimumExtentMeters ||
            usableNorthMeters < MinimumExtentMeters)
        {
            warning =
                "Map extents are too small for a benchmark route.";
            return false;
        }

        scale = Math.Min(
            1.0,
            Math.Min(
                usableEastMeters / StandardEastRadiusMeters,
                usableNorthMeters / StandardNorthRadiusMeters));
        eastRadius = (float)(StandardEastRadiusMeters * scale);
        northRadius = (float)(StandardNorthRadiusMeters * scale);
        return true;
    }

    private static void GetMetersPerRadian(
        double latitude,
        double altitude,
        out double latitudeMeters,
        out double longitudeMeters)
    {
        const double semiMajorAxis = 6378137.0;
        const double eccentricitySquared = 6.69437999014e-3;
        var sine = Math.Sin(latitude);
        var denominator = Math.Sqrt(
            1.0 - eccentricitySquared * sine * sine);
        var primeVerticalRadius = semiMajorAxis / denominator;
        var meridianRadius =
            semiMajorAxis * (1.0 - eccentricitySquared) /
            (denominator * denominator * denominator);
        latitudeMeters = meridianRadius + altitude;
        longitudeMeters =
            (primeVerticalRadius + altitude) * Math.Abs(Math.Cos(latitude));
    }

    private static double NormalizeLongitudeDelta(double longitude)
    {
        while (longitude > Math.PI)
            longitude -= Math.PI * 2.0;
        while (longitude < -Math.PI)
            longitude += Math.PI * 2.0;
        return longitude;
    }

    private static double NormalizeLongitude(double longitude)
    {
        return NormalizeLongitudeDelta(longitude);
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private double PrepareRouteArcLengths(
        float eastRadius,
        float northRadius)
    {
        var length = 0.0;
        var previousEast = 0.0;
        var previousNorth = 0.0;
        _routeCumulativeLengths[0] = 0.0;
        for (var segment = 1; segment <= RouteLengthSegments; segment++)
        {
            var angle =
                segment * Math.PI * 2.0 / RouteLengthSegments;
            var east = eastRadius * Math.Sin(angle);
            var north = northRadius * Math.Sin(angle * 2.0);
            var deltaEast = east - previousEast;
            var deltaNorth = north - previousNorth;
            length += Math.Sqrt(
                deltaEast * deltaEast + deltaNorth * deltaNorth);
            _routeCumulativeLengths[segment] = length;
            previousEast = east;
            previousNorth = north;
        }

        return length;
    }

    private double GetRouteParameter(double distanceProgress)
    {
        distanceProgress -= Math.Floor(distanceProgress);
        if (distanceProgress <= 0.0 || _routeLengthMeters <= 0.0)
            return 0.0;

        var targetLength = distanceProgress * _routeLengthMeters;
        var lower = 0;
        var upper = RouteLengthSegments;
        while (lower + 1 < upper)
        {
            var middle = (lower + upper) / 2;
            if (_routeCumulativeLengths[middle] <= targetLength)
                lower = middle;
            else
                upper = middle;
        }

        var segmentLength =
            _routeCumulativeLengths[upper] -
            _routeCumulativeLengths[lower];
        var segmentProgress = segmentLength > 0.0
            ? (targetLength - _routeCumulativeLengths[lower]) /
              segmentLength
            : 0.0;
        return (lower + segmentProgress) / RouteLengthSegments;
    }

    private void UpdateFlight()
    {
        if (_progressionMode ==
            CameraProgressionMode.FixedSamplesPerLap)
        {
            UpdateDeterministicFlight();
            return;
        }

        var elapsed = Time.realtimeSinceStartupAsDouble - _stateStartTime;
        var lapDuration = _lapDurationSeconds;

        if (_state == RunState.WarmUp)
        {
            ApplyFlightPose(elapsed / lapDuration);
            if (_state == RunState.Error || elapsed < lapDuration)
                return;

            ApplyFlightPose(0.0);
            if (_state == RunState.Error)
                return;

            BeginMeasurement();
            return;
        }

        var completedLaps = Mathf.Min(
            _measuredLaps,
            (int)(elapsed / lapDuration));
        GetActiveResult().CompletedLaps = completedLaps;

        if (completedLaps >= _measuredLaps)
        {
            ApplyFlightPose(0.0);
            if (_state != RunState.Error)
                CompleteMeasurement();
            return;
        }

        ApplyFlightPose(elapsed / lapDuration);
    }

    private void UpdateDeterministicFlight()
    {
        if (_state == RunState.WarmUp)
        {
            if (_stateStep >= _stepsPerLap)
            {
                BeginMeasurement();
                return;
            }

            ApplyFlightPose((double)_stateStep / _stepsPerLap);
            if (_state == RunState.Error)
                return;

            _stateStep++;
            return;
        }

        var totalSteps = _stepsPerLap * _measuredLaps;
        if (_stateStep >= totalSteps)
        {
            ApplyFlightPose(0.0);
            if (_state != RunState.Error)
                CompleteMeasurement();
            return;
        }

        ApplyFlightPose(
            (double)(_stateStep % _stepsPerLap) / _stepsPerLap);
        if (_state == RunState.Error)
            return;

        _stateStep++;
        GetActiveResult().CompletedLaps =
            Math.Min(_measuredLaps, _stateStep / _stepsPerLap);
    }

    private void BeginMeasurement()
    {
        _sceneManager.ResetStreamingDiagnostics();
        var result = GetActiveResult();
        result.Frames.Reset();
        result.CompletedLaps = 0;
        _state = RunState.Measuring;
        _stateStartTime = Time.realtimeSinceStartupAsDouble;
        ApplyFlightPose(0.0);
        if (_state == RunState.Error)
            return;

        _stateStep =
            _progressionMode == CameraProgressionMode.FixedSamplesPerLap
                ? 1
                : 0;
        Debug.Log(
            $"Streaming benchmark: {_measuredLaps} measured laps started; " +
            GetProgressionDescription() + ".");
    }

    private void CompleteMeasurement()
    {
        var result = GetActiveResult();
        result.Meshes = SceneManager.MeshTotalDiagnostics;
        result.Textures = SceneManager.TextureTotalDiagnostics;
        var now = Time.realtimeSinceStartupAsDouble;
        result.MeasuredDurationSeconds = now - _stateStartTime;
        result.TotalDurationSeconds = now - _runStartTime;
        result.Frames.Complete();
        try
        {
            result.Test = CreateTestReport();
        }
        catch (Exception exception)
        {
            AbortActiveTest(
                $"Test {_activeTest.TestName} report failed: {exception}");
            return;
        }

        _state = RunState.Complete;
        if (_saveDiagnosticsToFile)
            PersistReport(result);
        else
            Debug.Log(
                "Streaming benchmark complete.\n" +
                FormatStatistics(result));

        if (_state == RunState.Complete)
            TearDownActiveTest();
    }

    private BenchmarkTestReport CreateTestReport()
    {
        var report = new BenchmarkTestReport
        {
            name = _activeTest.TestName,
            type = _activeTest.GetType().FullName,
        };
        _activeTest.AddReportData(report, _sceneManager);
        return report;
    }

    private void TearDownActiveTest()
    {
        if (_activeTest == null)
            return;

        try
        {
            _activeTest.TearDown(_sceneManager);
        }
        catch (Exception exception)
        {
            StopSuiteWithError(
                $"Test {_activeTest.TestName} teardown failed: {exception}");
        }
    }

    private void StopSuiteWithError(string reason)
    {
        _suiteRunning = false;
        _state = RunState.Error;
        _progressText = string.IsNullOrEmpty(_progressText)
            ? "Benchmark error"
            : _progressText + " - Error";
        Fail(reason);
    }

    private void AbortActiveTest(string reason)
    {
        try
        {
            _activeTest?.TearDown(_sceneManager);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Streaming benchmark test teardown after failure also " +
                $"failed: {exception}");
        }

        StopSuiteWithError(reason);
    }

    private void ApplyFlightPose(double lapProgress)
    {
        if (!_routePrepared || _cameraControl == null)
            return;

        var routeParameter = GetRouteParameter(lapProgress);
        var angle = (float)(routeParameter * Mathf.PI * 2.0);
        var eastOffset = Mathf.Sin(angle) * _eastRadiusMeters;
        var northOffset = Mathf.Sin(angle * 2.0f) * _northRadiusMeters;
        var globalPosition = _routeCenter + _routeOrientation *
            new GizmoVector3(eastOffset, northOffset, _altitudeMeters);
        var localOrientation =
            MapControl.SystemMap.GetLocalOrientation(globalPosition);
        var east = localOrientation.GetCol(0).ToVector3FlipZ().normalized;
        var north = localOrientation.GetCol(1).ToVector3FlipZ().normalized;
        var up = localOrientation.GetCol(2).ToVector3FlipZ().normalized;
        var tangent = (
            east * (Mathf.Cos(angle) * _eastRadiusMeters) +
            north * (2.0f * Mathf.Cos(angle * 2.0f) *
                _northRadiusMeters)).normalized;
        var pitchRadians = LookDownDegrees * Mathf.Deg2Rad;
        var lookDirection = (
            tangent * Mathf.Cos(pitchRadians) -
            up * Mathf.Sin(pitchRadians)).normalized;
        var rotation = Quaternion.LookRotation(lookDirection, up) *
            Quaternion.AngleAxis(-BankDegrees, Vector3.forward);

        if (_cameraControl.TrySetPose(globalPosition, rotation))
            return;

        GetActiveResult().ValidityWarning = "Camera route failed during run.";
        AbortActiveTest(GetActiveResult().ValidityWarning);
    }

    private RunResult GetActiveResult()
    {
        return _result;
    }

    private string GetProgressionDescription()
    {
        if (_progressionMode ==
            CameraProgressionMode.FixedSamplesPerLap)
        {
            var sampleDistance = _routeLengthMeters / _stepsPerLap;
            return $"{_stepsPerLap} fixed camera samples/lap " +
                   $"({sampleDistance:F2} m/sample, uniform distance)";
        }

        return $"lap {_lapDurationSeconds:F1} s, uniform distance";
    }

    private string FormatStatistics(
        RunResult result)
    {
        var meshes = result.Meshes;
        var textures = result.Textures;
        if (_state == RunState.Measuring && _sceneManager != null)
        {
            meshes = SceneManager.MeshTotalDiagnostics;
            textures = SceneManager.TextureTotalDiagnostics;
        }

        var builder = new StringBuilder(768);
        builder.Append("HEADER\n")
            .Append("Test: ")
            .Append(result.Test?.name ?? _activeTest?.TestName ?? "None")
            .Append('\n')
            .Append("Mode / Status: ")
            .Append("Production GeometryMeshExport + TextureExport | ")
            .Append(GetDisplayStatus(result)).Append('\n')
            .Append("Configuration / Scenario: ")
            .Append("1 warm-up + ")
            .Append(_measuredLaps).Append(" measured laps | ")
            .Append(GetProgressionDescription())
            .Append(" | figure-eight ")
            .Append(result.RouteLengthMeters.ToString("F0"))
            .Append(" m | scale ")
            .Append(result.RouteScale.ToString("F3")).Append('\n')
            .Append("Control: F5 starts a clean run\n")
            .Append("GENERAL\n")
            .Append("Frame Time Avg / P95 / P99: ")
            .Append(result.Frames.AverageMilliseconds.ToString("F2"))
            .Append(" | ")
            .Append(result.Frames.P95Milliseconds.ToString("F2"))
            .Append(" | ")
            .Append(result.Frames.P99Milliseconds.ToString("F2"))
            .Append(" ms\nMeasured duration: ")
            .Append(result.MeasuredDurationSeconds.ToString("F3"))
            .Append(" s\nAverage FPS: ")
            .Append(result.Frames.AverageFps.ToString("F1")).Append('\n')
            .Append("MESH\nMeshes / Payload: ")
            .Append(meshes.TotalMeshes.ToString("N0")).Append(" | ")
            .Append(FormatBytes(meshes.TotalBytes)).Append('\n')
            .Append("Build Total / Average: ")
            .Append(FormatBuild(meshes.TotalTime, meshes.TotalMeshes))
            .Append("\nProcessing Throughput: ")
            .Append(FormatThroughput(
                meshes.TotalMeshes,
                meshes.TotalBytes,
                meshes.TotalTime,
                "meshes"))
            .Append("\nTEXTURE\nTextures / Payload: ")
            .Append(textures.TotalTextures.ToString("N0")).Append(" | ")
            .Append(FormatBytes(textures.TotalBytes)).Append('\n')
            .Append("Build Total / Average: ")
            .Append(FormatBuild(textures.TotalTime, textures.TotalTextures))
            .Append("\nProcessing Throughput: ")
            .Append(FormatThroughput(
                textures.TotalTextures,
                textures.TotalBytes,
                textures.TotalTime,
                "textures"));
        return builder.ToString();
    }

    private static string FormatBuild(TimeSpan totalTime, long count)
    {
        var totalMilliseconds = totalTime.TotalMilliseconds;
        var averageMilliseconds = count > 0
            ? totalMilliseconds / count
            : 0.0;
        return $"{totalMilliseconds:F2} ms | {averageMilliseconds:F3} ms";
    }

    private static string FormatThroughput(
        long count,
        long bytes,
        TimeSpan totalTime,
        string unit)
    {
        var seconds = totalTime.TotalSeconds;
        if (seconds <= 0.0)
            return $"0.00 {unit}/s | 0.00 MiB/s";
        return $"{count / seconds:F2} {unit}/s | " +
               $"{bytes / (1024.0 * 1024.0) / seconds:F2} MiB/s";
    }

    private static string FormatBytes(long bytes)
    {
        return $"{bytes / (1024.0 * 1024.0):F2} MiB";
    }

    private string GetDisplayStatus(RunResult result)
    {
        if (_state == RunState.WarmUp) return "Warm-up";
        if (_state == RunState.Measuring) return "Measuring";
        if (_state == RunState.Complete) return "Complete";
        if (_state == RunState.Error) return "Error";
        return string.IsNullOrEmpty(result.ValidityWarning) ? "Idle" : "Error";
    }

    private void PersistReport(RunResult result)
    {
        try
        {
            var completedUtc = DateTime.UtcNow;
            var completedLocal = DateTime.Now;
            var textureManager = _sceneManager.TextureDiagnostics;

            var runId =
                $"{completedUtc:yyyyMMddTHHmmss.fffffffZ}-" +
                $"{System.Diagnostics.Process.GetCurrentProcess().Id}-" +
                Guid.NewGuid().ToString("N");
            var report = CreateReport(
                result,
                textureManager,
                runId,
                completedUtc,
                completedLocal);
            var directory = Path.Combine(
                Application.persistentDataPath,
                ReportDirectoryName);
            Directory.CreateDirectory(directory);
            var prefix =
                $"{GetSafeFileName(result.Test.name)}-" +
                $"i{_suiteIterationIndex + 1:D2}-" +
                $"t{_suiteTestIndex + 1:D2}-{runId}";
            var jsonPath = Path.Combine(directory, prefix + ".json");
            var textPath = Path.Combine(directory, prefix + ".txt");
            var json = JsonUtility.ToJson(report, true);
            var text = CreateTextReport(report, result);

            File.WriteAllText(jsonPath, json, new UTF8Encoding(false));
            File.WriteAllText(textPath, text, new UTF8Encoding(false));

            Debug.Log(
                $"Streaming benchmark report JSON: {jsonPath}");
            Debug.Log(
                $"Streaming benchmark report TXT: {textPath}");
        }
        catch (Exception exception)
        {
            AbortActiveTest(
                $"Streaming benchmark report write failed: {exception}");
        }
    }

    private static string GetSafeFileName(string value)
    {
        foreach (var character in Path.GetInvalidFileNameChars())
            value = value.Replace(character, '-');
        return value;
    }

    private BenchmarkReport CreateReport(
        RunResult result,
        TextureManagerDiagnostics textureManager,
        string runId,
        DateTime completedUtc,
        DateTime completedLocal)
    {
        return new BenchmarkReport
        {
            schemaVersion = 4,
            runIdentity = RunIdentity,
            runId = runId,
            startedUtc = result.RunStartUtc.ToString("O", CultureInfo.InvariantCulture),
            startedLocal = result.RunStartLocal.ToString("O", CultureInfo.InvariantCulture),
            completedUtc = completedUtc.ToString("O", CultureInfo.InvariantCulture),
            completedLocal = completedLocal.ToString("O", CultureInfo.InvariantCulture),
            suite = new SuiteReport
            {
                suiteRunId = _suiteRunId,
                iteration = _suiteIterationIndex + 1,
                iterationCount = _testIterations,
                testIndex = _suiteTestIndex + 1,
                testsPerIteration = _tests.Length,
                runIndex = _suiteRunIndex + 1,
                totalRuns = _tests.Length * _testIterations,
            },
            test = result.Test,
            runtime = new RuntimeReport
            {
                unityVersion = Application.unityVersion,
                applicationVersion = Application.version,
                buildGuid = Application.buildGUID,
                platform = Application.platform.ToString(),
                operatingSystem = SystemInfo.operatingSystem,
                processor = SystemInfo.processorType,
                graphicsDevice = SystemInfo.graphicsDeviceName,
                dotNetRuntime = Environment.Version.ToString(),
                quality = QualitySettings.names[QualitySettings.GetQualityLevel()],
                qualityLevel = QualitySettings.GetQualityLevel(),
                vSyncCount = QualitySettings.vSyncCount,
                fullScreenMode = Screen.fullScreenMode.ToString(),
                screenWidth = Screen.width,
                screenHeight = Screen.height,
            },
            scenario = new ScenarioReport
            {
                mapUrl = result.MapUrl,
                cleanMapReload = result.CleanMapReload,
                validityWarning = result.ValidityWarning,
                cameraProgressionMode = _progressionMode.ToString(),
                routeParameterization = "UniformDistance",
                warmUpLaps = 1,
                measuredLaps = result.CompletedLaps,
                lapDurationSeconds = _lapDurationSeconds,
                stepsPerLap = _stepsPerLap,
                cameraSampleDistanceMeters =
                    result.RouteLengthMeters / _stepsPerLap,
                measuredCameraSamples = result.Frames.Count,
                totalDurationSeconds = result.TotalDurationSeconds,
                measuredDurationSeconds = result.MeasuredDurationSeconds,
                cameraSamplesPerSecond =
                    result.MeasuredDurationSeconds > 0.0
                        ? result.Frames.Count /
                          result.MeasuredDurationSeconds
                        : 0.0,
                altitudeMeters = _altitudeMeters,
                eastRadiusMeters = result.EastRadiusMeters,
                northRadiusMeters = result.NorthRadiusMeters,
                routeScale = result.RouteScale,
                routeLengthMeters = result.RouteLengthMeters,
                mapMargin = _mapMargin,
                lookDownDegrees = LookDownDegrees,
                bankDegrees = BankDegrees,
                minimumBuildBudgetMilliseconds =
                    result.MinimumBuildBudgetMilliseconds,
                maximumBuildBudgetMilliseconds =
                    result.MaximumBuildBudgetMilliseconds,
            },
            frames = new FrameReport
            {
                count = result.Frames.Count,
                averageMilliseconds = result.Frames.AverageMilliseconds,
                p95Milliseconds = result.Frames.P95Milliseconds,
                p99Milliseconds = result.Frames.P99Milliseconds,
                worstMilliseconds = result.Frames.WorstMilliseconds,
                averageFps = result.Frames.AverageFps,
            },
            meshes = CreateBuildReport(
                result.Meshes.TotalMeshes,
                result.Meshes.TotalBytes,
                result.Meshes.TotalTime,
                result.Meshes.WorstTime),
            textures = CreateBuildReport(
                result.Textures.TotalTextures,
                result.Textures.TotalBytes,
                result.Textures.TotalTime,
                result.Textures.WorstTime),
            textureManager = new TextureManagerReport
            {
                activeTextures = textureManager.ActiveTextures,
                activeReferences = textureManager.ActiveReferences,
                cachedTextures = textureManager.CachedTextures,
                cachedBytes = textureManager.CachedBytes,
                peakCachedBytes = textureManager.PeakCachedBytes,
                maximumCachedBytes = textureManager.MaximumCachedBytes,
                created = textureManager.Created,
                reused = textureManager.Reused,
                destroyed = textureManager.Destroyed,
                discarded = textureManager.Discarded,
            },
        };
    }

    private static BuildReport CreateBuildReport(
        long count,
        long bytes,
        TimeSpan totalTime,
        TimeSpan worstTime)
    {
        var milliseconds = totalTime.TotalMilliseconds;
        var seconds = totalTime.TotalSeconds;
        return new BuildReport
        {
            count = count,
            payloadBytes = bytes,
            totalBuildMilliseconds = milliseconds,
            averageBuildMilliseconds =
                count > 0 ? milliseconds / count : 0.0,
            worstBuildMilliseconds = worstTime.TotalMilliseconds,
            itemsPerSecond = seconds > 0.0 ? count / seconds : 0.0,
            mebibytesPerSecond = seconds > 0.0
                ? bytes / (1024.0 * 1024.0) / seconds
                : 0.0,
        };
    }

    private string CreateTextReport(
        BenchmarkReport report,
        RunResult result)
    {
        var culture = CultureInfo.InvariantCulture;
        var progression = report.scenario.cameraProgressionMode ==
                          CameraProgressionMode.FixedSamplesPerLap.ToString()
            ? $"{report.scenario.stepsPerLap.ToString(culture)} samples/lap | " +
              $"{report.scenario.cameraSampleDistanceMeters.ToString("F3", culture)} m/sample"
            : $"{report.scenario.lapDurationSeconds.ToString("F1", culture)} s/lap";
        return new StringBuilder(2048)
            .AppendLine($"Run identity: {report.runIdentity}")
            .AppendLine($"Run ID: {report.runId}")
            .AppendLine($"Suite run ID: {report.suite.suiteRunId}")
            .AppendLine(
                $"Test: {report.test.name} | " +
                $"iteration {report.suite.iteration}/{report.suite.iterationCount} | " +
                $"slot {report.suite.testIndex}/{report.suite.testsPerIteration} | " +
                $"run {report.suite.runIndex}/{report.suite.totalRuns}")
            .AppendLine($"Started UTC: {report.startedUtc}")
            .AppendLine($"Completed UTC: {report.completedUtc}")
            .AppendLine($"Unity: {report.runtime.unityVersion}")
            .AppendLine($"Build GUID: {report.runtime.buildGuid}")
            .AppendLine($"Map: {report.scenario.mapUrl}")
            .AppendLine(
                $"Route: figure-eight | " +
                $"{report.scenario.routeLengthMeters.ToString("F1", culture)} m | " +
                $"scale {report.scenario.routeScale.ToString("F3", culture)}")
            .AppendLine(
                $"Scenario: 1 warm-up + " +
                $"{report.scenario.measuredLaps.ToString(culture)} measured laps | " +
                $"{report.scenario.cameraProgressionMode} | " +
                $"{report.scenario.routeParameterization} | " +
                progression)
            .AppendLine(
                $"Duration: measured " +
                $"{report.scenario.measuredDurationSeconds.ToString("F3", culture)} s | " +
                $"total {report.scenario.totalDurationSeconds.ToString("F3", culture)} s | " +
                $"{report.scenario.cameraSamplesPerSecond.ToString("F2", culture)} samples/s")
            .AppendLine(
                $"Frames: {report.frames.count.ToString(culture)} | " +
                $"Avg {report.frames.averageMilliseconds.ToString("F3", culture)} ms | " +
                $"P95 {report.frames.p95Milliseconds.ToString("F3", culture)} ms | " +
                $"P99 {report.frames.p99Milliseconds.ToString("F3", culture)} ms | " +
                $"Worst {report.frames.worstMilliseconds.ToString("F3", culture)} ms")
            .Append(FormatTestReport(report.test, culture))
            .AppendLine()
            .AppendLine(FormatStatistics(result))
            .ToString();
    }

    private static string FormatTestReport(
        BenchmarkTestReport report,
        CultureInfo culture)
    {
        var builder = new StringBuilder();
        if (report.properties.Count > 0 || report.metrics.Count > 0)
            builder.AppendLine().AppendLine("TEST");

        foreach (var property in report.properties)
        {
            builder.Append(property.name)
                .Append(": ")
                .AppendLine(property.value);
        }

        foreach (var metric in report.metrics)
        {
            builder.Append(metric.name)
                .Append(": ")
                .Append(metric.value.ToString("G17", culture));
            if (!string.IsNullOrEmpty(metric.unit))
                builder.Append(' ').Append(metric.unit);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static void Fail(string reason)
    {
        Debug.LogError($"Streaming benchmark failed: {reason}");
    }

}
}
