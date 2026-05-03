from __future__ import annotations

import argparse
import os
import zlib
from dataclasses import dataclass
from pathlib import Path

import cv2


MM_TO_PT = 72.0 / 25.4
A4_W_PT = 210.0 * MM_TO_PT
A4_H_PT = 297.0 * MM_TO_PT


@dataclass(frozen=True)
class MarkerSpec:
    path: Path
    label: str
    black_square_mm: float
    black_square_px: int
    x_mm: float
    y_mm_from_top: float

    @property
    def x_pt(self) -> float:
        return self.x_mm * MM_TO_PT

    @property
    def y_pt(self) -> float:
        return A4_H_PT - (self.y_mm_from_top + self.total_mm) * MM_TO_PT

    @property
    def total_mm(self) -> float:
        image = cv2.imread(str(self.path), cv2.IMREAD_GRAYSCALE)
        if image is None:
            raise FileNotFoundError(self.path)
        return image.shape[1] / self.black_square_px * self.black_square_mm

    @property
    def total_pt(self) -> float:
        return self.total_mm * MM_TO_PT


class PdfWriter:
    def __init__(self) -> None:
        self.objects: list[bytes] = []

    def add(self, body: bytes) -> int:
        self.objects.append(body)
        return len(self.objects)

    def write(self, output: Path, root_obj: int) -> None:
        chunks = [b"%PDF-1.4\n%\xe2\xe3\xcf\xd3\n"]
        offsets = [0]
        for index, obj in enumerate(self.objects, start=1):
            offsets.append(sum(len(chunk) for chunk in chunks))
            chunks.append(f"{index} 0 obj\n".encode("ascii"))
            chunks.append(obj)
            chunks.append(b"\nendobj\n")
        xref_pos = sum(len(chunk) for chunk in chunks)
        chunks.append(f"xref\n0 {len(self.objects) + 1}\n".encode("ascii"))
        chunks.append(b"0000000000 65535 f \n")
        for offset in offsets[1:]:
            chunks.append(f"{offset:010d} 00000 n \n".encode("ascii"))
        chunks.append(
            f"trailer\n<< /Size {len(self.objects) + 1} /Root {root_obj} 0 R >>\n"
            f"startxref\n{xref_pos}\n%%EOF\n".encode("ascii")
        )
        output.write_bytes(b"".join(chunks))


def image_object(path: Path) -> tuple[bytes, int, int]:
    image = cv2.imread(str(path), cv2.IMREAD_GRAYSCALE)
    if image is None:
        raise FileNotFoundError(path)
    height, width = image.shape
    compressed = zlib.compress(image.tobytes())
    obj = (
        f"<< /Type /XObject /Subtype /Image /Width {width} /Height {height} "
        f"/ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /FlateDecode "
        f"/Length {len(compressed)} >>\nstream\n".encode("ascii")
        + compressed
        + b"\nendstream"
    )
    return obj, width, height


def escape_text(text: str) -> str:
    return text.replace("\\", "\\\\").replace("(", "\\(").replace(")", "\\)")


def build_pdf(marker_dir: Path, output: Path) -> None:
    # A4 portrait. Coordinates are in millimeters from the top-left for layout.
    specs = [
        MarkerSpec(marker_dir / "apriltag_36h11_id0.png", "ID 0  black square: 70 mm", 70, 1000, 58.8, 18.0),
        MarkerSpec(marker_dir / "ref_10.png", "ID 10  black square: 40 mm", 40, 800, 44.0, 130.0),
        MarkerSpec(marker_dir / "ref_11.png", "ID 11  black square: 40 mm", 40, 800, 114.0, 130.0),
        MarkerSpec(marker_dir / "ref_13.png", "ID 13  black square: 40 mm", 40, 800, 44.0, 202.0),
        MarkerSpec(marker_dir / "ref_12.png", "ID 12  black square: 40 mm", 40, 800, 114.0, 202.0),
    ]

    pdf = PdfWriter()
    image_names: list[tuple[str, int]] = []
    content_parts: list[str] = []

    for index, spec in enumerate(specs, start=1):
        obj, _, _ = image_object(spec.path)
        obj_id = pdf.add(obj)
        name = f"Im{index}"
        image_names.append((name, obj_id))

        x = spec.x_pt
        y = spec.y_pt
        size = spec.total_pt
        content_parts.append(f"q {size:.3f} 0 0 {size:.3f} {x:.3f} {y:.3f} cm /{name} Do Q\n")
        content_parts.append(
            f"0.65 G 0.35 w {x:.3f} {y:.3f} {size:.3f} {size:.3f} re S\n"
        )
        text_x = x
        text_y = y - 10
        content_parts.append(
            f"BT /F1 8 Tf 0 g {text_x:.3f} {text_y:.3f} Td ({escape_text(spec.label)}) Tj ET\n"
        )

    title = "AprilTag 36h11 marker sheet - print at actual size / 100%"
    content_parts.append(f"BT /F1 9 Tf 0 g 28.346 815.000 Td ({escape_text(title)}) Tj ET\n")
    content_parts.append(
        "BT /F1 7 Tf 0 g 28.346 802.500 Td "
        "(Measure the outer black square after printing. Do not fit to page.) Tj ET\n"
    )
    content = "".join(content_parts).encode("ascii")
    compressed_content = zlib.compress(content)
    content_obj = pdf.add(
        f"<< /Length {len(compressed_content)} /Filter /FlateDecode >>\nstream\n".encode("ascii")
        + compressed_content
        + b"\nendstream"
    )

    font_obj = pdf.add(b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>")
    xobjects = " ".join(f"/{name} {obj_id} 0 R" for name, obj_id in image_names)
    resources_obj = pdf.add(
        f"<< /XObject << {xobjects} >> /Font << /F1 {font_obj} 0 R >> >>".encode("ascii")
    )
    page_obj = pdf.add(
        f"<< /Type /Page /Parent 0 0 R /MediaBox [0 0 {A4_W_PT:.3f} {A4_H_PT:.3f}] "
        f"/Resources {resources_obj} 0 R /Contents {content_obj} 0 R >>".encode("ascii")
    )
    pages_obj = pdf.add(f"<< /Type /Pages /Kids [{page_obj} 0 R] /Count 1 >>".encode("ascii"))

    # Patch the page parent reference now that the pages object exists.
    pdf.objects[page_obj - 1] = pdf.objects[page_obj - 1].replace(b"/Parent 0 0 R", f"/Parent {pages_obj} 0 R".encode("ascii"))
    catalog_obj = pdf.add(f"<< /Type /Catalog /Pages {pages_obj} 0 R >>".encode("ascii"))

    output.parent.mkdir(parents=True, exist_ok=True)
    pdf.write(output, catalog_obj)


def main() -> None:
    parser = argparse.ArgumentParser(description="Create an A4 PDF sheet for the project AprilTag markers.")
    parser.add_argument("--marker-dir", default="markers")
    parser.add_argument("--output", default="markers/apriltag_a4_sheet.pdf")
    args = parser.parse_args()

    build_pdf(Path(args.marker_dir), Path(args.output))
    print(os.path.abspath(args.output))


if __name__ == "__main__":
    main()
