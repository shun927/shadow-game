import json
import math
import time
from pathlib import Path

import cv2
import numpy as np


def load_json(path: str | Path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def write_json(path: str | Path, data):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2), encoding="utf-8")


def load_calibration(path: str | None, width: int, height: int, fallback_fov_deg: float):
    if path:
        data = load_json(path)
        camera_matrix = np.asarray(
            data.get("camera_matrix") or data.get("K"),
            dtype=np.float64,
        )
        dist_coeffs = np.asarray(
            data.get("dist_coeffs")
            or data.get("distortion_coefficients")
            or data.get("D")
            or [0, 0, 0, 0, 0],
            dtype=np.float64,
        ).reshape(-1, 1)
        return camera_matrix, dist_coeffs, False

    fov = math.radians(fallback_fov_deg)
    fx = fy = width / (2.0 * math.tan(fov / 2.0))
    camera_matrix = np.array(
        [[fx, 0.0, width / 2.0], [0.0, fy, height / 2.0], [0.0, 0.0, 1.0]],
        dtype=np.float64,
    )
    dist_coeffs = np.zeros((5, 1), dtype=np.float64)
    return camera_matrix, dist_coeffs, True


def make_detector():
    dictionary = cv2.aruco.getPredefinedDictionary(cv2.aruco.DICT_APRILTAG_36h11)

    if hasattr(cv2.aruco, "DetectorParameters"):
        params = cv2.aruco.DetectorParameters()
    else:
        params = cv2.aruco.DetectorParameters_create()

    if hasattr(cv2.aruco, "CORNER_REFINE_SUBPIX"):
        params.cornerRefinementMethod = cv2.aruco.CORNER_REFINE_SUBPIX
        params.cornerRefinementWinSize = 5

    if hasattr(cv2.aruco, "ArucoDetector"):
        return cv2.aruco.ArucoDetector(dictionary, params)

    class LegacyDetector:
        def detectMarkers(self, image):
            return cv2.aruco.detectMarkers(image, dictionary, parameters=params)

    return LegacyDetector()


def marker_object_points(marker_size_m: float):
    half = marker_size_m / 2.0
    return np.array(
        [
            [-half, half, 0.0],
            [half, half, 0.0],
            [half, -half, 0.0],
            [-half, -half, 0.0],
        ],
        dtype=np.float64,
    )


def field_tag_object_points(tag):
    size_m = float(tag["size_m"])
    local = marker_object_points(size_m)
    center = tag.get("center_m", {})
    translation = np.array(
        [
            float(center.get("x", 0.0)),
            float(center.get("y", 0.0)),
            float(center.get("z", 0.0)),
        ],
        dtype=np.float64,
    )
    yaw = math.radians(float(tag.get("yaw_deg", 0.0)))
    rotation = np.array(
        [
            [math.cos(yaw), -math.sin(yaw), 0.0],
            [math.sin(yaw), math.cos(yaw), 0.0],
            [0.0, 0.0, 1.0],
        ],
        dtype=np.float64,
    )
    return (rotation @ local.T).T + translation


def estimate_pose(corners, object_points, camera_matrix, dist_coeffs, flags=None):
    image_points = np.asarray(corners, dtype=np.float64).reshape(-1, 2)
    object_points = np.asarray(object_points, dtype=np.float64).reshape(-1, 3)
    if flags is None:
        flags = getattr(cv2, "SOLVEPNP_IPPE_SQUARE", cv2.SOLVEPNP_ITERATIVE)
    ok, rvec, tvec = cv2.solvePnP(
        object_points,
        image_points,
        camera_matrix,
        dist_coeffs,
        flags=flags,
    )
    if not ok:
        return None, None
    return rvec.reshape(3, 1), tvec.reshape(3, 1)


def transform_from_rvec_tvec(rvec, tvec):
    rotation, _ = cv2.Rodrigues(np.asarray(rvec, dtype=np.float64).reshape(3, 1))
    transform = np.eye(4, dtype=np.float64)
    transform[:3, :3] = rotation
    transform[:3, 3] = np.asarray(tvec, dtype=np.float64).reshape(3)
    return transform


def invert_transform(transform):
    transform = np.asarray(transform, dtype=np.float64).reshape(4, 4)
    inverse = np.eye(4, dtype=np.float64)
    rotation = transform[:3, :3]
    translation = transform[:3, 3]
    inverse[:3, :3] = rotation.T
    inverse[:3, 3] = -rotation.T @ translation
    return inverse


def rvec_to_euler_zyx_deg(rvec):
    rotation, _ = cv2.Rodrigues(np.asarray(rvec, dtype=np.float64).reshape(3, 1))
    return rotation_to_euler_zyx_deg(rotation)


def rotation_to_euler_zyx_deg(rotation):
    rotation = np.asarray(rotation, dtype=np.float64).reshape(3, 3)
    sy = math.sqrt(rotation[0, 0] * rotation[0, 0] + rotation[1, 0] * rotation[1, 0])
    singular = sy < 1e-6

    if not singular:
        roll_x = math.atan2(rotation[2, 1], rotation[2, 2])
        pitch_y = math.atan2(-rotation[2, 0], sy)
        yaw_z = math.atan2(rotation[1, 0], rotation[0, 0])
    else:
        roll_x = math.atan2(-rotation[1, 2], rotation[1, 1])
        pitch_y = math.atan2(-rotation[2, 0], sy)
        yaw_z = 0.0

    return {
        "roll_x": math.degrees(roll_x),
        "pitch_y": math.degrees(pitch_y),
        "yaw_z": math.degrees(yaw_z),
    }


def polygon_area(corners):
    points = np.asarray(corners, dtype=np.float64).reshape(4, 2)
    x = points[:, 0]
    y = points[:, 1]
    return float(0.5 * abs(np.dot(x, np.roll(y, -1)) - np.dot(y, np.roll(x, -1))))


def backend_api(backend: str | None):
    if backend is None:
        return cv2.CAP_DSHOW
    backend = backend.lower()
    if backend == "auto":
        return None
    if backend == "dshow":
        return cv2.CAP_DSHOW
    if backend == "msmf":
        return cv2.CAP_MSMF
    if backend in ("any", "default"):
        return cv2.CAP_ANY
    raise ValueError(f"Unknown camera backend: {backend}")


def backend_sequence(backend: str | None):
    if backend is None:
        return [("dshow", cv2.CAP_DSHOW)]
    backend = backend.lower()
    if backend == "auto":
        return [
            ("msmf", cv2.CAP_MSMF),
            ("any", cv2.CAP_ANY),
        ]
    return [(backend, backend_api(backend))]


def open_capture(
    camera_index: int,
    width: int,
    height: int,
    backend: str | None = "dshow",
    fourcc: str | None = None,
    fps: float | None = None,
    apply_settings: bool = True,
    require_frame: bool = False,
):
    last_cap = None
    for backend_name, api in backend_sequence(backend):
        cap = cv2.VideoCapture(int(camera_index), api)
        if apply_settings:
            if fourcc:
                cap.set(cv2.CAP_PROP_FOURCC, cv2.VideoWriter_fourcc(*fourcc[:4]))
            cap.set(cv2.CAP_PROP_FRAME_WIDTH, int(width))
            cap.set(cv2.CAP_PROP_FRAME_HEIGHT, int(height))
            if fps:
                cap.set(cv2.CAP_PROP_FPS, float(fps))
        if cap.isOpened() and not require_frame:
            return cap
        if cap.isOpened() and require_frame:
            ok, _ = read_frame_with_retries(cap, attempts=20, delay_s=0.05)
            if ok:
                return cap
        cap.release()
        last_cap = cap
    return last_cap if last_cap is not None else cv2.VideoCapture()


def open_capture_from_config(camera_config):
    return open_capture(
        camera_config["camera_index"],
        camera_config.get("width", 1280),
        camera_config.get("height", 720),
        camera_config.get("backend", "dshow"),
        camera_config.get("fourcc"),
        camera_config.get("fps"),
        bool(camera_config.get("apply_settings", True)),
        bool(camera_config.get("require_frame_on_open", False)),
    )


def read_frame_with_retries(cap, attempts: int = 60, delay_s: float = 0.05):
    last_frame = None
    for _ in range(attempts):
        ok, frame = cap.read()
        if ok and frame is not None:
            return True, frame
        last_frame = frame
        time.sleep(delay_s)
    return False, last_frame


def xyz_payload(vector):
    x, y, z = (float(v) for v in np.asarray(vector, dtype=np.float64).reshape(3))
    return {"x": x, "y": y, "z": z}


def draw_tag_label(frame, label, corners, color=(0, 255, 0)):
    corner = tuple(np.asarray(corners).reshape(4, 2)[0].astype(int))
    cv2.putText(
        frame,
        label,
        (corner[0], max(24, corner[1] - 12)),
        cv2.FONT_HERSHEY_SIMPLEX,
        0.55,
        color,
        2,
        cv2.LINE_AA,
    )
