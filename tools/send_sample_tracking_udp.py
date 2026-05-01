import argparse
import json
import math
import socket
import time


def make_message(t):
    radius = 0.08
    x = math.cos(t) * radius
    y = math.sin(t) * radius
    return {
        "timestamp_ms": int(time.time() * 1000),
        "id": 0,
        "field_xyz_m": {"x": x, "y": y, "z": 0.12},
        "raw_field_xyz_m": {"x": x, "y": y, "z": 0.12},
        "field_euler_zyx_deg": {
            "roll_x": 0.0,
            "pitch_y": 35.0 * math.sin(t * 0.5),
            "yaw_z": math.degrees(t) % 360.0,
        },
        "visible_cameras": ["sample"],
        "selected_camera": "sample",
        "rejected_jump": False,
    }


def main():
    parser = argparse.ArgumentParser(description="Send sample tracking UDP to Unity.")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=5005)
    parser.add_argument("--hz", type=float, default=30.0)
    args = parser.parse_args()

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    interval = 1.0 / args.hz
    started = time.time()
    print(f"sending sample tracking UDP to {args.host}:{args.port}")
    try:
        while True:
            t = time.time() - started
            sock.sendto(json.dumps(make_message(t)).encode("utf-8"), (args.host, args.port))
            time.sleep(interval)
    except KeyboardInterrupt:
        pass
    finally:
        sock.close()


if __name__ == "__main__":
    main()
