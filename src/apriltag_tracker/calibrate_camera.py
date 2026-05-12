import argparse
import json
from pathlib import Path

import cv2
import numpy as np

from .core import load_json, open_capture_from_config


def find_camera_config(config, camera_name):
    for camera_config in config.get("cameras", []):
        if camera_config.get("name") == camera_name:
            return camera_config
    names = [camera.get("name") for camera in config.get("cameras", [])]
    raise SystemExit(f"Camera name {camera_name!r} not found. Available: {names}")


def main():
    parser = argparse.ArgumentParser(description="Create a webcam calibration JSON.")
    parser.add_argument("--camera", type=int, default=0, help="Webcam index.")
    parser.add_argument(
        "--config",
        help="2-camera field config. When set, camera/backend settings are read from this file.",
    )
    parser.add_argument(
        "--camera-name",
        choices=["left", "right"],
        help="Camera name in --config to calibrate.",
    )
    parser.add_argument("--width", type=int, default=1280, help="Capture width.")
    parser.add_argument("--height", type=int, default=720, help="Capture height.")
    parser.add_argument(
        "--board-cols",
        type=int,
        default=9,
        help="Chessboard inner corners along the horizontal direction.",
    )
    parser.add_argument(
        "--board-rows",
        type=int,
        default=6,
        help="Chessboard inner corners along the vertical direction.",
    )
    parser.add_argument(
        "--square-size-m",
        type=float,
        required=True,
        help="One chessboard square size in meters.",
    )
    parser.add_argument("--samples", type=int, default=20, help="Frames to capture.")
    parser.add_argument("--output", default="calibrations/camera_calibration.json")
    args = parser.parse_args()

    camera_config = None
    if args.config:
        if not args.camera_name:
            raise SystemExit("--camera-name is required when --config is used.")
        config = load_json(args.config)
        camera_config = find_camera_config(config, args.camera_name)
        source = str(camera_config.get("source", "opencv")).lower()
        if source in ("realsense", "d435i"):
            raise SystemExit(
                "RealSense cameras use SDK intrinsics directly; "
                "skip internal chessboard calibration for this camera and run "
                "`apriltag-calibrate-field` for field extrinsics."
            )
        args.camera = int(camera_config["camera_index"])
        args.width = int(camera_config.get("width", args.width))
        args.height = int(camera_config.get("height", args.height))
        if args.output == "calibrations/camera_calibration.json":
            args.output = str(camera_config.get("calibration", args.output))

    pattern_size = (args.board_cols, args.board_rows)
    object_template = np.zeros((args.board_cols * args.board_rows, 3), np.float32)
    object_template[:, :2] = (
        np.mgrid[0 : args.board_cols, 0 : args.board_rows].T.reshape(-1, 2)
        * args.square_size_m
    )

    if camera_config:
        cap = open_capture_from_config(camera_config)
    else:
        cap = cv2.VideoCapture(args.camera, cv2.CAP_DSHOW)
        cap.set(cv2.CAP_PROP_FRAME_WIDTH, args.width)
        cap.set(cv2.CAP_PROP_FRAME_HEIGHT, args.height)

    if not cap.isOpened():
        raise SystemExit(f"Could not open camera index {args.camera}")

    object_points = []
    image_points = []
    image_size = None

    print("Press SPACE when the chessboard is detected. Press q or ESC to quit.")
    print(f"Need {args.samples} samples.")

    while len(object_points) < args.samples:
        ok, frame = cap.read()
        if not ok:
            break

        gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
        image_size = gray.shape[::-1]
        found, corners = cv2.findChessboardCorners(gray, pattern_size)

        display = frame.copy()
        if found:
            criteria = (
                cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER,
                30,
                0.001,
            )
            refined = cv2.cornerSubPix(gray, corners, (11, 11), (-1, -1), criteria)
            cv2.drawChessboardCorners(display, pattern_size, refined, found)
        else:
            refined = None

        cv2.putText(
            display,
            f"samples: {len(object_points)}/{args.samples}",
            (20, 36),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.9,
            (0, 255, 0),
            2,
            cv2.LINE_AA,
        )
        cv2.imshow("Camera calibration", display)

        key = cv2.waitKey(1) & 0xFF
        if key in (27, ord("q")):
            break
        if key == 32 and found and refined is not None:
            object_points.append(object_template.copy())
            image_points.append(refined)
            print(f"captured {len(object_points)}/{args.samples}")

    cap.release()
    cv2.destroyAllWindows()

    if len(object_points) < 5 or image_size is None:
        raise SystemExit("Need at least 5 captured samples for calibration.")

    rms, camera_matrix, dist_coeffs, rvecs, tvecs = cv2.calibrateCamera(
        object_points,
        image_points,
        image_size,
        None,
        None,
    )

    output = {
        "rms_reprojection_error": float(rms),
        "image_width": int(image_size[0]),
        "image_height": int(image_size[1]),
        "camera_matrix": camera_matrix.tolist(),
        "dist_coeffs": dist_coeffs.reshape(-1).tolist(),
        "board_cols": args.board_cols,
        "board_rows": args.board_rows,
        "square_size_m": args.square_size_m,
        "samples": len(object_points),
        "camera_index": args.camera,
        "camera_name": args.camera_name,
        "source_config": args.config,
    }
    Path(args.output).parent.mkdir(parents=True, exist_ok=True)
    Path(args.output).write_text(json.dumps(output, indent=2), encoding="utf-8")
    print(f"wrote {args.output}")


if __name__ == "__main__":
    main()
