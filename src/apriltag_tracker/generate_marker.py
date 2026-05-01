import argparse
from pathlib import Path

import cv2
import numpy as np


def main():
    parser = argparse.ArgumentParser(description="Generate an AprilTag 36h11 marker PNG.")
    parser.add_argument("--id", type=int, default=0, help="Marker id.")
    parser.add_argument(
        "--pixels",
        type=int,
        default=1000,
        help="Marker black-square image size before adding white margin.",
    )
    parser.add_argument(
        "--margin-pixels",
        type=int,
        default=160,
        help="White quiet-zone margin around the marker.",
    )
    parser.add_argument("--output", default="markers/apriltag_36h11_id0.png")
    args = parser.parse_args()

    dictionary = cv2.aruco.getPredefinedDictionary(cv2.aruco.DICT_APRILTAG_36h11)
    if hasattr(cv2.aruco, "generateImageMarker"):
        image = cv2.aruco.generateImageMarker(dictionary, args.id, args.pixels)
    else:
        image = cv2.aruco.drawMarker(dictionary, args.id, args.pixels)

    if args.margin_pixels > 0:
        image = np.pad(
            image,
            ((args.margin_pixels, args.margin_pixels), (args.margin_pixels, args.margin_pixels)),
            mode="constant",
            constant_values=255,
        )

    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    cv2.imwrite(str(output), image)
    print(f"wrote {output}")


if __name__ == "__main__":
    main()
