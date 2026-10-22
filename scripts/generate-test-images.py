#!/usr/bin/env python3
"""Generates deterministic PNG screenshots used for manual and automated verification.

Pure standard library (zlib) so the test fixtures can be regenerated on any machine.
Run from the repository root: python scripts/generate-test-images.py
"""
from __future__ import annotations

import os
import struct
import zlib

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "assets", "test-images")


def rgba(color) -> bytes:
    return bytes((color[0], color[1], color[2], 255))


class Canvas:
    def __init__(self, width: int, height: int, color=(246, 247, 249)):
        self.width = width
        self.height = height
        self.px = bytearray(rgba(color) * (width * height))

    def _index(self, x: int, y: int) -> int:
        return (y * self.width + x) * 4

    def pixel(self, x: int, y: int, color):
        if 0 <= x < self.width and 0 <= y < self.height:
            i = self._index(x, y)
            self.px[i : i + 4] = rgba(color)

    def rect(self, x: int, y: int, w: int, h: int, color, radius: int = 0):
        if radius <= 0:
            for yy in range(max(0, y), min(self.height, y + h)):
                row = yy * self.width
                for xx in range(max(0, x), min(self.width, x + w)):
                    i = (row + xx) * 4
                    self.px[i : i + 4] = rgba(color)
            return
        for yy in range(y, y + h):
            for xx in range(x, x + w):
                dx = min(xx - x, x + w - 1 - xx)
                dy = min(yy - y, y + h - 1 - yy)
                if dx < radius and dy < radius:
                    if (radius - dx) ** 2 + (radius - dy) ** 2 > radius * radius:
                        continue
                self.pixel(xx, yy, color)

    def circle(self, cx: int, cy: int, r: int, color):
        for yy in range(cy - r, cy + r + 1):
            for xx in range(cx - r, cx + r + 1):
                if (xx - cx) ** 2 + (yy - cy) ** 2 <= r * r:
                    self.pixel(xx, yy, color)

    def line(self, x0: int, y0: int, x1: int, y1: int, color, thickness: int = 1):
        dx = abs(x1 - x0)
        dy = -abs(y1 - y0)
        sx = 1 if x0 < x1 else -1
        sy = 1 if y0 < y1 else -1
        err = dx + dy
        while True:
            for oy in range(-(thickness // 2), thickness // 2 + 1):
                for ox in range(-(thickness // 2), thickness // 2 + 1):
                    self.pixel(x0 + ox, y0 + oy, color)
            if x0 == x1 and y0 == y1:
                break
            e2 = 2 * err
            if e2 >= dy:
                err += dy
                x0 += sx
            if e2 <= dx:
                err += dx
                y0 += sy

    def text_block(self, x: int, y: int, widths, color, height: int = 8, gap: int = 6):
        for i, width in enumerate(widths):
            self.rect(x, y + i * (height + gap), width, height, color, radius=2)

    def png(self) -> bytes:
        raw = bytearray()
        stride = self.width * 4
        for y in range(self.height):
            raw.append(0)
            raw.extend(self.px[y * stride : (y + 1) * stride])

        def chunk(kind: bytes, data: bytes) -> bytes:
            body = struct.pack(">I", len(data)) + kind + data
            return body + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)

        return (
            b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", struct.pack(">IIBBBBB", self.width, self.height, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(bytes(raw), 6))
            + chunk(b"IEND", b"")
        )


BG = (246, 247, 249)
WHITE = (255, 255, 255)
INK = (32, 36, 44)
MUTED = (138, 146, 160)
BORDER = (226, 230, 236)
ACCENT_A = (0, 103, 192)
ACCENT_B = (122, 61, 240)
GREEN = (16, 124, 86)
AVATAR_A = (255, 196, 120)
AVATAR_B = (120, 196, 255)


def dashboard(width: int, height: int, after: bool) -> Canvas:
    s = width / 1920.0
    c = Canvas(width, height, BG)

    def r(x, y, w, h, color, radius=0):
        c.rect(int(x * s), int(y * s), int(w * s), int(h * s), color, int(radius * s))

    # Top navigation
    r(0, 0, 1920, 72, WHITE)
    r(0, 71, 1920, 1, BORDER)
    r(28, 20, 32, 32, INK, 8)
    c.text_block(int(72 * s), int(28 * s), [int(90 * s)], INK, height=int(14 * s), gap=0)
    for i, x in enumerate([430, 540, 650, 760]):
        r(x, 31, 84, 10, INK if i == 0 else MUTED, 5)
    c.circle(int(1800 * s), int(36 * s), int(18 * s), AVATAR_B if after else AVATAR_A)
    r(1840, 21, 52, 30, (240, 242, 246), 8)

    # Sidebar
    r(0, 72, 248, 1008, WHITE)
    r(247, 72, 1, 1008, BORDER)
    r(24, 112, 120, 12, INK, 6)
    for i in range(6):
        y = 160 + i * 56
        if i == 1:
            r(16, y - 12, 216, 40, (232, 240, 252), 8)
        c.circle(int(40 * s), int((y + 2) * s), int(7 * s), ACCENT_A if i == 1 else MUTED)
        r(60, y - 4, 108 - (i % 3) * 12, 10, INK if i == 1 else (95, 104, 118), 5)

    # Page header
    r(296, 112, 420, 22, INK, 6)
    r(296, 148, 520, 10, MUTED, 5)
    r(1590, 104, 250, 48, ACCENT_B if after else ACCENT_A, 10)
    r(1622, 122, 186, 12, WHITE, 6)

    if after:
        # New release notification card only in the "after" screenshot
        r(296, 196, 1544, 92, (238, 232, 255), 12)
        c.circle(int(336 * s), int(242 * s), int(18 * s), ACCENT_B)
        r(370, 220, 420, 12, INK, 6)
        r(370, 242, 640, 10, (90, 80, 120), 5)
        r(1660, 224, 140, 34, ACCENT_B, 8)
        content_top = 316
    else:
        content_top = 212

    # Stat cards
    for i in range(3):
        x = 296 + i * 528
        r(x, content_top, 496, 132, WHITE, 12)
        r(x + 24, content_top + 24, 150, 10, MUTED, 5)
        r(x + 24, content_top + 52, 180, 24, INK, 6)
        trend_color = GREEN if i != 1 else (196, 43, 28)
        r(x + 24, content_top + 94, 96, 10, trend_color, 5)
        r(x + 400, content_top + 20, 72, 72, (240, 243, 247), 12)

    # Chart card
    chart_top = content_top + 164
    r(296, chart_top, 1024, 404, WHITE, 12)
    r(320, chart_top + 24, 260, 14, INK, 6)
    r(320, chart_top + 58, 160, 9, MUTED, 4)
    bars = [120, 178, 96, 220, 164, 260, 208, 292, 246, 318, 276, 332]
    for i, value in enumerate(bars):
        x = 344 + i * 78
        r(x, chart_top + 350 - value, 48, value, ACCENT_B if after and i == 9 else ACCENT_A, 6)
        r(x + 8, chart_top + 362, 32, 8, MUTED, 3)

    # Right column: activity + avatar card
    r(1352, chart_top, 488, 196, WHITE, 12)
    r(1376, chart_top + 24, 180, 12, INK, 6)
    for i in range(3):
        y = chart_top + 60 + i * 42
        c.circle(int(1392 * s), int((y + 6) * s), int(11 * s), (AVATAR_B if after else AVATAR_A) if i == 0 else (222, 226, 232))
        r(1416, y, 240 - i * 26, 9, (95, 104, 118), 4)
        r(1740, y, 72, 9, MUTED, 4)

    # Dynamic avatar tile (used to demonstrate ignore regions)
    r(1352, chart_top + 220, 488, 184, WHITE, 12)
    r(1376, chart_top + 244, 140, 12, INK, 6)
    c.circle(int(1596 * s), int((chart_top + 324) * s), int(44 * s), AVATAR_B if after else AVATAR_A)
    c.circle(int(1580 * s), int((chart_top + 312) * s), int(5 * s), (60, 40, 20))
    c.circle(int(1612 * s), int((chart_top + 312) * s), int(5 * s), (60, 40, 20))
    c.line(int(1596 * s), int((chart_top + 324) * s), int(1596 * s), int((chart_top + 338) * s), (60, 40, 20), max(1, int(2 * s)))

    # Footer
    r(296, height - int(52 * s), 1544, 1, BORDER)
    r(296, height - int(36 * s), 320, 9, MUTED, 4)

    return c


def size_mismatch(a_size, b_size) -> tuple[Canvas, Canvas]:
    a = dashboard(*a_size, after=False)
    b = dashboard(*b_size, after=True)
    return a, b


def main() -> None:
    os.makedirs(OUT, exist_ok=True)

    fixtures = {
        "dashboard-before.png": dashboard(1920, 1080, after=False),
        "dashboard-after.png": dashboard(1920, 1080, after=True),
        "dashboard-4k-before.png": dashboard(3840, 2160, after=False),
        "dashboard-4k-after.png": dashboard(3840, 2160, after=True),
    }

    small_a, small_b = size_mismatch((1600, 1000), (1720, 1080))
    fixtures["layout-before.png"] = small_a
    fixtures["layout-after.png"] = small_b

    for name, canvas in fixtures.items():
        path = os.path.join(OUT, name)
        with open(path, "wb") as handle:
            handle.write(canvas.png())
        print(f"{name}: {canvas.width}x{canvas.height} ({os.path.getsize(path):,} bytes)")


if __name__ == "__main__":
    main()
