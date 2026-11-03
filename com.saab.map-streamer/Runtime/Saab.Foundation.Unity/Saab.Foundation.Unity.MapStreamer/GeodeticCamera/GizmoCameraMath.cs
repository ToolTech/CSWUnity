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
// File         : GizmoCameraMath.cs
// Module       :
// Description  : Completes the Gizmo operations used by camera navigation.
// Author       : Daniel Sahlin
//
// Revision History...
//
// Who  Date    Description
//
// DSA  260909  Created file
//
//******************************************************************************

using System;
using GizmoSDK.GizmoBase;

namespace Saab.Foundation.Unity.MapStreamer.GeodeticCamera
{
    internal static class GizmoCameraMath
    {
        private const float MinimumLengthSquared = 1e-12f;

        internal static Vec2 Zero2 => default;
        internal static Vec3 Zero3 => default;
        internal static Vec3 UnitX => new Vec3(1.0f, 0.0f, 0.0f);
        internal static Vec3 UnitY => new Vec3(0.0f, 1.0f, 0.0f);
        internal static Vec3 UnitZ => new Vec3(0.0f, 0.0f, 1.0f);
        internal static Quaternion Identity => new Quaternion(1.0f, 0.0f, 0.0f, 0.0f);

        internal static float Dot(Quaternion left, Quaternion right)
            => left.x * right.x + left.y * right.y + left.z * right.z + left.w * right.w;

        internal static Vec3 Transform(Vec3 value, Quaternion rotation)
        {
            float x2 = rotation.x + rotation.x;
            float y2 = rotation.y + rotation.y;
            float z2 = rotation.z + rotation.z;
            float wx2 = rotation.w * x2;
            float wy2 = rotation.w * y2;
            float wz2 = rotation.w * z2;
            float xx2 = rotation.x * x2;
            float xy2 = rotation.x * y2;
            float xz2 = rotation.x * z2;
            float yy2 = rotation.y * y2;
            float yz2 = rotation.y * z2;
            float zz2 = rotation.z * z2;
            return new Vec3(
                value.x * (1.0f - yy2 - zz2) + value.y * (xy2 - wz2) + value.z * (xz2 + wy2),
                value.x * (xy2 + wz2) + value.y * (1.0f - xx2 - zz2) + value.z * (yz2 - wx2),
                value.x * (xz2 - wy2) + value.y * (yz2 + wx2) + value.z * (1.0f - xx2 - yy2));
        }

        internal static Quaternion Multiply(Quaternion left, Quaternion right)
        {
            float x = left.x * right.w + right.x * left.w + left.y * right.z - left.z * right.y;
            float y = left.y * right.w + right.y * left.w + left.z * right.x - left.x * right.z;
            float z = left.z * right.w + right.z * left.w + left.x * right.y - left.y * right.x;
            float w = left.w * right.w - (left.x * right.x + left.y * right.y + left.z * right.z);
            return new Quaternion(w, x, y, z);
        }

        internal static Quaternion CreateFromAxisAngle(Vec3 axis, float angle)
        {
            float halfAngle = angle * 0.5f;
            float scale = (float)Math.Sin(halfAngle);
            return new Quaternion(
                (float)Math.Cos(halfAngle),
                axis.x * scale,
                axis.y * scale,
                axis.z * scale);
        }

        internal static Quaternion CreateLookRotation(Vec3 forward, Vec3 up)
        {
            if (!TryNormalize(forward, out forward))
                return Identity;

            if (!TryNormalize(up, out up)
                || !TryNormalize(Vec3.Cross(up, forward), out Vec3 right))
            {
                Vec3 cross = Vec3.Cross(UnitZ, forward);
                var rotation = new Quaternion(
                    1.0f + forward.z,
                    cross.x,
                    cross.y,
                    cross.z);
                return TryNormalize(rotation, out rotation)
                    ? rotation
                    : CreateFromAxisAngle(UnitY, (float)Math.PI);
            }

            Vec3 correctedUp = Vec3.Cross(forward, right);
            float trace = right.x + correctedUp.y + forward.z;
            if (trace > 0.0f)
            {
                float scale = 2.0f * (float)Math.Sqrt(trace + 1.0f);
                return Normalize(new Quaternion(
                    0.25f * scale,
                    (correctedUp.z - forward.y) / scale,
                    (forward.x - right.z) / scale,
                    (right.y - correctedUp.x) / scale));
            }
            if (right.x > correctedUp.y && right.x > forward.z)
            {
                float scale = 2.0f * (float)Math.Sqrt(1.0f + right.x - correctedUp.y - forward.z);
                return Normalize(new Quaternion(
                    (correctedUp.z - forward.y) / scale,
                    0.25f * scale,
                    (correctedUp.x + right.y) / scale,
                    (forward.x + right.z) / scale));
            }
            if (correctedUp.y > forward.z)
            {
                float scale = 2.0f * (float)Math.Sqrt(1.0f + correctedUp.y - right.x - forward.z);
                return Normalize(new Quaternion(
                    (forward.x - right.z) / scale,
                    (correctedUp.x + right.y) / scale,
                    0.25f * scale,
                    (forward.y + correctedUp.z) / scale));
            }

            float finalScale = 2.0f * (float)Math.Sqrt(1.0f + forward.z - right.x - correctedUp.y);
            return Normalize(new Quaternion(
                (right.y - correctedUp.x) / finalScale,
                (forward.x + right.z) / finalScale,
                (forward.y + correctedUp.z) / finalScale,
                0.25f * finalScale));
        }

        internal static Quaternion Normalize(Quaternion value)
        {
            float inverseLength = 1.0f / (float)Math.Sqrt(Dot(value, value));
            return new Quaternion(
                value.w * inverseLength,
                value.x * inverseLength,
                value.y * inverseLength,
                value.z * inverseLength);
        }

        internal static Quaternion Inverse(Quaternion value)
        {
            float inverseLengthSquared = 1.0f / Dot(value, value);
            return new Quaternion(
                value.w * inverseLengthSquared,
                -value.x * inverseLengthSquared,
                -value.y * inverseLengthSquared,
                -value.z * inverseLengthSquared);
        }

        internal static bool TryNormalize(Vec3 value, out Vec3 normalized)
        {
            normalized = default;
            float lengthSquared = value.LengthSq2();
            if (!IsFinite(lengthSquared) || lengthSquared <= MinimumLengthSquared)
                return false;

            normalized = value / (float)Math.Sqrt(lengthSquared);
            return true;
        }

        internal static bool TryNormalize(Quaternion value, out Quaternion normalized)
        {
            normalized = default;
            float lengthSquared = Dot(value, value);
            if (!IsFinite(lengthSquared) || lengthSquared <= MinimumLengthSquared)
                return false;

            normalized = Normalize(value);
            return true;
        }

        internal static bool IsFinite(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
