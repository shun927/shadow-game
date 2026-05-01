import argparse
import json
import socket
import time

import cv2
import numpy as np

from .core import (
    draw_tag_label,
    estimate_pose,
    load_calibration,
    load_json,
    make_detector,
    marker_object_points,
    open_capture_from_config,
    polygon_area,
    read_frame_with_retries,
    rotation_to_euler_zyx_deg,
    transform_from_rvec_tvec,
    xyz_payload,
)


class TrackingFilter:
    def __init__(self, config):
        tracking = config.get("tracking", {})
        self.min_area_px2 = float(tracking.get("min_image_area_px2", 600.0))
        self.smoothing_alpha = float(tracking.get("smoothing_alpha", 0.35))
        self.max_jump_m = float(tracking.get("max_jump_m", 0.12))
        self.switch_area_ratio = float(tracking.get("camera_switch_area_ratio", 1.35))
        self.last_camera = None
        self.filtered_xyz = None

    def filter_candidates(self, results):
        return [
            result
            for result in results
            if float(result["image_area_px2"]) >= self.min_area_px2
        ]

    def choose(self, results):
        candidates = self.filter_candidates(results)
        if not candidates:
            return None

        best = max(candidates, key=lambda result: result["image_area_px2"])
        if self.last_camera:
            previous = next(
                (result for result in candidates if result["camera"] == self.last_camera),
                None,
            )
            if (
                previous is not None
                and best["camera"] != self.last_camera
                and best["image_area_px2"] < previous["image_area_px2"] * self.switch_area_ratio
            ):
                return previous
        return best

    def smooth(self, selected):
        raw_xyz = np.asarray(
            [
                selected["field_xyz_m"]["x"],
                selected["field_xyz_m"]["y"],
                selected["field_xyz_m"]["z"],
            ],
            dtype=np.float64,
        )

        rejected_jump = False
        if self.filtered_xyz is None:
            self.filtered_xyz = raw_xyz
        else:
            jump = float(np.linalg.norm(raw_xyz - self.filtered_xyz))
            if jump <= self.max_jump_m:
                alpha = self.smoothing_alpha
                self.filtered_xyz = self.filtered_xyz * (1.0 - alpha) + raw_xyz * alpha
            else:
                rejected_jump = True

        self.last_camera = selected["camera"]
        stable = dict(selected)
        stable["raw_field_xyz_m"] = selected["field_xyz_m"]
        stable["field_xyz_m"] = xyz_payload(self.filtered_xyz)
        stable["rejected_jump"] = rejected_jump
        return stable


def load_camera_runtime(camera_config):
    cap = open_capture_from_config(camera_config)
    if not cap.isOpened():
        raise RuntimeError(
            f"Could not open camera {camera_config['name']} "
            f"(index={camera_config.get('camera_index')}, "
            f"backend={camera_config.get('backend', 'auto')}). "
            "Run `uv run apriltag-list-cameras` and update configs/field_config.json."
        )

    ok, frame = read_frame_with_retries(cap)
    if not ok:
        cap.release()
        raise RuntimeError(
            f"Could not read from camera {camera_config['name']} "
            f"(index={camera_config.get('camera_index')}, "
            f"backend={camera_config.get('backend', 'msmf')}). "
            "Try another USB port, close other camera apps, or swap camera_index values."
        )

    height, width = frame.shape[:2]
    camera_matrix, dist_coeffs, using_fallback = load_calibration(
        camera_config.get("calibration"),
        width,
        height,
        60.0,
    )
    if using_fallback:
        print(f"WARNING: {camera_config['name']} has no calibration; pose is approximate.")

    return {
        "config": camera_config,
        "cap": cap,
        "camera_matrix": camera_matrix,
        "dist_coeffs": dist_coeffs,
    }


def detect_target_from_camera(runtime, detector, object_points, target_id, field_from_camera):
    cap = runtime["cap"]
    ok, frame = cap.read()
    if not ok:
        return None, None

    gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
    corners_list, ids, _ = detector.detectMarkers(gray)

    if ids is not None:
        cv2.aruco.drawDetectedMarkers(frame, corners_list, ids)
        for corners, marker_id in zip(corners_list, ids.flatten()):
            marker_id = int(marker_id)
            if marker_id != target_id:
                draw_tag_label(frame, f"id {marker_id}", corners, (128, 128, 128))
                continue

            rvec, tvec = estimate_pose(
                corners,
                object_points,
                runtime["camera_matrix"],
                runtime["dist_coeffs"],
            )
            if rvec is None:
                continue

            camera_from_marker = transform_from_rvec_tvec(rvec, tvec)
            field_from_marker = field_from_camera @ camera_from_marker
            field_xyz = field_from_marker[:3, 3]
            area = polygon_area(corners)

            label = (
                f"id:{marker_id} "
                f"x:{field_xyz[0]:+.3f} y:{field_xyz[1]:+.3f} z:{field_xyz[2]:+.3f}m"
            )
            draw_tag_label(frame, label, corners, (0, 255, 0))
            cv2.drawFrameAxes(
                frame,
                runtime["camera_matrix"],
                runtime["dist_coeffs"],
                rvec,
                tvec,
                float(np.max(object_points) - np.min(object_points)) * 0.5,
            )

            return {
                "id": marker_id,
                "camera": runtime["config"]["name"],
                "camera_index": int(runtime["config"]["camera_index"]),
                "field_from_marker": field_from_marker,
                "field_xyz_m": xyz_payload(field_xyz),
                "field_euler_zyx_deg": rotation_to_euler_zyx_deg(field_from_marker[:3, :3]),
                "camera_xyz_m": xyz_payload(tvec),
                "image_area_px2": area,
            }, frame

    return None, frame


def payload(selected, results):
    selected_payload = {
        "timestamp_ms": int(time.time() * 1000),
        "id": int(selected.get("id", 0)),
        "field_xyz_m": selected["field_xyz_m"],
        "raw_field_xyz_m": selected.get("raw_field_xyz_m", selected["field_xyz_m"]),
        "field_euler_zyx_deg": selected["field_euler_zyx_deg"],
        "visible_cameras": [result["camera"] for result in results],
        "selected_camera": selected["camera"],
        "rejected_jump": bool(selected.get("rejected_jump", False)),
        "per_camera": {},
    }
    for result in results:
        selected_payload["per_camera"][result["camera"]] = {
            "camera_index": result["camera_index"],
            "field_xyz_m": result["field_xyz_m"],
            "field_euler_zyx_deg": result["field_euler_zyx_deg"],
            "camera_xyz_m": result["camera_xyz_m"],
            "image_area_px2": result["image_area_px2"],
        }
    return selected_payload


def main():
    parser = argparse.ArgumentParser(
        description="Track one AprilTag 36h11 in field coordinates using two webcams."
    )
    parser.add_argument("--config", default="configs/field_config.json")
    parser.add_argument("--extrinsics", default="calibrations/field_extrinsics.json")
    parser.add_argument(
        "--print-every",
        type=int,
        default=5,
        help="Print JSON every N frames with a detection. Use 0 to disable.",
    )
    parser.add_argument(
        "--udp-host",
        default=None,
        help="Send tracking JSON to this UDP host when set. Example: 127.0.0.1",
    )
    parser.add_argument(
        "--udp-port",
        type=int,
        default=5005,
        help="UDP port used with --udp-host.",
    )
    args = parser.parse_args()

    config = load_json(args.config)
    extrinsics = load_json(args.extrinsics)
    detector = make_detector()

    target_id = int(config["moving_tag"]["id"])
    object_points = marker_object_points(float(config["moving_tag"]["size_m"]))
    tracking_filter = TrackingFilter(config)
    runtimes = []
    field_from_camera_by_name = {}
    udp_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM) if args.udp_host else None
    udp_target = (args.udp_host, args.udp_port) if args.udp_host else None

    for camera_config in config["cameras"]:
        name = camera_config["name"]
        if name not in extrinsics["cameras"]:
            raise SystemExit(f"Missing extrinsics for camera {name}")
        runtimes.append(load_camera_runtime(camera_config))
        field_from_camera_by_name[name] = np.asarray(
            extrinsics["cameras"][name]["field_from_camera"],
            dtype=np.float64,
        )

    frame_index = 0
    try:
        while True:
            results = []
            frames = []

            for runtime in runtimes:
                name = runtime["config"]["name"]
                result, frame = detect_target_from_camera(
                    runtime,
                    detector,
                    object_points,
                    target_id,
                    field_from_camera_by_name[name],
                )
                if result is not None:
                    results.append(result)
                if frame is not None:
                    frames.append((name, frame))

            selected = tracking_filter.choose(results)
            if selected is not None:
                selected = tracking_filter.smooth(selected)
            if selected is not None:
                message = json.dumps(payload(selected, results))
                if udp_socket and udp_target:
                    udp_socket.sendto(message.encode("utf-8"), udp_target)
                if args.print_every and frame_index % args.print_every == 0:
                    print(message, flush=True)

            for name, frame in frames:
                if selected is not None:
                    cv2.putText(
                        frame,
                        f"selected: {selected['camera']}",
                        (20, 36),
                        cv2.FONT_HERSHEY_SIMPLEX,
                        0.75,
                        (0, 255, 0),
                        2,
                        cv2.LINE_AA,
                    )
                cv2.imshow(f"AprilTag 2cam - {name}", frame)

            key = cv2.waitKey(1) & 0xFF
            if key in (27, ord("q")):
                break

            frame_index += 1
    finally:
        for runtime in runtimes:
            runtime["cap"].release()
        if udp_socket:
            udp_socket.close()
        cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
