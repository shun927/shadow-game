using System;

namespace OomiyaFes.Input
{
    [Serializable]
    public class TrackingMessage
    {
        public long timestamp_ms;
        public int id;
        public Vec3 field_xyz_m;
        public Vec3 raw_field_xyz_m;
        public Euler field_euler_zyx_deg;
        public string[] visible_cameras;
        public string selected_camera;
        public bool rejected_jump;
    }

    [Serializable]
    public class Vec3
    {
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public class Euler
    {
        public float roll_x;
        public float pitch_y;
        public float yaw_z;
    }
}
