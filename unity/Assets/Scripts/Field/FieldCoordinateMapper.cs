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

        public static Vector3 PythonDirectionToUnity(Vec3 direction)
        {
            return PythonToUnity(direction).normalized;
        }

        public static bool TryMarkerBasisToUnityRotation(TrackingMessage message, out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            if (message?.marker_y_axis_field == null || message.marker_z_axis_field == null)
            {
                return false;
            }

            var up = PythonDirectionToUnity(message.marker_y_axis_field);
            var forward = PythonDirectionToUnity(message.marker_z_axis_field);
            if (up.sqrMagnitude < 0.0001f || forward.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            rotation = Quaternion.LookRotation(forward, up);
            return true;
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
