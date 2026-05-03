using System;

namespace OomiyaFes.Input
{
    [Serializable]
    public class TrackedMarkerMessage
    {
        public long timestamp_ms;
        public int id;
        public Vec3 field_xyz_m;
        public Vec3 raw_field_xyz_m;
        public Euler field_euler_zyx_deg;
        public Vec3 marker_x_axis_field;
        public Vec3 marker_y_axis_field;
        public Vec3 marker_z_axis_field;
    }

    [Serializable]
    public class TrackingMessage : TrackedMarkerMessage
    {
        public string[] visible_cameras;
        public string selected_camera;
        public bool rejected_jump;
        public string tracking_mode;
        public TrackedMarkerMessage[] tracked_markers;
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
