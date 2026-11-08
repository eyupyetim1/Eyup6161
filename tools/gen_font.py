#!/usr/bin/env python3
"""Inter alt kümesinden BlueCore gömülü fontunu üretir.

Glyph'ler Inter 4.001'den raster edilir. Ürün içindeki yüzün adı Inter değildir;
SIL OFL 1.1 ayrılmış ad koşulu için bitmap "BlueCore UI" olarak gömülür.
Kaynak lisans: third_party/inter/LICENSE.txt
"""

from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "src" / "display" / "font_data.cpp"
REGULAR = "/usr/share/fonts/truetype/macos/Inter-Regular.ttf"
MEDIUM = "/usr/share/fonts/truetype/macos/Inter-Medium.ttf"
SEMIBOLD = "/usr/share/fonts/truetype/macos/Inter-SemiBold.ttf"

CHARSET = "".join(chr(c) for c in range(32, 127)) + "ÇĞİÖŞÜçğıöşü°µ·—"

FACES = (
    ("Small", REGULAR, 13),
    ("Body", MEDIUM, 16),
    ("Value", SEMIBOLD, 20),
    ("Title", SEMIBOLD, 28),
)


def raster(font: ImageFont.FreeTypeFont, ch: str, threshold: int = 130) -> dict:
    ascent, descent = font.getmetrics()
    if ch == " ":
        advance = max(1, int(round(font.getlength(ch))))
        return {
            "cp": ord(ch),
            "w": 0,
            "h": 0,
            "x": 0,
            "y": 0,
            "adv": advance,
            "bits": b"",
            "line": ascent + descent,
        }

    pad_top = 8
    origin_y = pad_top + ascent
    image = Image.new("L", (128, origin_y + descent + 8), 0)
    draw = ImageDraw.Draw(image)
    draw.text((2, origin_y), ch, font=font, fill=255, anchor="ls")
    bbox = image.getbbox()
    advance = max(1, int(round(font.getlength(ch))))
    if bbox is None:
        return {
            "cp": ord(ch),
            "w": 0,
            "h": 0,
            "x": 0,
            "y": 0,
            "adv": advance,
            "bits": b"",
            "line": ascent + descent,
        }

    left, top, right, bottom = bbox
    width = right - left
    height = bottom - top
    bits = bytearray()
    acc = 0
    nbit = 0
    for y in range(top, bottom):
        for x in range(left, right):
            acc = (acc << 1) | (1 if image.getpixel((x, y)) >= threshold else 0)
            nbit += 1
            if nbit == 8:
                bits.append(acc)
                acc = 0
                nbit = 0
    if nbit:
        bits.append(acc << (8 - nbit))

    x_off = left - 2
    y_off = top - pad_top
    if advance < x_off + width:
        advance = x_off + width + 1
    return {
        "cp": ord(ch),
        "w": width,
        "h": height,
        "x": x_off,
        "y": y_off,
        "adv": advance,
        "bits": bytes(bits),
        "line": ascent + descent,
    }


def bytes_literal(data: bytes) -> str:
    lines = []
    for i in range(0, len(data), 20):
        chunk = ", ".join(f"0x{b:02x}" for b in data[i : i + 20])
        lines.append(f"    {chunk},")
    return "\n".join(lines)


def main() -> None:
    bitmap = bytearray()
    face_glyphs = []
    for name, path, size in FACES:
        font = ImageFont.truetype(path, size)
        glyphs = []
        line = 0
        for ch in CHARSET:
            g = raster(font, ch)
            if g["cp"] > 0xFFFF:
                raise SystemExit(f"BMP dışı karakter: {ch}")
            g["offset"] = len(bitmap)
            bitmap.extend(g["bits"])
            line = max(line, g["line"])
            glyphs.append(g)
        glyphs.sort(key=lambda item: item["cp"])
        face_glyphs.append((name, glyphs, line))

    parts = []
    parts.append("#include \"bluecore/font.hpp\"")
    parts.append("")
    parts.append("namespace bluecore {")
    parts.append("")
    parts.append("// BlueCore UI bitmap alt kümesi.")
    parts.append("// Raster kaynağı: Inter 4.001, Copyright (c) 2016 The Inter Project Authors.")
    parts.append("// Lisans: SIL Open Font License 1.1. Ayrılmış ad kullanılmaz.")
    parts.append("// Üretim: tools/gen_font.py")
    parts.append("static const uint8_t kBitmap[] = {")
    parts.append(bytes_literal(bitmap) if bitmap else "    0x00,")
    parts.append("};")
    parts.append("")

    for name, glyphs, _line in face_glyphs:
        parts.append(f"static const Glyph k{name}Glyphs[] = {{")
        for g in glyphs:
            parts.append(
                "    {%d, %d, %d, %d, %d, %d, %du},"
                % (g["cp"], g["w"], g["h"], g["x"], g["y"], g["adv"], g["offset"])
            )
        parts.append("};")
        parts.append("")

    parts.append("const FontFace& face(FontId id) {")
    parts.append("    static const FontFace faces[] = {")
    for name, glyphs, line in face_glyphs:
        parts.append(
            "        {k%sGlyphs, %d, %d, kBitmap, sizeof(kBitmap)},"
            % (name, len(glyphs), line)
        )
    parts.append("    };")
    parts.append("    unsigned index = static_cast<unsigned>(id);")
    parts.append("    if (index >= static_cast<unsigned>(FontId::Count)) {")
    parts.append("        index = 0;")
    parts.append("    }")
    parts.append("    return faces[index];")
    parts.append("}")
    parts.append("")
    parts.append("}  // namespace bluecore")
    parts.append("")
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text("\n".join(parts), encoding="utf-8")
    print(f"wrote {OUT} glyphs={sum(len(g) for _, g, _ in face_glyphs)} bytes={len(bitmap)}")


if __name__ == "__main__":
    main()
