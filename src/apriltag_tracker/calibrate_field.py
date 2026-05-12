import argparse
import time

import cv2
import numpy as np

from .core import (
    draw_tag_label,
    estimate_pose,
    field_tag_object_points,
    invert_transform,
    load_json,
    make_detector,
    open_camera_runtime_from_config,
    read_camera_frame_with_retries,
    release_camera_runtime,
    transform_from_rvec_tvec,
    write_json,
    xyz_payload,
)


def collect_reference_points(corners_list, ids, reference_tags):
    if ids is None:
        return [], [], []

    tag_by_id = {int(tag["id"]): tag for tag in reference_tags}
    object_points = []
    image_points = []
    seen_ids = []

    for corners, marker_id in zip(corners_list, ids.flatten()):
        marker_id = int(marker_id)
        tag = tag_by_id.get(marker_id)
        if tag is None:
            continue
        object_points.extend(field_tag_object_points(tag))
        image_points.extend(np.asarray(corners, dtype=np.float64).reshape(4, 2))
        seen_ids.append(marker_id)

    return object_points, image_points, seen_ids


def reprojection_error(object_points, image_points, rvec, tvec, camera_matrix, dist_coeffs):
    projected, _ = cv2.projectPoints(
        np.asarray(object_points, dtype=np.float64),
        rvec,
        tvec,
        camera_matrix,
        dist_coeffs,
    )
    projected = projected.reshape(-1, 2)
    image_points = np.asarray(image_points, dtype=np.float64).reshape(-1, 2)
    error = np.linalg.norm(projected - image_points, axis=1)
    return float(np.mean(error)), float(np.max(error))


def calibrate_camera_extrinsic(camera_config, config, detector):
    name = camera_config["name"]
    runtime = open_camera_runtime_from_config(camera_config)

    ok, frame = read_camera_frame_with_retries(runtime)
    if not ok:
        release_camera_runtime(runtime)
        raise RuntimeError(f"Could not read from camera {name}")

    camera_matrix = runtime["camera_matrix"]
    dist_coeffs = runtime["dist_coeffs"]
    if runtime.get("using_fallback_calibration"):
        print(f"WARNING: {name} has no calibration file; extrinsics will be approximate.")

    print(f"[{name}] Show at least 2 reference tags, preferably all 4.")
    print(f"[{name}] Press SPACE to save extrinsic. Press q or ESC to abort.")

    while True:
        ok, frame = runtime["read_frame"]()
        if not ok:
            break

        gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
        corners_list, ids, _ = detector.detectMarkers(gray)
        object_points, image_points, seen_ids = collect_reference_points(
            corners_list,
            ids,
            config["reference_tags"],
        )

        if ids is not None:
            cv2.aruco.drawDetectedMarkers(frame, corners_list, ids)
            for corners, marker_id in zip(corners_list, ids.flatten()):
                if int(marker_id) in seen_ids:
                    draw_tag_label(frame, f"ref {int(marker_id)}", corners, (0, 255, 0))

        label = f"{name}: refs {sorted(set(seen_ids))}  SPACE=capture"
        cv2.putText(
            frame,
            label,
            (20, 36),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.7,
            (0, 255, 0) if len(set(seen_ids)) >= 2 else (0, 0, 255),
            2,
            cv2.LINE_AA,
        )
        cv2.imshow(f"Field extrinsic calibration - {name}", frame)

        key = cv2.waitKey(1) & 0xFF
        if key in (27, ord("q")):
            release_camera_runtime(runtime)
            cv2.destroyWindow(f"Field extrinsic calibration - {name}")
            raise RuntimeError(f"Aborted camera {name}")
        if key == 32:
            if len(set(seen_ids)) < 2:
                print(f"[{name}] Need at least 2 reference tags.")
                continue

            rvec, tvec = estimate_pose(
                np.asarray(image_points, dtype=np.float64),
                np.asarray(object_points, dtype=np.float64),
                camera_matrix,
                dist_coeffs,
                flags=cv2.SOLVEPNP_ITERATIVE,
            )
            if rvec is None:
                print(f"[{name}] solvePnP failed.")
                continue

            camera_from_field = transform_from_rvec_tvec(rvec, tvec)
            field_from_camera = invert_transform(camera_from_field)
            mean_error, max_error = reprojection_error(
                object_points,
                image_points,
                rvec,
                tvec,
                camera_matrix,
                dist_coeffs,
            )

            release_camera_runtime(runtime)
            cv2.destroyWindow(f"Field extrinsic calibration - {name}")
            return {
                "name": name,
                "source": runtime.get("source", "opencv"),
                "camera_index": int(runtime.get("camera_index", -1)),
                "serial": runtime.get("serial", ""),
                "seen_reference_tag_ids": sorted(set(seen_ids)),
                "camera_from_field": camera_from_field.tolist(),
                "field_from_camera": field_from_camera.tolist(),
                "camera_position_field_m": xyz_payload(field_from_camera[:3, 3]),
                "mean_reprojection_error_px": mean_error,
                "max_reprojection_error_px": max_error,
            }

    release_camera_runtime(runtime)
    cv2.destroyWindow(f"Field extrinsic calibration - {name}")
    raise RuntimeError(f"Could not calibrate camera {name}")


def main():
    parser = argparse.ArgumentParser(
        description="Estimate each fixed camera pose from field reference AprilTags."
    )
    parser.add_argument("--config", default="configs/field_config.json")
    parser.add_argument("--output", default="calibrations/field_extrinsics.json")
    args = parser.parse_args()

    config = load_json(args.config)
    detector = make_detector()
    cameras = []

    for camera_config in config["cameras"]:
        cameras.append(calibrate_camera_extrinsic(camera_config, config, detector))

    output = {
        "created_timestamp_ms": int(time.time() * 1000),
        "config": args.config,
        "field": config.get("field", {}),
        "cameras": {camera["name"]: camera for camera in cameras},
    }
    write_json(args.output, output)
    print(f"wrote {args.output}")


if __name__ == "__main__":
    main()
