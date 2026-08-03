#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# SPEC_VideoMattingAtlas.md v1.0 — orchestrate extract → matte → prepare → dice.
"""End-to-end Video Matting Atlas pipeline.

Example:
  python run_pipeline.py --video clip.mp4
  python run_pipeline.py --video clip.mp4 --skip-extract --skip-matte
"""
from __future__ import annotations

import argparse
import os
import sys
import time

from _common import ensure_dir, fatal, log

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
DEFAULT_OUTPUT_ROOT = os.path.join(SCRIPT_DIR, "output")


def parse_args(argv=None):
    p = argparse.ArgumentParser(prog="run_pipeline")
    p.add_argument("--video", required=True, help="input video path")
    p.add_argument(
        "--out-root",
        default="",
        help="output root (default: Tools/VideoMatting/output/<video_stem>/)",
    )
    # Extract
    p.add_argument("--fps", type=float, default=15.0)
    # Matte
    p.add_argument("--device", choices=["auto", "cuda", "cpu"], default="auto")
    p.add_argument("--model-id", default="ZhengPeng7/BiRefNet")
    p.add_argument("--chroma-key", action="store_true")
    p.add_argument("--key-color", default="0,255,0")
    p.add_argument("--key-tolerance", type=float, default=40.0)
    # Prepare
    p.add_argument("--max-side", type=int, default=512)
    p.add_argument("--prepare-pad", type=int, default=4)
    p.add_argument("--alpha-threshold", type=int, default=8)
    # Dice
    p.add_argument("--unit-size", type=int, default=64)
    p.add_argument("--dice-pad", type=int, default=2)
    p.add_argument("--atlas-limit", type=int, default=2048)
    p.add_argument("--atlas-format", choices=["png", "webp", "tga"], default="png")
    p.add_argument("--trim", action="store_true")
    p.add_argument("--dice-cli", default="")
    p.add_argument("--ppu", type=float, default=100.0)
    # Skip flags
    p.add_argument("--skip-extract", action="store_true")
    p.add_argument("--skip-matte", action="store_true")
    p.add_argument("--skip-prepare", action="store_true")
    p.add_argument("--skip-dice", action="store_true")
    return p.parse_args(argv)


def default_out_root(video_path: str) -> str:
    stem = os.path.splitext(os.path.basename(video_path))[0]
    if not stem:
        fatal("cannot derive video stem from path")
    return os.path.join(DEFAULT_OUTPUT_ROOT, stem)


def main(argv=None):
    # Ensure sibling imports work when invoked with absolute script path
    if SCRIPT_DIR not in sys.path:
        sys.path.insert(0, SCRIPT_DIR)

    args = parse_args(argv)

    if not os.path.isfile(args.video):
        fatal(f"video not found: {args.video}")
    if args.fps <= 0:
        fatal(f"fps must be > 0, got {args.fps}")
    if args.max_side <= 0:
        fatal(f"max-side must be > 0, got {args.max_side}")

    out_root = args.out_root.strip() if args.out_root else default_out_root(args.video)
    out_root = os.path.abspath(out_root)
    ensure_dir(out_root)

    frames_raw = os.path.join(out_root, "frames_raw")
    frames_rgba = os.path.join(out_root, "frames_rgba")
    frames_sprite = os.path.join(out_root, "frames_sprite")
    atlas_dir = os.path.join(out_root, "atlas")

    log(f"[pipeline] video={args.video}")
    log(f"[pipeline] out_root={out_root}")
    log(
        f"[pipeline] fps={args.fps} max_side={args.max_side} pad={args.prepare_pad} "
        f"alpha_th={args.alpha_threshold} unit={args.unit_size} dice_pad={args.dice_pad} "
        f"limit={args.atlas_limit} format={args.atlas_format} trim={args.trim}"
    )

    t0 = time.time()

    if not args.skip_extract:
        import extract_frames

        extract_frames.extract(args.video, frames_raw, args.fps)
    else:
        log("[pipeline] skip extract")

    if not args.skip_matte:
        import matte_frames

        matte_args = argparse.Namespace(
            in_dir=frames_raw,
            out_dir=frames_rgba,
            device=args.device,
            model_id=args.model_id,
            chroma_key=args.chroma_key,
            key_color=args.key_color,
            key_tolerance=args.key_tolerance,
        )
        matte_frames.run_matte(matte_args)
    else:
        log("[pipeline] skip matte")

    if not args.skip_prepare:
        import prepare_sprites

        prep_args = argparse.Namespace(
            in_dir=frames_rgba,
            out_dir=frames_sprite,
            max_side=args.max_side,
            prepare_pad=args.prepare_pad,
            alpha_threshold=args.alpha_threshold,
        )
        prepare_sprites.prepare(prep_args)
    else:
        log("[pipeline] skip prepare")

    if not args.skip_dice:
        import dice_atlas

        dice_args = argparse.Namespace(
            in_dir=frames_sprite,
            out_dir=atlas_dir,
            unit_size=args.unit_size,
            dice_pad=args.dice_pad,
            atlas_limit=args.atlas_limit,
            atlas_format=args.atlas_format,
            trim=args.trim,
            dice_cli=args.dice_cli,
            ppu=args.ppu,
        )
        dice_atlas.run_dice(dice_args)
    else:
        log("[pipeline] skip dice")

    elapsed = time.time() - t0
    log(f"[pipeline] DONE in {elapsed:.1f}s → {out_root}")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except SystemExit:
        raise
    except Exception as ex:
        fatal(f"unhandled: {ex}")
