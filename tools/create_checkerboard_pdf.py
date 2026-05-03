from __future__ import annotations

import argparse
from pathlib import Path


MM_TO_PT = 72.0 / 25.4
A4_W_PT = 210.0 * MM_TO_PT
A4_H_PT = 297.0 * MM_TO_PT


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


def escape_text(text: str) -> str:
    return text.replace("\\", "\\\\").replace("(", "\\(").replace(")", "\\)")


def build_checkerboard_pdf(output: Path, cols: int, rows: int, square_mm: float) -> None:
    board_w_mm = cols * square_mm
    board_h_mm = rows * square_mm
    x0 = (210.0 - board_w_mm) / 2.0 * MM_TO_PT
    y0 = (297.0 - board_h_mm) / 2.0 * MM_TO_PT
    square = square_mm * MM_TO_PT

    content: list[str] = []
    title = f"Camera calibration checkerboard: {cols} x {rows} squares, {square_mm:g} mm"
    note = "Print at actual size / 100%. OpenCV inner corners: 9 x 6."
    content.append(f"BT /F1 10 Tf 0 g 36 806 Td ({escape_text(title)}) Tj ET\n")
    content.append(f"BT /F1 8 Tf 0 g 36 792 Td ({escape_text(note)}) Tj ET\n")

    # White background and black outer border.
    content.append(f"1 g {x0:.3f} {y0:.3f} {board_w_mm * MM_TO_PT:.3f} {board_h_mm * MM_TO_PT:.3f} re f\n")
    for row in range(rows):
        for col in range(cols):
            if (row + col) % 2 == 0:
                x = x0 + col * square
                y = y0 + (rows - row - 1) * square
                content.append(f"0 g {x:.3f} {y:.3f} {square:.3f} {square:.3f} re f\n")

    content.append(f"0 g 0.75 w {x0:.3f} {y0:.3f} {board_w_mm * MM_TO_PT:.3f} {board_h_mm * MM_TO_PT:.3f} re S\n")

    pdf = PdfWriter()
    stream = "".join(content).encode("ascii")
    content_obj = pdf.add(f"<< /Length {len(stream)} >>\nstream\n".encode("ascii") + stream + b"endstream")
    font_obj = pdf.add(b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>")
    resources_obj = pdf.add(f"<< /Font << /F1 {font_obj} 0 R >> >>".encode("ascii"))
    page_obj = pdf.add(
        f"<< /Type /Page /Parent 0 0 R /MediaBox [0 0 {A4_W_PT:.3f} {A4_H_PT:.3f}] "
        f"/Resources {resources_obj} 0 R /Contents {content_obj} 0 R >>".encode("ascii")
    )
    pages_obj = pdf.add(f"<< /Type /Pages /Kids [{page_obj} 0 R] /Count 1 >>".encode("ascii"))
    pdf.objects[page_obj - 1] = pdf.objects[page_obj - 1].replace(b"/Parent 0 0 R", f"/Parent {pages_obj} 0 R".encode("ascii"))
    catalog_obj = pdf.add(f"<< /Type /Catalog /Pages {pages_obj} 0 R >>".encode("ascii"))

    output.parent.mkdir(parents=True, exist_ok=True)
    pdf.write(output, catalog_obj)


def main() -> None:
    parser = argparse.ArgumentParser(description="Create an A4 checkerboard PDF for camera calibration.")
    parser.add_argument("--output", default="markers/checkerboard_10x7_16mm_a4.pdf")
    parser.add_argument("--cols", type=int, default=10)
    parser.add_argument("--rows", type=int, default=7)
    parser.add_argument("--square-mm", type=float, default=16.0)
    args = parser.parse_args()

    build_checkerboard_pdf(Path(args.output), args.cols, args.rows, args.square_mm)
    print(Path(args.output).resolve())


if __name__ == "__main__":
    main()
