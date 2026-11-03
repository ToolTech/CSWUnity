# Map Streamer benchmarking

The benchmark in `projects/com.saab.map-streamer/Assets/Benchmark` runs a
repeatable camera workload against one or more test configurations. It supports
ordinary performance runs, deterministic A/B suites, custom test setup and
cleanup, and per-test report data.

The example scene is already configured with `MeshStreamingBenchmark` and
`DefaultBenchmarkTest`.

## Running a benchmark

1. Open `projects/com.saab.map-streamer` in the Unity version specified by
   `ProjectSettings/ProjectVersion.txt`.
2. Open `Assets/Scenes/ExampleScene.unity`.
3. Configure the map normally and enter Play mode, or build and run the player.
4. Press **F5** once.

The lower-right label shows the sequence, active test, and iteration:

```text
BaselineTest [CandidateTest] CandidateTest BaselineTest (4/5)
```

## Benchmark configuration

### Tests

`Tests` is the ordered list of `BenchmarkTest` components to execute. A
component reference may appear more than once. `Test Iterations` repeats the
entire ordered list.

For an A/B comparison, prefer a balanced sequence such as:

```text
A, B, B, A
```

This gives both alternatives equivalent positions in each iteration.

Each test applies its setup, performs one warm-up lap, resets streaming
diagnostics, and measures the configured laps. Enable `Reload Map Between
Tests` when each test should reset and reload the map. It is disabled by
default.

### Camera progression

The route is a map-centered figure eight. Route samples are parameterized by
travelled distance, not by curve angle, so equal progress represents
approximately equal distance along the route.

The benchmark provides two progression modes:

- `Realtime`: advances according to elapsed wall-clock time. `Lap Duration
  Seconds` controls the duration of one lap. This produces approximately
  constant metres per second, but faster and slower implementations can visit
  different numbers of camera poses.
- `FixedSamplesPerLap`: advances by exactly one camera pose per rendered frame.
  `Steps Per Lap` controls the number of equally spaced poses. Every test visits
  the same poses in the same order, but wall-clock camera speed varies with
  frame time.

Use `FixedSamplesPerLap` for controlled A/B comparisons. Visible slowing in
expensive areas is expected: it represents longer frame times rather than
skipped camera samples.

`Measured Laps` controls the number of measured laps after the warm-up lap. The
runner stores at most 32,768 frame samples per test and clamps steps per lap
when necessary.

### Route

- `Altitude Meters` sets camera height above the route center.
- `Map Margin` reserves space between the route and map extents.

## Creating a benchmark test

Create a component derived from
`Saab.MapStreamer.Benchmark.BenchmarkTest`:

```csharp
using Saab.Foundation.Unity.MapStreamer;

namespace Saab.MapStreamer.Benchmark
{
    public sealed class BuildBudgetTest : BenchmarkTest
    {
        private double _originalMinimumBuildTime;

        public override string TestName => "BuildBudget4ms";

        public override void SetUp(SceneManager sceneManager)
        {
            _originalMinimumBuildTime =
                sceneManager.Settings.MinBuildTime;
            sceneManager.Settings.MinBuildTime = 0.004;
        }

        public override void AddReportData(
            BenchmarkTestReport report,
            SceneManager sceneManager)
        {
            report.AddProperty(
                "configuration",
                "4 ms minimum build budget");
            report.AddMetric(
                "minimumBuildBudget",
                sceneManager.Settings.MinBuildTime * 1000.0,
                "ms");
        }

        public override void TearDown(SceneManager sceneManager)
        {
            sceneManager.Settings.MinBuildTime =
                _originalMinimumBuildTime;
        }
    }
}
```

Attach the new component to the benchmark GameObject, then drag it into the
`Tests` list. Multiple list entries may reference the same component instance.

### Test lifecycle

The hooks run in this order:

1. `SetUp` applies the configuration under test.
2. The runner optionally reloads the map, warms up for one lap, resets
   diagnostics, and measures the configured laps.
3. `AddReportData` runs after measurement. Add configuration values with
   `AddProperty` and numeric measurements with `AddMetric`.
4. The runner writes the JSON and text reports.
5. `TearDown` runs after reporting. It also runs when a partially initialized
   test fails, so cleanup must tolerate incomplete setup.

An exception in a test hook stops the suite and is logged as a benchmark error.
Do not silently replace invalid test data with defaults.

`TestName` defaults to the component type name. Override it when multiple
components of the same type represent different configurations.

## Reports

When `Save Diagnostics To File` is enabled, each test writes one JSON report and
one human-readable text report under:

```text
Application.persistentDataPath/MapStreamerBenchmarks
```

Reports from one suite share `suiteRunId` and include:

- iteration, sequence slot, run index, and total run count;
- Unity, operating-system, processor, graphics, quality, and display settings;
- route and camera progression configuration;
- measured and total duration, camera samples, and samples per second;
- average, P95, P99, and worst frame times;
- mesh, texture, and texture-cache diagnostics;
- custom test properties and metrics.

If the report shape changes, increment `schemaVersion`.

## Interpreting A/B results

For meaningful comparisons:

- use one executable containing all alternatives;
- use `FixedSamplesPerLap`;
- use a balanced order such as A/B/B/A and several iterations;
- compare runs with the same `suiteRunId`, schema version, sample count, route,
  and runtime configuration;
- inspect iteration-level pairs, not only the aggregate average.

The runner ends after the final camera sample; it does not currently wait for
all streaming queues to drain. Compare mesh, texture, and other workload counts
to detect materially different outstanding work before attributing a small
duration difference to the implementation.

## Guidance for repository agents

When extending the benchmark:

- keep workload generation in `MeshStreamingBenchmark` and test-specific
  configuration in `BenchmarkTest` subclasses;
- preserve the invariant that deterministic tests visit identical camera poses
  in identical order;
- avoid adding work to the measured per-frame path;
- report test-specific data through `BenchmarkTestReport` instead of adding
  temporary instrumentation to production hot paths;
- keep tests self-contained and restore changed global settings in `TearDown`;
- retain the `Saab.MapStreamer.Benchmark` namespace;
- update `schemaVersion` when the report shape changes.
