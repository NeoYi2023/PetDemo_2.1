# -*- coding: utf-8 -*-
"""Objective transform search: re-dice current frames, then pixel-compare every
flip/uv-origin candidate against ground-truth frames_sprite PNGs."""
import json
import math
import os
import subprocess
import sys

from PIL import Image

BASE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "output")
VENV_PY = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".venv", "Scripts", "python.exe")
DICE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "dice_atlas.py")


def find_frames(count):
    for d in os.listdir(BASE):
        fs = os.path.join(BASE, d, "frames_sprite")
        if os.path.isdir(fs) and len(os.listdir(fs)) == count:
            return fs
    raise SystemExit("frames not found")


def redice(frames_dir, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    cmd = [VENV_PY, DICE, "--in-dir", frames_dir, "--out-dir", out_dir,
           "--unit-size", "64", "--dice-pad", "2", "--atlas-limit", "2048", "--ppu", "100"]
    r = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if r.returncode != 0:
        sys.stdout.buffer.write((r.stdout or "").encode("utf-8", "replace"))
        sys.stdout.buffer.write((r.stderr or "").encode("utf-8", "replace"))
        raise SystemExit("dice failed")


def build(entry, atlas, src_y_down, fv_flip, out_flip, ppu=100):
    """Reconstruct one frame. Returns PIL image (y-down) sized by rect."""
    AW, AH = atlas.size
    vs, uvs = entry["vertices"], entry["uvs"]
    r = entry["rect"]
    x0, y0, w, h = r["x"], r["y"], r["width"], r["height"]
    texW = max(1, math.ceil(w * ppu))
    texH = max(1, math.ceil(h * ppu))
    out = Image.new("RGBA", (texW, texH), (0, 0, 0, 0))
    px = out.load()
    ap = atlas.load()
    for b in range(0, len(vs) - 3, 4):
        bl, tr = vs[b], vs[b + 2]
        ub, ut = uvs[b], uvs[b + 2]
        dx0 = round((bl["x"] - x0) * ppu)
        dy0 = round((bl["y"] - y0) * ppu)
        dx1 = round((tr["x"] - x0) * ppu)
        dy1 = round((tr["y"] - y0) * ppu)
        if dx1 <= dx0 or dy1 <= dy0:
            continue
        su0, su1 = ub["u"], ut["u"]
        sv0, sv1 = ub["v"], ut["v"]
        for y in range(dy0, dy1):
            if y < 0 or y >= texH:
                continue
            fv = (y - dy0) / (dy1 - dy0)
            if fv_flip:
                fv = 1.0 - fv
            sv = sv0 + fv * (sv1 - sv0)
            sy = int(sv * AH) if src_y_down else int((1.0 - sv) * AH)
            sy = max(0, min(AH - 1, sy))
            oy = (texH - 1 - y) if out_flip else y  # dst row in image (y-down) space
            for x in range(dx0, dx1):
                if x < 0 or x >= texW:
                    continue
                fu = (x - dx0) / (dx1 - dx0)
                su = su0 + fu * (su1 - su0)
                sx = max(0, min(AW - 1, int(su * AW)))
                c = ap[sx, sy]
                if c[3] > 0:
                    px[x, oy] = c
    return out


def diff_score(img, gt):
    """Pixel diff ratio over union bbox, alpha-aware. Lower is better."""
    if img.size != gt.size:
        return float("inf"), -1.0
    w, h = img.size
    ip, gp = img.load(), gt.load()
    diff = 0
    both = 0
    union = 0
    for y in range(h):
        for x in range(w):
            a = ip[x, y]
            b = gp[x, y]
            if a[3] > 10 or b[3] > 10:
                union += 1
                if a[3] > 10 and b[3] > 10:
                    both += 1
                    diff += abs(a[0] - b[0]) + abs(a[1] - b[1]) + abs(a[2] - b[2])
                else:
                    diff += 255 * 3
    if union == 0:
        return float("inf"), 0.0
    iou = both / union
    return diff / (union * 255 * 3), iou


def main():
    fs_dir = find_frames(48)
    red = os.path.join(BASE, "ZJDH_rest_2_fresh")
    redice(fs_dir, red)

    entries = json.load(open(os.path.join(red, "sprites.json"), encoding="utf-8"))
    atlas = Image.open(os.path.join(red, "atlas_0.png")).convert("RGBA")
    print("atlas", atlas.size, "entries", len(entries))

    # Use a middle frame for verification
    entry = entries[len(entries) // 2]
    fid = entry["id"]
    gt_path = os.path.join(fs_dir, fid + ".png")
    gt = Image.open(gt_path).convert("RGBA")
    r = entry["rect"]
    texW = max(1, math.ceil(r["width"] * 100))
    texH = max(1, math.ceil(r["height"] * 100))
    print("frame", fid, "gt", gt.size, "rect", texW, texH)

    best = None
    for src_y_down in (True, False):
        for fv_flip in (True, False):
            for out_flip in (True, False):
                img = build(entry, atlas, src_y_down, fv_flip, out_flip)
                score, iou = diff_score(img, gt)
                name = f"src={'uvdown' if src_y_down else 'vup'} fvflip={fv_flip} outflip={out_flip}"
                print(f"{name}: diff={score:.4f} iou={iou:.4f}")
                img.save(os.path.join(BASE, f"cand_{int(src_y_down)}{int(fv_flip)}{int(out_flip)}.png"))
                if best is None or score < best[0]:
                    best = (score, name)
    print("BEST:", best)


if __name__ == "__main__":
    main()
