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
// Information Class:  COMPANY UNCLASSIFIED
// Defence Secrecy:     NOT CLASSIFIED
// Export Control:      NOT EXPORT CONTROLLED
//
//
// File         : ICameraPose.cs
// Module       :
// Description  : Defines direct camera pose application independent of navigation.
// Author       : Daniel Sahlin
//
// Revision History...
//
// Who      Date    Description
//
// DSA      261023  Created file
//
//******************************************************************************

#nullable enable

using System;
using GizmoSDK.GizmoBase;

namespace Saab.Foundation.Unity.MapStreamer.GeodeticCamera
{
    /// <summary>
    /// Direct poses bypass navigation limits and preserve roll.
    /// Calls require the camera-owning thread; accepted rotations are normalized.
    /// </summary>
    public interface ICameraPose
    {
        /// <summary>
        /// Raised synchronously after successful explicit pose application and
        /// invalidation of retained navigation references. Failure raises no event.
        /// </summary>
        event Action? PoseApplied;

        /// <summary>
        /// Raised after map-relative retained navigation references are invalidated.
        /// Consumers discard saved targets for the previous map.
        /// </summary>
        event Action? MapInvalidated;

        /// <summary>
        /// Applies the whole pose before notifying observers.
        /// </summary>
        /// <param name="position">Position in the active map's global coordinates, in metres.</param>
        /// <param name="orientation">Camera-world rotation, not ENU-local.</param>
        /// <returns>
        /// False for invalid values or unavailable coordinate/basis conversion; the pose is unchanged.
        /// </returns>
        bool TrySetPose(Vec3D position, Quaternion orientation);
    }
}
