using OomiyaFes.Input;
using UnityEngine;

namespace OomiyaFes.Field
{
    public static class FieldCoordinateMapper
    {
        public const float FieldSizeMeters = 0.3f;

        public static Vector3 PythonToUnity(Vec3 position)
        {
            if (position == null)
            {
                return Vector3.zero;
            }

            return new Vector3(position.x, position.z, position.y);
        }

        public static Quaternion PythonEulerToUnity(Euler euler)
        {
            if (euler == null)
            {
                return Quaternion.identity;
            }

            return Quaternion.Euler(euler.pitch_y, euler.yaw_z, -euler.roll_x);
        }

        public static bool TryProjectToField(Vector3 origin, Vector3 direction, out Vector3 hit)
        {
            hit = Vector3.zero;
            if (Mathf.Abs(direction.y) < 0.0001f)
            {
                return false;
            }

            var t = -origin.y / direction.y;
            if (t < 0f)
            {
                return false;
            }

            hit = origin + direction * t;
            var half = FieldSizeMeters * 0.5f;
            return hit.x >= -half && hit.x <= half && hit.z >= -half && hit.z <= half;
        }
    }
}
