"""AprilTag 36h11 tracking tools."""

import os

# This must be set before importing cv2. It avoids common Windows MSMF failures
# with multiple USB cameras, including delayed/failed first reads.
os.environ.setdefault("OPENCV_VIDEOIO_MSMF_ENABLE_HW_TRANSFORMS", "0")
