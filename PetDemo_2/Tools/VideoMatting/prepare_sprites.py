#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# SPEC_VideoMattingAtlas.md v1.0 — Step C: unified bbox crop + downsample.
"""Prepare sprites: union bbox across all frames, pad, crop uniformly, then max-side scale.

Critical: do NOT per-frame trim to different sizes (anchor jitter).
"""
from __future__ import annotations

import argparse
import os
import sys

from _common import clear_frame_pngs, fatal, list_frame_paths, log


def parse_args(argv=None):
    p = argparse.ArgumentParser(prog="prepare_sprites")
    p.add_argument("--in-dir", required=True, help="frames_rgba directory")
    p.add_argument("--out-dir", required=True, help="frames_sprite directory")
    p.add_argument("--max-side", type=int, default=512)
    p.add_argument("--pad", type=int, default=4, dest="prepare_pad")
    p.add_argument("--alpha-threshold", type=int, default=8)
    return p.parse_args(argv)


def alpha_bbox(alpha, threshold: int):
    import numpy as np

    ys, xs = np.where(alpha > threshold)
    if len(xs) == 0:
        return None
    return int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())  # x0,y0,x1,y1 inclusive


def union_box(a, b):
    if a is None:
        return b
    if b is None:
        return a
    return min(a[0], b[0]), min(a[1], b[1]), max(a[2], b[2]), max(a[3], b[3])


def prepare(args) -> int:
    from PIL import Image
    import numpy as np

    if args.max_side <= 0:
        fatal(f"max-side must be > 0, got {args.max_side}")
    if args.prepare_pad < 0:
        fatal(f"pad must be >= 0, got {args.prepare_pad}")

    frames = list_frame_paths(args.in_dir)
    if not frames:
        fatal(f"no frames in {args.in_dir}")

    # Pass 1: union bbox
    union = None
    img_w = img_h = None
    for i, path in enumerate(frames):
        im = Image.open(path).convert("RGBA")
        arr = np.asarray(im)
        if img_w is None:
            img_h, img_w = arr.shape[0], arr.shape[1]
        elif arr.shape[1] != img_w or arr.shape[0] != img_h:
            fatal(f"inconsistent frame size: {path} got {arr.shape[1]}x{arr.shape[0]}, expected {img_w}x{img_h}")
        box = alpha_bbox(arr[:, :, 3], args.alpha_threshold)
        union = union_box(union, box)
        log(f"PROGRESS stage=prepare_bbox {i + 1}/{len(frames)}")

    if union is None:
        fatal("all frames fully transparent; cannot compute bbox")

    x0, y0, x1, y1 = union
    pad = args.prepare_pad
    x0 = max(0, x0 - pad)
    y0 = max(0, y0 - pad)
    x1 = min(img_w - 1, x1 + pad)
    y1 = min(img_h - 1, y1 + pad)
    crop_w = x1 - x0 + 1
    crop_h = y1 - y0 + 1
    log(f"[prepare] union bbox=({x0},{y0})-({x1},{y1}) size={crop_w}x{crop_h} pad={pad}")

    # Scale
    scale = 1.0
    longest = max(crop_w, crop_h)
    if longest > args.max_side:
        scale = args.max_side / float(longest)
    out_w = max(1, int(round(crop_w * scale)))
    out_h = max(1, int(round(crop_h * scale)))
    log(f"[prepare] scale={scale:.6f} → {out_w}x{out_h} (max_side={args.max_side})")

    clear_frame_pngs(args.out_dir)

    # Pass 2: crop + resize
    for i, path in enumerate(frames):
        im = Image.open(path).convert("RGBA")
        cropped = im.crop((x0, y0, x1 + 1, y1 + 1))
        if scale != 1.0:
            cropped = cropped.resize((out_w, out_h), Image.Resampling.LANCZOS)
        out_path = os.path.join(args.out_dir, os.path.basename(path))
        cropped.save(out_path, format="PNG")
        # Verify size
        if cropped.size != (out_w, out_h):
            fatal(f"size mismatch after prepare: {out_path}")
        log(f"PROGRESS stage=prepare {i + 1}/{len(frames)}")

    # Acceptance check
    for path in list_frame_paths(args.out_dir):
        with Image.open(path) as im:
            w, h = im.size
            if w != out_w or h != out_h:
                fatal(f"acceptance fail: {path} size {w}x{h} != {out_w}x{out_h}")
            if max(w, h) > args.max_side:
                fatal(f"acceptance fail: {path} max side {max(w, h)} > {args.max_side}")

    log(f"[prepare] wrote {len(frames)} sprites {out_w}x{out_h} → {args.out_dir}")
    return len(frames)


def main(argv=None):
    args = parse_args(argv)
    prepare(args)
    return 0


if __name__ == "__main__":
    sys.exit(main())
