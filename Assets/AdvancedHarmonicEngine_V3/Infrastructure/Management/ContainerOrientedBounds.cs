using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    /// <summary>World ↔ container-local transforms for an open-top cylinder (floor at local y=0).</summary>
    public static class ContainerOrientedBounds
    {
        public static Matrix4x4 BuildLocalToWorld(Vector3 floorPivotWorld, Quaternion worldRotation)
        {
            return Matrix4x4.TRS(floorPivotWorld, worldRotation, Vector3.one);
        }

        public static Matrix4x4 BuildWorldToLocal(Vector3 floorPivotWorld, Quaternion worldRotation)
        {
            return BuildLocalToWorld(floorPivotWorld, worldRotation).inverse;
        }

        public static Vector3 WorldToLocal(Vector3 worldPos, Matrix4x4 worldToLocal)
        {
            return worldToLocal.MultiplyPoint3x4(worldPos);
        }

        public static bool IsBelowRim(Vector3 localPos, float height)
        {
            return localPos.y <= height;
        }
    }
}
