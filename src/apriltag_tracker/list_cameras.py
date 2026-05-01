import argparse

import cv2

from .core import open_capture, read_frame_with_retries


def probe_camera(index, backend, width, height):
    cap = open_capture(index, width, height, backend=backend, fourcc="MJPG", fps=30)
    opened = cap.isOpened()
    ok = False
    actual_width = 0
    actual_height = 0
    actual_fps = 0
    if opened:
        ok, _ = read_frame_with_retries(cap, attempts=20, delay_s=0.05)
        actual_width = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
        actual_height = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
        actual_fps = cap.get(cv2.CAP_PROP_FPS)
    cap.release()
    return opened, ok, actual_width, actual_height, actual_fps


def main():
    parser = argparse.ArgumentParser(description="Probe available OpenCV camera indexes.")
    parser.add_argument("--max-index", type=int, default=6)
    parser.add_argument("--width", type=int, default=1280)
    parser.add_argument("--height", type=int, default=720)
    parser.add_argument(
        "--backend",
        choices=["dshow", "msmf", "any"],
        default="dshow",
        help="OpenCV video backend to test.",
    )
    args = parser.parse_args()

    print(f"backend={args.backend} requested={args.width}x{args.height}")
    for index in range(args.max_index + 1):
        opened, ok, actual_width, actual_height, actual_fps = probe_camera(
            index,
            args.backend,
            args.width,
            args.height,
        )
        status = "OK" if opened and ok else "unavailable"
        print(
            f"index {index}: {status}"
            + (
                f" ({actual_width}x{actual_height}, fps={actual_fps:.1f})"
                if opened
                else ""
            )
        )


if __name__ == "__main__":
    main()
