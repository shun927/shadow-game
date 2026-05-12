import argparse
import json
import socket
import time

import cv2
import numpy as np

from .core import (
    draw_tag_label,
    estimate_pose,
    load_json,
    make_detector,
    marker_object_points,
    open_camera_runtime_from_config,
    polygon_area,
    release_camera_runtime,
    rotation_to_euler_zyx_deg,
    transform_from_rvec_tvec,
    xyz_payload,
)


def axes_payload(transform):
    rotation = np.asarray(transform, dtype=np.float64).reshape(4, 4)[:3, :3]
    return {
        "marker_x_axis_field": xyz_payload(rotation[:, 0]),
        "marker_y_axis_field": xyz_payload(rotation[:, 1]),
        "marker_z_axis_field": xyz_payload(rotation[:, 2]),
    }


class TrackingFilter:
    def __init__(self, config):
        tracking = config.get("tracking", {})
        self.min_area_px2 = float(tracking.get("min_image_area_px2", 600.0))
        self.smoothing_alpha = float(tracking.get("smoothing_alpha", 0.35))
        self.single_camera_alpha = float(tracking.get("single_camera_alpha", 0.75))
        self.camera_transition_alpha = float(tracking.get("camera_transition_alpha", 0.9))
        self.max_jump_m = float(tracking.get("max_jump_m", 0.12))
        self.jump_reset_frames = int(tracking.get("jump_reset_frames", 5))
        self.allow_single_camera_pose = bool(tracking.get("allow_single_camera_pose", True))
        self.switch_area_ratio = float(tracking.get("camera_switch_area_ratio", 1.35))
        self.last_camera = None
        self.last_visible_count = 0
        self.filtered_xyz = None
        self.rejected_jump_count = 0

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

    def smooth(self, selected, visible_count=1):
        raw_xyz = np.asarray(
            [
                selected["field_xyz_m"]["x"],
                selected["field_xyz_m"]["y"],
                selected["field_xyz_m"]["z"],
            ],
            dtype=np.float64,
        )

        rejected_jump = False
        camera_changed = self.last_camera is not None and selected["camera"] != self.last_camera
        entered_single_camera = self.last_visible_count > 1 and visible_count == 1
        if self.filtered_xyz is None:
            self.filtered_xyz = raw_xyz
        elif self.allow_single_camera_pose and visible_count == 1:
            alpha = self.camera_transition_alpha if camera_changed or entered_single_camera else self.single_camera_alpha
            self.filtered_xyz = self.filtered_xyz * (1.0 - alpha) + raw_xyz * alpha
            self.rejected_jump_count = 0
        else:
            jump = float(np.linalg.norm(raw_xyz - self.filtered_xyz))
            if jump <= self.max_jump_m:
                alpha = self.smoothing_alpha
                self.filtered_xyz = self.filtered_xyz * (1.0 - alpha) + raw_xyz * alpha
                self.rejected_jump_count = 0
            else:
                rejected_jump = True
                self.rejected_jump_count += 1
                if self.rejected_jump_count >= self.jump_reset_frames:
                    self.filtered_xyz = raw_xyz
                    self.rejected_jump_count = 0
                    rejected_jump = False

        self.last_camera = selected["camera"]
        self.last_visible_count = visible_count
        stable = dict(selected)
        stable["raw_field_xyz_m"] = selected["field_xyz_m"]
        stable["field_xyz_m"] = xyz_payload(self.filtered_xyz)
        stable["rejected_jump"] = rejected_jump
        stable["tracking_mode"] = "single_camera_pose" if visible_count == 1 else "multi_camera_pose"
        return stable

def detect_targets_from_camera(runtime, detector, object_points_by_id, field_from_camera):
    ok, frame = runtime["read_frame"]()
    if not ok:
        return [], None

    gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
    corners_list, ids, _ = detector.detectMarkers(gray)
    results = []

    if ids is not None:
        cv2.aruco.drawDetectedMarkers(frame, corners_list, ids)
        for corners, marker_id in zip(corners_list, ids.flatten()):
            marker_id = int(marker_id)
            if marker_id not in object_points_by_id:
                draw_tag_label(frame, f"id {marker_id}", corners, (128, 128, 128))
                continue

            marker_points = object_points_by_id[marker_id]
            rvec, tvec = estimate_pose(
                corners,
                marker_points,
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
                float(np.max(marker_points) - np.min(marker_points)) * 0.5,
            )

            result = {
                "id": marker_id,
                "camera": runtime["config"]["name"],
                "camera_index": int(runtime.get("camera_index", -1)),
                "field_from_marker": field_from_marker,
                "field_xyz_m": xyz_payload(field_xyz),
                "field_euler_zyx_deg": rotation_to_euler_zyx_deg(field_from_marker[:3, :3]),
                **axes_payload(field_from_marker),
                "camera_xyz_m": xyz_payload(tvec),
                "image_area_px2": area,
            }
            results.append(result)

    return results, frame


def payload(selected, results, tracked_markers):
    timestamp_ms = int(time.time() * 1000)
    selected_payload = {
        "timestamp_ms": timestamp_ms,
        "id": int(selected.get("id", 0)),
        "field_xyz_m": selected["field_xyz_m"],
        "raw_field_xyz_m": selected.get("raw_field_xyz_m", selected["field_xyz_m"]),
        "field_euler_zyx_deg": selected["field_euler_zyx_deg"],
        "marker_x_axis_field": selected["marker_x_axis_field"],
        "marker_y_axis_field": selected["marker_y_axis_field"],
        "marker_z_axis_field": selected["marker_z_axis_field"],
        "visible_cameras": [result["camera"] for result in results],
        "selected_camera": selected["camera"],
        "rejected_jump": bool(selected.get("rejected_jump", False)),
        "tracking_mode": selected.get("tracking_mode", "multi_camera_pose"),
        "tracked_markers": [
            {
                "timestamp_ms": int(marker.get("timestamp_ms", timestamp_ms)),
                "id": int(marker.get("id", 0)),
                "field_xyz_m": marker["field_xyz_m"],
                "raw_field_xyz_m": marker.get("raw_field_xyz_m", marker["field_xyz_m"]),
                "field_euler_zyx_deg": marker["field_euler_zyx_deg"],
                "marker_x_axis_field": marker["marker_x_axis_field"],
                "marker_y_axis_field": marker["marker_y_axis_field"],
                "marker_z_axis_field": marker["marker_z_axis_field"],
                "visible_cameras": [result["camera"] for result in marker.get("results", [])],
                "selected_camera": marker["camera"],
                "rejected_jump": bool(marker.get("rejected_jump", False)),
                "tracking_mode": marker.get("tracking_mode", "multi_camera_pose"),
            }
            for marker in tracked_markers
        ],
        "per_camera": {},
    }
    for result in results:
        selected_payload["per_camera"][result["camera"]] = {
            "camera_index": result["camera_index"],
            "field_xyz_m": result["field_xyz_m"],
            "field_euler_zyx_deg": result["field_euler_zyx_deg"],
            "marker_x_axis_field": result["marker_x_axis_field"],
            "marker_y_axis_field": result["marker_y_axis_field"],
            "marker_z_axis_field": result["marker_z_axis_field"],
            "camera_xyz_m": result["camera_xyz_m"],
            "image_area_px2": result["image_area_px2"],
        }
    return selected_payload


def main():
    parser = argparse.ArgumentParser(
        description="Track AprilTag 36h11 markers in field coordinates using two cameras."
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

    target_tags = [config["moving_tag"], *config.get("object_tags", [])]
    object_points_by_id = {
        int(tag["id"]): marker_object_points(float(tag["size_m"]))
        for tag in target_tags
    }
    moving_tag_id = int(config["moving_tag"]["id"])
    tracking_filters = {marker_id: TrackingFilter(config) for marker_id in object_points_by_id}
    runtimes = []
    field_from_camera_by_name = {}
    udp_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM) if args.udp_host else None
    udp_target = (args.udp_host, args.udp_port) if args.udp_host else None

    for camera_config in config["cameras"]:
        name = camera_config["name"]
        if name not in extrinsics["cameras"]:
            raise SystemExit(f"Missing extrinsics for camera {name}")
        runtimes.append(open_camera_runtime_from_config(camera_config))
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
                camera_results, frame = detect_targets_from_camera(
                    runtime,
                    detector,
                    object_points_by_id,
                    field_from_camera_by_name[name],
                )
                results.extend(camera_results)
                if frame is not None:
                    frames.append((name, frame))

            tracked_markers = []
            for marker_id, tracking_filter in tracking_filters.items():
                marker_results = [result for result in results if int(result["id"]) == marker_id]
                selected_marker = tracking_filter.choose(marker_results)
                if selected_marker is None:
                    continue
                selected_marker = tracking_filter.smooth(selected_marker, len(marker_results))
                selected_marker["timestamp_ms"] = int(time.time() * 1000)
                selected_marker["results"] = marker_results
                tracked_markers.append(selected_marker)

            selected = next(
                (marker for marker in tracked_markers if int(marker["id"]) == moving_tag_id),
                None,
            )
            if selected is not None:
                message = json.dumps(payload(selected, results, tracked_markers))
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
            release_camera_runtime(runtime)
        if udp_socket:
            udp_socket.close()
        cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
