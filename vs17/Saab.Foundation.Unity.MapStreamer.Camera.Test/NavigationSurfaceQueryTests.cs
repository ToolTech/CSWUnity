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

using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;
using GizmoSDK.Coordinate;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Saab.Foundation.Map;
using Saab.Foundation.Unity.MapStreamer;
using Saab.Foundation.Unity.MapStreamer.GeodeticCamera;
using static Saab.Foundation.Unity.MapStreamer.GeodeticCamera.GizmoCameraMath;
using Vector2 = GizmoSDK.GizmoBase.Vec2;
using Vector3 = GizmoSDK.GizmoBase.Vec3;
using Quaternion = GizmoSDK.GizmoBase.Quaternion;

namespace Saab.Foundation.Unity.MapStreamer.Camera.Test
{
    [TestClass]
    [DoNotParallelize]
    public sealed class NavigationSurfaceQueryTests
    {
        [TestCleanup]
        public void ClearMap() => MapControl.SystemMap = null!;

        [TestMethod]
        public void GroundDrag_TerrainHit_ConvertsScreenCoordinatesAndShortCircuits()
        {
            var calls = new List<string>();
            var global = new Vec3D(1234567.25, 91, -2345678.5);
            var terrain = new MapPos
            {
                position = new Vec3D(12, 9, 23),
                ToGlobal = () => global,
                clampResult = IntersectQuery.INTERSECT
            };
            MapControl.SystemMap = new MapControl
            {
                Ray = (x, y, width, height) =>
                {
                    calls.Add("ray");
                    Assert.AreEqual(17, x);
                    Assert.AreEqual(557, y);
                    Assert.AreEqual(800u, width);
                    Assert.AreEqual(600u, height);
                    return (true, new Vec3D(10, 20, 30), new Vec3(2, -1, 3));
                },
                Terrain = (origin, direction, mask, flags) =>
                {
                    calls.Add("terrain");
                    Assert.AreEqual(new Vec3D(10, 20, 30), origin);
                    Assert.AreEqual(new Vec3(2, -1, 3), direction);
                    Assert.AreEqual(GroundClampType.GROUND, mask);
                    Assert.AreEqual(ClampFlags.FRUSTRUM_CULL, flags);
                    return (true, terrain);
                }
            };
            var query = new NavigationSurfaceQuery(() => (800, 600));

            Assert.IsTrue(query.TryGetGroundDragReference(17.75f, 42.25f, out Vec3D position));
            Assert.AreEqual(global, position);
            CollectionAssert.AreEqual(new[] { "ray", "terrain" }, calls);
        }

        [DataTestMethod]
        [DataRow(MapType.UTM, false)]
        [DataRow(MapType.UTM, true)]
        [DataRow(MapType.PROJ_SWEREF99, true)]
        public void GroundDrag_OnlySuccessfulTerrainMissUsesProjectedMapGlobalPlane(MapType mapType, bool queryWithoutIntersection)
        {
            var calls = new List<string>();
            MapControl.SystemMap = new MapControl
            {
                MapType = mapType,
                Terrain = (origin, direction, mask, flags) =>
                {
                    calls.Add("terrain");
                    Assert.AreEqual(new Vec3D(1000000, 100, 2000000), origin);
                    Assert.AreEqual(new Vec3(2, -1, 3), direction);
                    return (queryWithoutIntersection, new MapPos { clampResult = IntersectQuery.NULL });
                },
                Ray = (x, y, width, height) =>
                {
                    calls.Add("ray");
                    Assert.AreEqual(-2, x);
                    Assert.AreEqual(-1, y);
                    Assert.AreEqual(800u, width);
                    Assert.AreEqual(600u, height);
                    return (true, new Vec3D(1000000, 100, 2000000), new Vec3(2, -1, 3));
                }
            };
            var query = new NavigationSurfaceQuery(() => (800, 600));

            Assert.AreEqual(queryWithoutIntersection, query.TryGetGroundDragReference(-2.75f, 601.25f, out Vec3D position));
            Assert.AreEqual(queryWithoutIntersection ? new Vec3D(1000200, 0, 2000300) : default, position);
            CollectionAssert.AreEqual(new[] { "ray", "terrain" }, calls);
        }

        [TestMethod]
        public void GroundDrag_GeocentricFallback_ConvertsRayEndpointsAndReturnsMapGlobalEllipsoidHit()
        {
            var calls = new List<string>();
            var converted = new List<Vec3D>();
            MapControl.SystemMap = new MapControl
            {
                MapType = MapType.GEOCENTRIC,
                Terrain = (origin, direction, mask, flags) =>
                {
                    calls.Add("terrain");
                    return (true, new MapPos { clampResult = IntersectQuery.NULL });
                },
                Ray = (x, y, width, height) =>
                {
                    calls.Add("ray");
                    return (true, new Vec3D(1000, 2000, 3000), new Vec3(0, -1, 0));
                },
                ToWorld = position =>
                {
                    calls.Add("to-world");
                    converted.Add(position);
                    return (true, new CartPos(6378637 + (position.y - 2000) * 2,
                        (position.z - 3000) * 3, (position.x - 1000) * 4));
                },
                PositionFromWorld = (position, cart) =>
                {
                    calls.Add("to-global");
                    Assert.IsNull(position.node);
                    Assert.AreEqual(6378137, cart.X, 0.000001);
                    Assert.AreEqual(0, cart.Y, 0.000001);
                    Assert.AreEqual(0, cart.Z, 0.000001);
                    position.position = new Vec3D(1000 + cart.Z / 4, 2000 + (cart.X - 6378637) / 2, 3000 + cart.Y / 3);
                    return true;
                }
            };
            var query = new NavigationSurfaceQuery(() => (800, 600));

            Assert.IsTrue(query.TryGetGroundDragReference(10, 20, out Vec3D position));
            Assert.AreEqual(1000, position.x, 0.000001);
            Assert.AreEqual(1750, position.y, 0.000001);
            Assert.AreEqual(3000, position.z, 0.000001);
            CollectionAssert.AreEqual(new[] { new Vec3D(1000, 2000, 3000), new Vec3D(1000, 1999, 3000) }, converted);
            CollectionAssert.AreEqual(new[] { "ray", "terrain", "to-world", "to-world", "to-global" }, calls);
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        public void Zoom_UsesExactPointerThenViewportCenterFallbackOrder(int winningQuery)
        {
            var calls = new List<string>();
            MapControl.SystemMap = new MapControl
            {
                Terrain = (origin, direction, mask, flags) =>
                {
                    bool center = origin.x == 300;
                    calls.Add(center ? "center-terrain" : "pointer-terrain");
                    bool hit = winningQuery == (center ? 2 : 0);
                    return (true, new MapPos
                    {
                        position = center ? new Vec3D(4, 5, 6) : new Vec3D(1, 2, 3),
                        clampResult = hit ? IntersectQuery.INTERSECT : IntersectQuery.NULL
                    });
                },
                Ray = (x, y, width, height) =>
                {
                    bool center = x == 400;
                    Assert.AreEqual(center ? 300 : 580, y);
                    Assert.AreEqual(center ? 400 : 10, x);
                    calls.Add(center ? "center-ray" : "pointer-ray");
                    return (true, center ? new Vec3D(300, 100, 400) : new Vec3D(100, 100, 200),
                        new Vec3(0, winningQuery == (center ? 3 : 1) ? -1 : 1, 0));
                }
            };
            var query = new NavigationSurfaceQuery(() => (800, 600));

            Assert.AreEqual(winningQuery != 4, query.TryGetZoomReference(10, 20, out Vec3D position));
            var expected = new[] { new Vec3D(1, 2, 3), new Vec3D(100, 0, 200), new Vec3D(4, 5, 6),
                new Vec3D(300, 0, 400), default(Vec3D) };
            Assert.AreEqual(expected[winningQuery], position);
            CollectionAssert.AreEqual(new[] { "pointer-ray", "pointer-terrain", "center-ray", "center-terrain" }
                .Take(winningQuery <= 1 ? 2 : 4).ToArray(), calls);
        }

        [DataTestMethod]
        [DataRow(false, true)]
        [DataRow(true, true)]
        [DataRow(false, false)]
        [DataRow(true, false)]
        public void OrbitPolicies_QueryTerrainOnly_AndPreserveGlobalVersusLocalResults(bool center, bool hit)
        {
            var terrain = new MapPos
            {
                position = new Vec3D(1000000.25, 500, -2000000.5),
                node = new Node(), clampResult = hit ? IntersectQuery.INTERSECT : IntersectQuery.NULL
            };
            int queries = 0;
            MapControl.SystemMap = new MapControl
            {
                MapType = MapType.GEOCENTRIC,
                Ray = (x, y, width, height) =>
                {
                    Assert.AreEqual(center ? 400 : 10, x);
                    Assert.AreEqual(center ? 300 : 580, y);
                    return (true, new Vec3D(1, 2, 3), new Vec3(0, -1, 0));
                },
                Terrain = (origin, direction, mask, flags) =>
                {
                    queries++;
                    return (true, terrain);
                }
            };
            var query = new NavigationSurfaceQuery(() => (800, 600));
            NavigationQueryResult result;
            if (center)
            {
                result = query.GetViewportCenterOrbitTerrain(400, 300, out MapPos selected);
                if (hit) Assert.AreSame(terrain, selected);
            }
            else
            {
                result = query.GetPointerOrbitPivot(10, 20, out Vec3D selected);
                Assert.AreEqual(hit ? terrain.position : default, selected);
            }
            Assert.AreEqual(hit ? NavigationQueryResult.Hit : NavigationQueryResult.Miss, result);
            Assert.AreEqual(1, queries);
        }

        [TestMethod]
        public void MapRay_UsesCurrentMapAndViewport_WithoutPickingOrNormalizing()
        {
            (int Width, int Height) viewport = (800, 600);
            var query = new NavigationSurfaceQuery(() => viewport);
            for (int i = 0; i < 2; i++)
            {
                Vec3D expectedOrigin = new Vec3D(1000000.25 + i, 2000000.5, 3000000.75);
                Vec3 expectedDirection = new Vec3(2, -3, 4);
                MapControl.SystemMap = new MapControl
                {
                    MapType = MapType.PLAIN,
                    Ray = (x, y, width, height) =>
                    {
                        Assert.AreEqual(-9, x);
                        Assert.AreEqual(i == 0 ? 98 : 266, y);
                        Assert.AreEqual((uint)viewport.Width, width);
                        Assert.AreEqual((uint)viewport.Height, height);
                        return (true, expectedOrigin, expectedDirection);
                    }
                };
                Assert.IsTrue(query.TryGetMapRay(-9.75f, 501.25f, out Vec3D origin, out Vec3 direction));
                Assert.AreEqual(expectedOrigin, origin);
                Assert.AreEqual(expectedDirection, direction);
                viewport = (1024, 768);
            }
        }

        [DataTestMethod]
        [DataRow("map")]
        [DataRow("width-zero")]
        [DataRow("height-zero")]
        [DataRow("nan-input")]
        [DataRow("infinite-input")]
        public void InvalidEnvironmentOrInput_RejectsEveryPolicyWithoutCallingNativeQueries(string failure)
        {
            int calls = 0;
            MapControl.SystemMap = failure == "map" ? null! : new MapControl
            {
                Terrain = (origin, direction, mask, flags) =>
                {
                    calls++;
                    return (true, new MapPos { clampResult = IntersectQuery.INTERSECT });
                },
                Ray = (x, y, width, height) =>
                {
                    calls++;
                    return (true, new Vec3D(0, 10, 0), new Vec3(0, -1, 0));
                }
            };
            int width = failure == "width-zero" ? 0 : failure == "width-negative" ? -1 : 800;
            int height = failure == "height-zero" ? 0 : failure == "height-negative" ? -1 : 600;
            float x = failure == "nan-input" ? float.NaN : 10;
            float y = failure == "infinite-input" ? float.PositiveInfinity : 20;
            var query = new NavigationSurfaceQuery(() => (width, height));

            Assert.AreEqual(failure.EndsWith("input"), query.IsAvailable);
            Assert.IsFalse(query.TryGetGroundDragReference(x, y, out Vec3D ground));
            Assert.IsFalse(query.TryGetZoomReference(x, y, out Vec3D zoom));
            Assert.AreEqual(NavigationQueryResult.Invalid, query.GetPointerOrbitPivot(x, y, out Vec3D pivot));
            Assert.AreEqual(NavigationQueryResult.Invalid, query.GetViewportCenterOrbitTerrain(x, y, out MapPos terrain));
            Assert.IsFalse(query.TryGetMapRay(x, y, out Vec3D origin, out Vec3 direction));
            Assert.AreEqual(default(Vec3D), ground);
            Assert.AreEqual(default(Vec3D), zoom);
            Assert.AreEqual(default(Vec3D), pivot);
            Assert.IsNull(terrain);
            Assert.AreEqual(default(Vec3D), origin);
            Assert.AreEqual(default(Vec3), direction);
            Assert.AreEqual(0, calls);
        }

        [DataTestMethod]
        [DataRow("x", double.NaN)]
        [DataRow("y", double.PositiveInfinity)]
        [DataRow("z", double.NegativeInfinity)]
        [DataRow("null", 0.0)]
        public void InvalidTerrainResult_AbortsInsteadOfUsingMathematicalOrOrbitFallback(string axis, double value)
        {
            Vec3D invalid = new Vec3D(axis == "x" ? value : 1, axis == "y" ? value : 2, axis == "z" ? value : 3);
            int terrainQueries = 0;
            int rayQueries = 0;
            MapControl.SystemMap = new MapControl
            {
                Terrain = (origin, direction, mask, flags) =>
                {
                    terrainQueries++;
                    return (true, axis == "null" ? null! : new MapPos
                    {
                        position = invalid, clampResult = IntersectQuery.INTERSECT
                    });
                },
                Ray = (x, y, width, height) =>
                {
                    rayQueries++;
                    return (true, new Vec3D(0, 10, 0), new Vec3(0, -1, 0));
                }
            };
            var query = new NavigationSurfaceQuery(() => (800, 600));

            Assert.IsFalse(query.TryGetGroundDragReference(10, 20, out Vec3D ground));
            Assert.IsFalse(query.TryGetZoomReference(10, 20, out Vec3D zoom));
            Assert.AreEqual(NavigationQueryResult.Invalid, query.GetPointerOrbitPivot(10, 20, out Vec3D orbit));
            Assert.AreEqual(NavigationQueryResult.Invalid, query.GetViewportCenterOrbitTerrain(400, 300, out MapPos center));
            Assert.AreEqual(default(Vec3D), ground);
            Assert.AreEqual(default(Vec3D), zoom);
            Assert.AreEqual(default(Vec3D), orbit);
            Assert.IsNull(center);
            Assert.AreEqual(4, terrainQueries);
            Assert.AreEqual(4, rayQueries);
        }

        [DataTestMethod]
        [DataRow(MapType.UTM, "origin-x", double.NaN)]
        [DataRow(MapType.PROJ_SWEREF99, "origin-y", double.PositiveInfinity)]
        [DataRow(MapType.GEOCENTRIC, "origin-z", double.NegativeInfinity)]
        [DataRow(MapType.GEOCENTRIC, "direction-x", double.NaN)]
        [DataRow(MapType.UTM, "direction-y", double.PositiveInfinity)]
        [DataRow(MapType.PROJ_SWEREF99, "direction-z", double.NegativeInfinity)]
        [DataRow(MapType.UTM, "degenerate", 0.0)]
        [DataRow(MapType.GEOCENTRIC, "degenerate", 0.0000001)]
        public void InvalidMapRay_AbortsEveryPolicyBeforeNativeTerrain(
            MapType mapType, string failure, double value)
        {
            var calls = new List<string>();
            MapControl.SystemMap = new MapControl
            {
                MapType = mapType,
                Terrain = (origin, direction, mask, flags) =>
                {
                    calls.Add("terrain");
                    return (true, new MapPos { clampResult = IntersectQuery.INTERSECT });
                },
                Ray = (x, y, width, height) =>
                {
                    calls.Add("ray");
                    if (failure == "degenerate")
                        return (true, new Vec3D(0, 100, 0), new Vec3((float)value, 0, 0));
                    return (true,
                        new Vec3D(failure == "origin-x" ? value : 0, failure == "origin-y" ? value : 100,
                            failure == "origin-z" ? value : 0),
                        new Vec3(failure == "direction-x" ? (float)value : 0,
                            failure == "direction-y" ? (float)value : -1, failure == "direction-z" ? (float)value : 0));
                }
            };
            var query = new NavigationSurfaceQuery(() => (800, 600));
            Assert.IsFalse(query.TryGetZoomReference(10, 20, out Vec3D zoom));
            Assert.AreEqual(default(Vec3D), zoom);
            CollectionAssert.AreEqual(new[] { "ray" }, calls);
            Assert.IsFalse(query.TryGetGroundDragReference(10, 20, out Vec3D ground));
            Assert.AreEqual(default(Vec3D), ground);
            Assert.AreEqual(NavigationQueryResult.Invalid, query.GetPointerOrbitPivot(10, 20, out Vec3D pointer));
            Assert.AreEqual(default(Vec3D), pointer);
            Assert.AreEqual(NavigationQueryResult.Invalid, query.GetViewportCenterOrbitTerrain(400, 300, out MapPos center));
            Assert.IsNull(center);
            Assert.IsFalse(query.TryGetMapRay(10, 20, out Vec3D origin, out Vec3 direction));
            Assert.AreEqual(default(Vec3D), origin);
            Assert.AreEqual(default(Vec3), direction);
            CollectionAssert.AreEqual(Enumerable.Repeat("ray", 5).ToArray(), calls);
        }

        [TestMethod]
        public void SmallNonDegenerateRay_ReachesTerrainWithoutNormalization()
        {
            var direction = new Vec3(0.000002f, 0, 0);
            MapControl.SystemMap = new MapControl
            {
                Ray = (x, y, width, height) => (true, new Vec3D(0, 0, 0), direction),
                Terrain = (origin, rayDirection, mask, flags) =>
                {
                    Assert.AreEqual(direction, rayDirection);
                    return (true, new MapPos { position = new Vec3D(10, 20, 30), clampResult = IntersectQuery.INTERSECT });
                }
            };
            var query = new NavigationSurfaceQuery(() => (800, 600));
            Assert.AreEqual(NavigationQueryResult.Hit, query.GetPointerOrbitPivot(10, 20, out Vec3D point));
            Assert.AreEqual(new Vec3D(10, 20, 30), point);
        }

        [DataTestMethod]
        [DataRow("projected-overflow", 0)]
        [DataRow("ecef-origin", 1)]
        [DataRow("ecef-end", 2)]
        [DataRow("ecef-direction", 2)]
        [DataRow("global-nan", 3)]
        [DataRow("global-infinity", 3)]
        public void InvalidMathematicalResult_AbortsWithoutCenterFallback(string failure, int conversionCalls)
        {
            var calls = new List<string>();
            int worldQueries = 0;
            MapControl.SystemMap = new MapControl
            {
                MapType = failure == "projected-overflow" ? MapType.UTM : MapType.GEOCENTRIC,
                Terrain = (origin, direction, mask, flags) =>
                {
                    calls.Add("terrain");
                    return (true, new MapPos { clampResult = IntersectQuery.NULL });
                },
                Ray = (x, y, width, height) =>
                {
                    calls.Add("ray");
                    return failure == "projected-overflow"
                        ? (true, new Vec3D(0, double.MaxValue, 0), new Vec3(2, -1, 0))
                        : (true, new Vec3D(0, 0, 0), new Vec3(1, 0, 0));
                },
                ToWorld = position =>
                {
                    calls.Add("world");
                    worldQueries++;
                    if (failure == "ecef-origin") return (true, new CartPos(double.NaN, 0, 0));
                    if (failure == "ecef-end" && worldQueries == 2)
                        return (true, new CartPos(6378236, double.PositiveInfinity, 0));
                    if (failure == "ecef-direction")
                        return (true, new CartPos(worldQueries == 1 ? double.MaxValue : -double.MaxValue, 0, 0));
                    return (true, new CartPos(6378238 - worldQueries, 0, 0));
                },
                PositionFromWorld = (position, cart) =>
                {
                    calls.Add("global");
                    position.position = new Vec3D(0, failure == "global-nan" ? double.NaN : double.NegativeInfinity, 0);
                    return true;
                }
            };
            var query = new NavigationSurfaceQuery(() => (800, 600));

            Assert.IsFalse(query.TryGetZoomReference(10, 20, out Vec3D position));
            Assert.AreEqual(default(Vec3D), position);
            string[] expected = new[] { "ray", "terrain", "world", "world", "global" };
            CollectionAssert.AreEqual(expected.Take(2 + conversionCalls).ToArray(), calls);
        }

        [DataTestMethod]
        [DataRow(OrbitPivotMode.PointerSurface, "nonfinite")]
        [DataRow(OrbitPivotMode.ViewportCenter, "nonfinite")]
        [DataRow(OrbitPivotMode.PointerSurface, "native-ray")]
        [DataRow(OrbitPivotMode.ViewportCenter, "native-ray")]
        [DataRow(OrbitPivotMode.PointerSurface, "native-terrain")]
        [DataRow(OrbitPivotMode.ViewportCenter, "native-terrain")]
        [DataRow(OrbitPivotMode.PointerSurface, "missing-map")]
        [DataRow(OrbitPivotMode.ViewportCenter, "missing-map")]
        [DataRow(OrbitPivotMode.PointerSurface, "viewport-zero")]
        [DataRow(OrbitPivotMode.ViewportCenter, "viewport-zero")]
        public void InvalidQueryOrContext_EndsOrbitWithoutRetainedPivotOrForwardFallback(OrbitPivotMode mode, string failure)
        {
            var terrain = new MapPos { position = new Vec3D(0, -100, -100), clampResult = IntersectQuery.INTERSECT };
            bool fail = false;
            int width = 800;
            int rayQueries = 0;
            int terrainQueries = 0;
            var map = new MapControl
            {
                Ray = (x, y, viewportWidth, height) =>
                {
                    rayQueries++;
                    return (!(fail && failure == "native-ray"), new Vec3D(0, 0, 0), new Vec3(0, -1, 1));
                },
                Terrain = (origin, direction, mask, flags) =>
                {
                    terrainQueries++;
                    return (!(fail && failure == "native-terrain"), terrain);
                }
            };
            MapControl.SystemMap = map;
            var query = new NavigationSurfaceQuery(() => (width, 600));
            Vec3D camera = default;
            Quaternion rotation = Identity;
            int applications = 0;
            int forwardFallbacks = 0;
            bool Frame(out OrbitGesture.Frame frame)
            {
                frame = new OrbitGesture.Frame
                {
                    WorldPosition = new Vec3D(camera.x, camera.y, -camera.z), Orientation = rotation,
                    East = UnitX, North = UnitZ, Up = UnitY
                };
                return query.IsAvailable;
            }
            NavigationQueryResult Pick(OrbitPivotMode selectedMode, Vector2 screen, out Vec3D position)
            {
                if (selectedMode == OrbitPivotMode.PointerSurface)
                {
                    NavigationQueryResult result = query.GetPointerOrbitPivot(screen.x, screen.y, out Vec3D global);
                    position = new Vec3D(global.x, global.y, -global.z);
                    return result;
                }
                NavigationQueryResult center = query.GetViewportCenterOrbitTerrain(screen.x, screen.y, out MapPos selected);
                position = center != NavigationQueryResult.Hit ? default
                    : new Vec3D(selected.position.x - camera.x, selected.position.y - camera.y, camera.z - selected.position.z);
                return center;
            }
            bool Resolve(Vector3 offset, out Vec3D position, out Vector3 up)
            {
                position = new Vec3D(camera.x + offset.x, camera.y + offset.y, camera.z - offset.z);
                up = UnitY;
                return true;
            }
            bool Prepare(Vec3D? resolved, Vector3 offset, Quaternion orientation, out Action apply)
            {
                Resolve(offset, out Vec3D candidate, out _);
                apply = () => { camera = resolved ?? candidate; rotation = orientation; applications++; };
                return true;
            }
            var gesture = new OrbitGesture(Frame, Pick,
                (float distance, out Vector3 point) => { forwardFallbacks++; point = UnitZ * distance; return true; }, Resolve,
                CreateLookRotation, Prepare, faceCenter: true);
            gesture.SetPivotMode(mode);
            gesture.Begin(Zero2, 800, 600);
            gesture.Rotate(MathF.PI / 36, 0);
            Assert.IsTrue(applications > 0, "The query must first establish an active orbit.");
            Vec3D acceptedPosition = camera;
            Quaternion acceptedRotation = rotation;
            int acceptedApplications = applications;

            fail = true;
            if (failure == "nonfinite") terrain.position = new Vec3D(double.NaN, 0, 0);
            if (failure == "missing-map") MapControl.SystemMap = null!;
            if (failure == "viewport-zero") width = 0;
            bool contextFailure = failure == "missing-map" || failure == "viewport-zero";
            Assert.AreEqual(!contextFailure, query.IsAvailable);
            if (contextFailure)
                gesture.Rotate(MathF.PI / 36, 0);
            else
                gesture.Begin(new Vector2(80, 0), width, 600);
            Assert.AreEqual(contextFailure ? 1 : 2, rayQueries);
            Assert.AreEqual(contextFailure || failure == "native-ray" ? 1 : 2, terrainQueries);

            fail = false;
            MapControl.SystemMap = map;
            width = 800;
            Assert.IsTrue(query.IsAvailable);
            gesture.Rotate(MathF.PI / 36, 0);

            Assert.AreEqual(acceptedApplications, applications);
            Assert.AreEqual(acceptedPosition, camera);
            Assert.AreEqual(acceptedRotation, rotation);
            Assert.AreEqual(0, forwardFallbacks);

            terrain.clampResult = IntersectQuery.NULL;
            gesture.Begin(new Vector2(160, 0), 800, 600);
            gesture.Rotate(MathF.PI / 36, 0);
            Assert.IsTrue(applications > acceptedApplications, "A later ordinary miss retains the mode's existing fallback.");
            Assert.AreEqual(mode == OrbitPivotMode.PointerSurface ? 0 : 1, forwardFallbacks);
        }

        [DataTestMethod]
        [DataRow(MapType.UNKNOWN)]
        [DataRow(MapType.PLAIN)]
        public void MapsWithoutMathematicalModel_StillUseTerrainButNeverInventAFallback(MapType mapType)
        {
            var calls = new List<double>();
            MapControl.SystemMap = new MapControl
            {
                MapType = mapType,
                Ray = (x, y, width, height) => (true, new Vec3D(x, 0, y), new Vec3(0, -1, 0)),
                Terrain = (origin, direction, mask, flags) =>
                {
                    calls.Add(origin.x);
                    return (true, new MapPos
                    {
                        position = new Vec3D(1, 2, 3),
                        clampResult = origin.x == 400 ? IntersectQuery.INTERSECT : IntersectQuery.NULL
                    });
                }
            };
            var query = new NavigationSurfaceQuery(() => (800, 600));

            Assert.IsFalse(query.TryGetGroundDragReference(10, 20, out Vec3D ground));
            Assert.AreEqual(default(Vec3D), ground);
            Assert.IsTrue(query.TryGetZoomReference(10, 20, out Vec3D zoom));
            Assert.AreEqual(new Vec3D(1, 2, 3), zoom);
            CollectionAssert.AreEqual(new[] { 10.0, 10.0, 400.0 }, calls);
        }

        [DataTestMethod]
        [DataRow("ray", 0, false)]
        [DataRow("terrain", 0, false)]
        [DataRow("origin-conversion", 1, false)]
        [DataRow("end-conversion", 2, false)]
        [DataRow("intersection", 2, false)]
        [DataRow("global-conversion", 2, true)]
        public void NativeFailuresAbortZoom_OnlyIntersectionMissCanUseCenter(
            string failure, int worldCalls, bool setsPosition)
        {
            int queries = 0;
            var calls = new List<string>();
            MapControl.SystemMap = new MapControl
            {
                MapType = MapType.GEOCENTRIC,
                Terrain = (origin, direction, mask, flags) =>
                {
                    calls.Add(origin.x == 400 ? "center-terrain" : "pointer-terrain");
                    return (failure != "terrain", new MapPos
                    {
                        position = new Vec3D(1, 2, 3),
                        clampResult = origin.x == 400 ? IntersectQuery.INTERSECT : IntersectQuery.NULL
                    });
                },
                Ray = (x, y, width, height) =>
                {
                    calls.Add(x == 400 ? "center-ray" : "ray");
                    return (failure != "ray", new Vec3D(x, 0, 0), new Vec3(1, 0, 0));
                },
                ToWorld = position =>
                {
                    calls.Add("world");
                    queries++;
                    bool success = !(failure == "origin-conversion" && queries == 1)
                        && !(failure == "end-conversion" && queries == 2);
                    return (success, new CartPos(6378237 + (failure == "intersection" ? queries : -queries), 0, 0));
                },
                PositionFromWorld = (position, cart) => { calls.Add("global"); return false; }
            };
            var query = new NavigationSurfaceQuery(() => (800, 600));

            bool miss = failure == "intersection";
            Assert.AreEqual(miss, query.TryGetZoomReference(10, 20, out Vec3D position));
            Assert.AreEqual(miss ? new Vec3D(1, 2, 3) : default, position);
            var expected = new List<string> { "ray" };
            if (failure != "ray") expected.Add("pointer-terrain");
            expected.AddRange(Enumerable.Repeat("world", worldCalls));
            if (setsPosition) expected.Add("global");
            if (miss) expected.AddRange(new[] { "center-ray", "center-terrain" });
            CollectionAssert.AreEqual(expected, calls);
        }

        [DataTestMethod]
        [DataRow(0.0, false)]
        [DataRow(0.0000001, false)]
        [DataRow(0.000001, false)]
        [DataRow(0.000002, true)]
        [DataRow(1e200, false)]
        public void ConvertedEcefDirection_OnlyValidNonIntersectionCanReachZoomCenter(double endpointDelta, bool validMiss)
        {
            var calls = new List<string>();
            MapControl.SystemMap = new MapControl
            {
                MapType = MapType.GEOCENTRIC,
                Ray = (x, y, width, height) =>
                {
                    calls.Add(x == 400 ? "center-ray" : "pointer-ray");
                    return (true, new Vec3D(x, 0, 0), new Vec3(1, 0, 0));
                },
                Terrain = (origin, direction, mask, flags) =>
                {
                    bool center = origin.x == 400;
                    calls.Add(center ? "center-terrain" : "pointer-terrain");
                    return (true, new MapPos
                    {
                        position = new Vec3D(1, 2, 3),
                        clampResult = center ? IntersectQuery.INTERSECT : IntersectQuery.NULL
                    });
                },
                ToWorld = position =>
                {
                    calls.Add("world");
                    return (true, new CartPos(6378237, (position.x - 10) * endpointDelta, 0));
                }
            };
            var query = new NavigationSurfaceQuery(() => (800, 600));

            Assert.AreEqual(validMiss, query.TryGetZoomReference(10, 20, out Vec3D point));
            Assert.AreEqual(validMiss ? new Vec3D(1, 2, 3) : default, point);
            var expected = new List<string> { "pointer-ray", "pointer-terrain", "world", "world" };
            if (validMiss) expected.AddRange(new[] { "center-ray", "center-terrain" });
            CollectionAssert.AreEqual(expected, calls);
        }

        [DataTestMethod]
        [DataRow(0.0, -1.0, true)]
        [DataRow(-1.0, -1.0, false)]
        [DataRow(100.0, 1.0, false)]
        [DataRow(100.0, 0.0, false)]
        public void ProjectedIntersection_PreservesZeroDistanceAndForwardRayRules(double height, double directionY, bool hit)
        {
            MapControl.SystemMap = new MapControl
            {
                Terrain = (origin, direction, mask, flags) => (true, new MapPos { clampResult = IntersectQuery.NULL }),
                Ray = (x, y, width, viewportHeight) => (true, new Vec3D(123, height, 456), new Vec3(0, (float)directionY, 0))
            };
            var query = new NavigationSurfaceQuery(() => (800, 600));

            Assert.AreEqual(hit, query.TryGetGroundDragReference(10, 20, out Vec3D position));
            Assert.AreEqual(hit ? new Vec3D(123, 0, 456) : default, position);
        }

        [TestMethod]
        public void EllipsoidConversion_ReusesScratchWithoutStaleNodeOrFailedPositionAcrossMapChanges()
        {
            var query = new NavigationSurfaceQuery(() => (800, 600));
            MapPos? scratch = null;
            for (int i = 0; i < 3; i++)
            {
                MapControl.SystemMap = new MapControl
                {
                    MapType = MapType.GEOCENTRIC,
                    Terrain = (origin, direction, mask, flags) => (true, new MapPos { clampResult = IntersectQuery.NULL }),
                    Ray = (x, y, width, height) => (true, new Vec3D(0, 0, 0), new Vec3(1, 0, 0)),
                    ToWorld = position => (true, new CartPos(6378237 - position.x, 0, 0)),
                    PositionFromWorld = (position, cart) =>
                    {
                        if (scratch != null) Assert.AreSame(scratch, position);
                        scratch = position;
                        Assert.IsNull(position.node);
                        position.node = new Node();
                        position.position = new Vec3D(i + 1, i + 2, i + 3);
                        return i != 1;
                    }
                };
                Assert.AreEqual(i != 1, query.TryGetGroundDragReference(10, 20, out Vec3D position));
                Assert.AreEqual(i != 1 ? new Vec3D(i + 1, i + 2, i + 3) : default, position);
            }
        }
    }
}
