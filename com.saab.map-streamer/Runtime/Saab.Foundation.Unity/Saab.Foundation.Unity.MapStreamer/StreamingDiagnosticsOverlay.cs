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
using System.Text;
using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer
{
    public sealed class StreamingDiagnosticsOverlay : MonoBehaviour
    {
        private struct StreamingSample
        {
            public double Time;
            public MeshBuildDiagnostics Meshes;
            public TextureBuildDiagnostics Textures;
        }

        private const double RefreshIntervalSeconds = 0.25;
        private const double StreamingWindowSeconds = 1.0;
        private const double FrameWindowSeconds = 1.0;
        private const int MaximumFrameSamples = 1024;
        private const int MaximumStreamingSamples = 8;
        private const int FrameTimeHistogramBinCount = 1024;
        private const double FrameTimeHistogramBinMilliseconds = 0.1;
        private const float DiagnosticsHeight = 222.0f;
        private const float ControlsHeight = 30.0f;
        private const float PanelPadding = 10.0f;
        private const double BytesPerMiB = 1024.0 * 1024.0;
        private const string GrayStart = "<color=#AEBBC7>";
        private const string AccentStart = "<color=#FFB454>";
        private const string ColorEnd = "</color>";
        private static readonly Color PanelBackground =
            new Color(0.025f, 0.035f, 0.045f, 0.97f);
        private static readonly Color ControlsBackground =
            new Color(0.045f, 0.06f, 0.075f, 0.98f);
        private static readonly Color SeparatorColor =
            new Color(1.0f, 1.0f, 1.0f, 0.18f);

        [SerializeField]
        private SceneManager _sceneManager;

        private readonly float[] _frameDurations =
            new float[MaximumFrameSamples];
        private readonly float[] _sortedFrameDurations =
            new float[MaximumFrameSamples];
        private readonly double[] _meshFrameWorstMilliseconds =
            new double[MaximumFrameSamples];
        private readonly double[] _textureFrameWorstMilliseconds =
            new double[MaximumFrameSamples];
        private readonly long[] _totalFrameHistogram =
            new long[FrameTimeHistogramBinCount];
        private readonly StreamingSample[] _streamingSamples =
            new StreamingSample[MaximumStreamingSamples];
        private readonly StringBuilder _builder = new StringBuilder(512);
        private readonly GUIContent _inactiveContent =
            new GUIContent("F1 - Diagnostics");
        private readonly GUIContent _frameContent = new GUIContent();
        private readonly GUIContent _meshContent = new GUIContent();
        private readonly GUIContent _textureStreamingContent =
            new GUIContent();
        private readonly GUIContent _textureManagerContent =
            new GUIContent();
        private readonly GUIContent _controlsContent = new GUIContent();

        private int _frameNext;
        private int _frameCount;
        private long _totalFrameCount;
        private double _totalFrameSeconds;
        private double _totalFrameWorstMilliseconds;
        private int _streamingNext;
        private int _streamingCount;
        private long _observedResetVersion;
        private int _enabledVSyncCount = 1;
        private double _nextRefreshTime;
        private bool _visible;
        private GUIStyle _textStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _controlsStyle;

        private void Awake()
        {
            if (_sceneManager == null)
                _sceneManager = GetComponent<SceneManager>();
            if (_sceneManager == null)
                _sceneManager = FindFirstObjectByType<SceneManager>();

            _observedResetVersion =
                SceneManager.BuildDiagnosticsResetVersion;
            if (QualitySettings.vSyncCount > 0)
                _enabledVSyncCount = QualitySettings.vSyncCount;
            AddStreamingSample(Time.realtimeSinceStartupAsDouble);
            RefreshControlsContent();
        }

        private void Update()
        {
            ObserveExternalReset();
            SampleFrame(Time.unscaledDeltaTime);

            if (Input.GetKeyDown(KeyCode.F1))
            {
                _visible = !_visible;
                if (_visible)
                    RefreshContent();
            }

            if (_visible && Input.GetKeyDown(KeyCode.F2))
                ResetMeasurements();

            if (_visible && Input.GetKeyDown(KeyCode.F3))
                ToggleVSync();

            var now = Time.realtimeSinceStartupAsDouble;
            if (now < _nextRefreshTime)
                return;

            _nextRefreshTime = now + RefreshIntervalSeconds;
            AddStreamingSample(now);
            if (_visible)
                RefreshContent();
        }

        private void ObserveExternalReset()
        {
            var resetVersion = SceneManager.BuildDiagnosticsResetVersion;
            if (resetVersion == _observedResetVersion)
                return;

            _observedResetVersion = resetVersion;
            ResetLocalMeasurements();
            AddStreamingSample(Time.realtimeSinceStartupAsDouble);
        }

        private void ResetMeasurements()
        {
            _sceneManager?.ResetStreamingDiagnostics();
            _observedResetVersion =
                SceneManager.BuildDiagnosticsResetVersion;
            ResetLocalMeasurements();
            AddStreamingSample(Time.realtimeSinceStartupAsDouble);
            RefreshContent();
        }

        private void ResetLocalMeasurements()
        {
            _frameNext = 0;
            _frameCount = 0;
            _totalFrameCount = 0;
            _totalFrameSeconds = 0.0;
            _totalFrameWorstMilliseconds = 0.0;
            Array.Clear(
                _totalFrameHistogram,
                0,
                _totalFrameHistogram.Length);
            _streamingNext = 0;
            _streamingCount = 0;
        }

        private void SampleFrame(float durationSeconds)
        {
            if (durationSeconds <= 0.0f ||
                float.IsNaN(durationSeconds) ||
                float.IsInfinity(durationSeconds))
            {
                return;
            }

            var frameIndex = _frameNext;
            _frameDurations[frameIndex] = durationSeconds;
            if (_sceneManager != null)
            {
                _meshFrameWorstMilliseconds[frameIndex] =
                    SceneManager.MeshFrameDiagnostics
                        .WorstTime.TotalMilliseconds;
                _textureFrameWorstMilliseconds[frameIndex] =
                    SceneManager.TextureFrameDiagnostics
                        .WorstTime.TotalMilliseconds;
            }
            else
            {
                _meshFrameWorstMilliseconds[frameIndex] = 0.0;
                _textureFrameWorstMilliseconds[frameIndex] = 0.0;
            }

            _frameNext = (_frameNext + 1) % _frameDurations.Length;
            if (_frameCount < _frameDurations.Length)
                _frameCount++;

            var milliseconds = durationSeconds * 1000.0;
            _totalFrameCount++;
            _totalFrameSeconds += durationSeconds;
            _totalFrameWorstMilliseconds =
                Math.Max(_totalFrameWorstMilliseconds, milliseconds);
            var histogramBin = GetHistogramBin(milliseconds);
            _totalFrameHistogram[histogramBin]++;
        }

        private void AddStreamingSample(double now)
        {
            if (_sceneManager == null)
                return;

            var sample = new StreamingSample
            {
                Time = now,
                Meshes = SceneManager.MeshTotalDiagnostics,
                Textures = SceneManager.TextureTotalDiagnostics,
            };
            if (_streamingCount > 0)
            {
                var previousIndex =
                    (_streamingNext - 1 + _streamingSamples.Length) %
                    _streamingSamples.Length;
                var previous = _streamingSamples[previousIndex];
                if (sample.Meshes.TotalMeshes <
                        previous.Meshes.TotalMeshes ||
                    sample.Textures.TotalTextures <
                        previous.Textures.TotalTextures)
                {
                    _streamingNext = 0;
                    _streamingCount = 0;
                }
            }

            _streamingSamples[_streamingNext] = sample;
            _streamingNext =
                (_streamingNext + 1) % _streamingSamples.Length;
            if (_streamingCount < _streamingSamples.Length)
                _streamingCount++;
        }

        private void RefreshContent()
        {
            RefreshFrameContent();
            RefreshStreamingContent();
            RefreshTextureManagerContent();
            RefreshControlsContent();
        }

        private void ToggleVSync()
        {
            if (QualitySettings.vSyncCount > 0)
            {
                _enabledVSyncCount = QualitySettings.vSyncCount;
                QualitySettings.vSyncCount = 0;
            }
            else
            {
                QualitySettings.vSyncCount =
                    Math.Max(1, _enabledVSyncCount);
            }

            RefreshControlsContent();
        }

        private void RefreshControlsContent()
        {
            _builder.Clear()
                .Append("F1 Close          F2 Reset          F3 VSync ");
            if (QualitySettings.vSyncCount > 0)
            {
                _builder.Append("ON (")
                    .Append(QualitySettings.vSyncCount)
                    .Append(')');
            }
            else
            {
                _builder.Append("OFF");
            }
            _controlsContent.text = _builder.ToString();
        }

        private void RefreshFrameContent()
        {
            GetCurrentFrameStatistics(
                out var fps,
                out var averageMilliseconds,
                out var p95Milliseconds,
                out var worstMilliseconds);
            var totalAverageMilliseconds = _totalFrameCount > 0
                ? _totalFrameSeconds * 1000.0 / _totalFrameCount
                : 0.0;
            var totalP95Milliseconds = GetHistogramPercentile(
                _totalFrameHistogram,
                _totalFrameCount,
                0.95);

            _builder.Clear()
                .Append("<b>FRAME</b>                         FPS ")
                .Append(fps.ToString("F1"))
                .Append('\n').Append(GrayStart)
                .Append("CURRENT · 1 SECOND")
                .Append(ColorEnd).Append('\n')
                .Append(GrayStart).Append("Frame Time")
                .Append(ColorEnd).Append("  Avg ")
                .Append(FormatDuration(averageMilliseconds))
                .Append("  |  P95 ")
                .Append(FormatDuration(p95Milliseconds))
                .Append("  |  Worst ")
                .Append(FormatDuration(worstMilliseconds))
                .Append("\n\n").Append(GrayStart)
                .Append("TOTAL · SINCE F2 RESET")
                .Append(ColorEnd).Append('\n')
                .Append(GrayStart).Append("Frame Time")
                .Append(ColorEnd).Append("  Avg ")
                .Append(FormatDuration(totalAverageMilliseconds))
                .Append("  |  P95 ")
                .Append(FormatDuration(totalP95Milliseconds))
                .Append("  |  Worst ")
                .Append(FormatDuration(_totalFrameWorstMilliseconds));
            _frameContent.text = _builder.ToString();
        }

        private void GetCurrentFrameStatistics(
            out double fps,
            out double averageMilliseconds,
            out double p95Milliseconds,
            out double worstMilliseconds)
        {
            var elapsed = 0.0;
            var count = 0;
            worstMilliseconds = 0.0;
            var index =
                (_frameNext - 1 + _frameDurations.Length) %
                _frameDurations.Length;
            while (count < _frameCount &&
                   elapsed < FrameWindowSeconds)
            {
                var duration = _frameDurations[index];
                _sortedFrameDurations[count] = duration;
                elapsed += duration;
                worstMilliseconds = Math.Max(
                    worstMilliseconds,
                    duration * 1000.0);
                count++;
                index =
                    (index - 1 + _frameDurations.Length) %
                    _frameDurations.Length;
            }

            Array.Sort(_sortedFrameDurations, 0, count);
            averageMilliseconds =
                count > 0 ? elapsed * 1000.0 / count : 0.0;
            fps = elapsed > 0.0 ? count / elapsed : 0.0;
            var p95Index = count > 0
                ? Math.Max(0, (int)Math.Ceiling(count * 0.95) - 1)
                : 0;
            p95Milliseconds = count > 0
                ? _sortedFrameDurations[p95Index] * 1000.0
                : 0.0;
        }

        private void RefreshStreamingContent()
        {
            if (!TryGetStreamingWindow(
                    out var oldestIndex,
                    out var latestIndex,
                    out var elapsed))
            {
                _meshContent.text =
                    "MESH STREAMING\nWaiting for diagnostics...";
                _textureStreamingContent.text =
                    "TEXTURE STREAMING\nWaiting for diagnostics...";
                return;
            }

            var oldest = _streamingSamples[oldestIndex];
            var latest = _streamingSamples[latestIndex];
            GetRecentBuildWorst(
                out var currentMeshWorst,
                out var currentTextureWorst);
            RefreshMeshContent(
                oldest,
                latest,
                elapsed,
                currentMeshWorst);
            RefreshTextureStreamingContent(
                oldest,
                latest,
                elapsed,
                currentTextureWorst);
        }

        private void RefreshMeshContent(
            StreamingSample oldest,
            StreamingSample latest,
            double elapsed,
            double currentWorstMilliseconds)
        {
            var currentCount =
                latest.Meshes.TotalMeshes - oldest.Meshes.TotalMeshes;
            var currentBytes =
                latest.Meshes.TotalBytes - oldest.Meshes.TotalBytes;
            var currentTime =
                latest.Meshes.TotalTime - oldest.Meshes.TotalTime;

            BuildStreamingPanel(
                "MESH STREAMING",
                "meshes",
                currentCount,
                currentBytes,
                currentTime,
                currentWorstMilliseconds,
                latest.Meshes.TotalMeshes,
                latest.Meshes.TotalBytes,
                latest.Meshes.TotalTime,
                latest.Meshes.WorstTime.TotalMilliseconds,
                elapsed);
            _meshContent.text = _builder.ToString();
        }

        private void RefreshTextureStreamingContent(
            StreamingSample oldest,
            StreamingSample latest,
            double elapsed,
            double currentWorstMilliseconds)
        {
            var currentCount =
                latest.Textures.TotalTextures -
                oldest.Textures.TotalTextures;
            var currentBytes =
                latest.Textures.TotalBytes - oldest.Textures.TotalBytes;
            var currentTime =
                latest.Textures.TotalTime - oldest.Textures.TotalTime;

            BuildStreamingPanel(
                "TEXTURE STREAMING",
                "textures",
                currentCount,
                currentBytes,
                currentTime,
                currentWorstMilliseconds,
                latest.Textures.TotalTextures,
                latest.Textures.TotalBytes,
                latest.Textures.TotalTime,
                latest.Textures.WorstTime.TotalMilliseconds,
                elapsed);
            _textureStreamingContent.text = _builder.ToString();
        }

        private void BuildStreamingPanel(
            string title,
            string unit,
            long currentCount,
            long currentBytes,
            TimeSpan currentTime,
            double currentWorstMilliseconds,
            long totalCount,
            long totalBytes,
            TimeSpan totalTime,
            double totalWorstMilliseconds,
            double elapsed)
        {
            NormalizeWindow(
                ref currentCount,
                ref currentBytes,
                ref currentTime);
            var rate = elapsed > 0.0
                ? currentCount / elapsed
                : 0.0;
            var payloadRate = elapsed > 0.0
                ? currentBytes / BytesPerMiB / elapsed
                : 0.0;
            var currentAverageMilliseconds = currentCount > 0
                ? currentTime.TotalMilliseconds / currentCount
                : 0.0;
            var totalAverageMilliseconds = totalCount > 0
                ? totalTime.TotalMilliseconds / totalCount
                : 0.0;

            _builder.Clear()
                .Append("<b>").Append(title).Append("</b>")
                .Append('\n').Append(GrayStart)
                .Append("CURRENT · 1 SECOND")
                .Append(ColorEnd).Append('\n')
                .Append(GrayStart).Append("Rate")
                .Append(ColorEnd).Append("  ")
                .Append(rate.ToString("F1")).Append(' ')
                .Append(unit).Append("/s  |  ")
                .Append(payloadRate.ToString("F2"))
                .Append(" MiB/s\n")
                .Append(GrayStart).Append("Build Time")
                .Append(ColorEnd).Append("  Avg ")
                .Append(FormatDuration(currentAverageMilliseconds))
                .Append("  |  Worst ")
                .Append(FormatDuration(currentWorstMilliseconds))
                .Append("\n\n").Append(GrayStart)
                .Append("TOTAL · SINCE F2 RESET")
                .Append(ColorEnd).Append('\n')
                .Append(GrayStart).Append("Processed")
                .Append(ColorEnd).Append("  ")
                .Append(totalCount.ToString("N0")).Append(' ')
                .Append(unit).Append("  |  ")
                .Append(FormatBytes(totalBytes))
                .Append('\n')                .Append(GrayStart).Append("Build Time")
                .Append(ColorEnd).Append("  Avg ")
                .Append(FormatDuration(totalAverageMilliseconds))
                .Append("  |  Worst ")
                .Append(FormatDuration(totalWorstMilliseconds))
                .Append('\n').Append(GrayStart)
                .Append("Throughput").Append(ColorEnd).Append("  ")
                .Append(AccentStart)
                .Append(FormatBuildThroughput(totalBytes, totalTime))
                .Append(ColorEnd);
        }

        private void GetRecentBuildWorst(
            out double meshWorstMilliseconds,
            out double textureWorstMilliseconds)
        {
            meshWorstMilliseconds = 0.0;
            textureWorstMilliseconds = 0.0;
            var elapsed = 0.0;
            var count = 0;
            var index =
                (_frameNext - 1 + _frameDurations.Length) %
                _frameDurations.Length;
            while (count < _frameCount &&
                   elapsed < FrameWindowSeconds)
            {
                elapsed += _frameDurations[index];
                meshWorstMilliseconds = Math.Max(
                    meshWorstMilliseconds,
                    _meshFrameWorstMilliseconds[index]);
                textureWorstMilliseconds = Math.Max(
                    textureWorstMilliseconds,
                    _textureFrameWorstMilliseconds[index]);
                count++;
                index =
                    (index - 1 + _frameDurations.Length) %
                    _frameDurations.Length;
            }
        }

        private static void NormalizeWindow(
            ref long count,
            ref long bytes,
            ref TimeSpan time)
        {
            if (count >= 0 && bytes >= 0 && time >= TimeSpan.Zero)
                return;
            count = 0;
            bytes = 0;
            time = TimeSpan.Zero;
        }

        private bool TryGetStreamingWindow(
            out int oldestIndex,
            out int latestIndex,
            out double elapsed)
        {
            oldestIndex = 0;
            latestIndex = 0;
            elapsed = 0.0;
            if (_streamingCount == 0)
                return false;

            latestIndex =
                (_streamingNext - 1 + _streamingSamples.Length) %
                _streamingSamples.Length;
            oldestIndex = latestIndex;
            var latest = _streamingSamples[latestIndex];
            for (var offset = 1;
                 offset < _streamingCount;
                 offset++)
            {
                var index =
                    (latestIndex - offset + _streamingSamples.Length) %
                    _streamingSamples.Length;
                var candidate = _streamingSamples[index];
                if (latest.Time - candidate.Time >
                    StreamingWindowSeconds)
                {
                    break;
                }
                oldestIndex = index;
            }

            elapsed = latest.Time - _streamingSamples[oldestIndex].Time;
            return true;
        }

        private void RefreshTextureManagerContent()
        {
            if (_sceneManager == null)
            {
                _textureManagerContent.text =
                    "TEXTURE MANAGER\nSceneManager unavailable";
                return;
            }

            var diagnostics = _sceneManager.TextureDiagnostics;
            var requests = diagnostics.Created + diagnostics.Reused;
            var reusePercent = requests > 0
                ? diagnostics.Reused * 100.0 / requests
                : 0.0;

            _builder.Clear()
                .Append("<b>TEXTURE MANAGER</b>\n")
                .Append(GrayStart).Append("CURRENT")
                .Append(ColorEnd).Append('\n')
                .Append(GrayStart).Append("Active")
                .Append(ColorEnd).Append("  ")
                .Append(diagnostics.ActiveTextures.ToString("N0"))
                .Append(" unique  |  ")
                .Append(diagnostics.ActiveReferences.ToString("N0"))
                .Append(" refs\n").Append(GrayStart).Append("Cache")
                .Append(ColorEnd).Append("  ")
                .Append(diagnostics.CachedTextures.ToString("N0"))
                .Append(" textures  |  ")
                .Append(FormatMiB(diagnostics.CachedBytes))
                .Append(" / ")
                .Append(FormatMiB(diagnostics.MaximumCachedBytes))
                .Append('\n').Append(GrayStart).Append("Peak")
                .Append(ColorEnd).Append("  ")
                .Append(FormatMiB(diagnostics.PeakCachedBytes))
                .Append("\n\n").Append(GrayStart)
                .Append("TOTAL · SINCE F2 RESET")
                .Append(ColorEnd).Append('\n')
                .Append(GrayStart).Append("Created")
                .Append(ColorEnd).Append("  ")
                .Append(diagnostics.Created.ToString("N0"))
                .Append('\n').Append(GrayStart).Append("Reused")
                .Append(ColorEnd).Append("  ")
                .Append(diagnostics.Reused.ToString("N0"))
                .Append("  |  ")
                .Append(AccentStart)
                .Append(reusePercent.ToString("F1"))
                .Append("%")
                .Append(ColorEnd)
                .Append('\n').Append(GrayStart).Append("Destroyed")
                .Append(ColorEnd).Append("  ")
                .Append(diagnostics.Destroyed.ToString("N0"))
                .Append("  |  ").Append(GrayStart)
                .Append("Discarded").Append(ColorEnd).Append("  ")
                .Append(diagnostics.Discarded.ToString("N0"));
            _textureManagerContent.text = _builder.ToString();
        }

        private static int GetHistogramBin(double milliseconds)
        {
            return Math.Min(
                FrameTimeHistogramBinCount - 1,
                Math.Max(
                    0,
                    (int)(milliseconds /
                          FrameTimeHistogramBinMilliseconds)));
        }

        private static double GetHistogramPercentile(
            long[] histogram,
            long sampleCount,
            double percentile)
        {
            if (sampleCount <= 0)
                return 0.0;

            var target = (long)Math.Ceiling(sampleCount * percentile);
            long cumulative = 0;
            for (var bin = 0; bin < histogram.Length; bin++)
            {
                cumulative += histogram[bin];
                if (cumulative >= target)
                    return (bin + 1) *
                           FrameTimeHistogramBinMilliseconds;
            }

            return histogram.Length *
                   FrameTimeHistogramBinMilliseconds;
        }

        private static string FormatBytes(long bytes)
        {
            var mebibytes = bytes / BytesPerMiB;
            return mebibytes >= 1024.0
                ? (mebibytes / 1024.0).ToString("F2") + " GiB"
                : mebibytes.ToString("F2") + " MiB";
        }

        private static string FormatDuration(double milliseconds)
        {
            return milliseconds < 1.0
                ? (milliseconds * 1000.0).ToString("F1") + " µs"
                : milliseconds.ToString("F2") + " ms";
        }

        private static string FormatBuildThroughput(
            long bytes,
            TimeSpan buildTime)
        {
            if (buildTime <= TimeSpan.Zero)
                return "0.00 MiB/s";

            var mebibytesPerSecond =
                bytes / BytesPerMiB / buildTime.TotalSeconds;
            return mebibytesPerSecond >= 1024.0
                ? (mebibytesPerSecond / 1024.0).ToString("F2") +
                  " GiB/s"
                : mebibytesPerSecond.ToString("F2") + " MiB/s";
        }

        private static string FormatMiB(ulong bytes)
        {
            return (bytes / BytesPerMiB).ToString("F1") + " MiB";
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint)
                return;

            EnsureStyles();
            if (!_visible)
            {
                DrawSolidRect(
                    new Rect(6.0f, 5.0f, 150.0f, 28.0f),
                    ControlsBackground);
                GUI.Label(
                    new Rect(10.0f, 8.0f, 150.0f, 24.0f),
                    _inactiveContent,
                    _hintStyle);
                return;
            }

            var frameWidth = Screen.width * 0.22f;
            var meshWidth = Screen.width * 0.27f;
            var textureWidth = Screen.width * 0.27f;
            DrawPanel(0.0f, frameWidth, _frameContent);
            DrawPanel(frameWidth, meshWidth, _meshContent);
            DrawPanel(
                frameWidth + meshWidth,
                textureWidth,
                _textureStreamingContent);
            DrawPanel(
                frameWidth + meshWidth + textureWidth,
                Screen.width - frameWidth - meshWidth - textureWidth,
                _textureManagerContent);

            var controls = new Rect(
                0.0f,
                DiagnosticsHeight,
                Screen.width,
                ControlsHeight);
            DrawSolidRect(controls, ControlsBackground);
            DrawSolidRect(
                new Rect(0.0f, DiagnosticsHeight, Screen.width, 1.0f),
                SeparatorColor);
            GUI.Label(controls, _controlsContent, _controlsStyle);
        }

        private void DrawPanel(float x, float width, GUIContent content)
        {
            var panel = new Rect(
                x,
                0.0f,
                width,
                DiagnosticsHeight);
            DrawSolidRect(panel, PanelBackground);
            DrawSolidRect(
                new Rect(x + width - 1.0f, 0.0f, 1.0f, DiagnosticsHeight),
                SeparatorColor);
            GUI.Label(
                new Rect(
                    x + PanelPadding,
                    PanelPadding,
                    width - PanelPadding * 2.0f,
                    DiagnosticsHeight - PanelPadding * 2.0f),
                content,
                _textStyle);
        }

        private void EnsureStyles()
        {
            if (_textStyle != null)
                return;

            _textStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                clipping = TextClipping.Clip,
                fontSize = 14,
                richText = true,
                normal = { textColor = Color.white },
            };
            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
            };
            _controlsStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
            };
        }

        private static void DrawSolidRect(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }
    }
}
