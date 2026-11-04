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
using Saab.Foundation.Map;
using static Saab.Foundation.Unity.MapStreamer.GeodeticCamera.GizmoCameraMath;

namespace Saab.Foundation.Unity.MapStreamer.GeodeticCamera
{
    // Extends the existing native-boundary fixture. Public movement dispatch,
    // surface queries, gestures and accepted-reference state are production code.
    public partial class GeodeticCameraControl
    {
        internal bool PositionApplicationAllowed = true;
        internal bool DampedPan;
        private bool DampenPan => DampedPan;
        private CameraZoomGesture? _zoomGesture;
        private CameraZoomGesture ZoomGesture => _zoomGesture ??= new(
            NavigationSurfaces.TryGetZoomReference, () => Position, ApplyMovementPosition);
        private OrbitGesture? _orbitGesture;
        private OrbitGesture OrbitGesture => _orbitGesture ??= new(
            ReadFrame, QueryOrbitTerrain, TryPointAlongView, Resolve,
            CreateLookRotation, Prepare, faceCenter: true);
        private GroundDragGesture? _groundDragGesture;
        private GroundDragGesture GroundDragGesture => _groundDragGesture ??= new(
            ReadGroundDragMap, NavigationSurfaces.TryGetGroundDragReference,
            NavigationSurfaces.TryGetMapRay,
            (Vec3D _, out GroundDragGesture.TangentFrame frame) =>
            {
                frame = default;
                throw new InvalidOperationException("This boundary fixture supplies projected coordinates only.");
            },
            (float _, float _, Vec3 _, Vec3 _, out Vec3 point, out float incidence) =>
            {
                point = default;
                incidence = default;
                throw new InvalidOperationException("Projected drag must use the production projected-plane geometry.");
            },
            (mapType, position, east, north) => ApplyPan(mapType, position, east, north), DampenPan);

        private bool ReadPanOrigin(out MapType mapType, out Vec3D position)
        {
            position = Position;
            mapType = MapControl.SystemMap?.MapType ?? MapType.UNKNOWN;
            return MapControl.SystemMap != null;
        }

        private bool ReadGroundDragMap(bool beginning, out MapType mapType, out Vec3D position)
        {
            return ReadPanOrigin(out mapType, out position) && NavigationSurfaces.IsAvailable;
        }

        private bool ApplyPan(MapType mapType, Vec3D position, float east, float north)
        {
            if (mapType == MapType.GEOCENTRIC)
                throw new InvalidOperationException("Native ellipsoid conversion belongs to its existing Adapter tests.");

            return ApplyMovementPosition(position);
        }

        private bool ApplyMovementPosition(Vec3D position)
        {
            if (!PositionApplicationAllowed)
                return false;

            Position = position;
            Applications++;
            return true;
        }
    }
}
