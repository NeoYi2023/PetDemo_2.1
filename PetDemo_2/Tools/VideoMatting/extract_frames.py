#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# SPEC_VideoMattingAtlas.md v1.0 — Step A: extract frames at target fps (time-uniform).
"""Extract RGB PNG frames from a video at a target FPS.

Sampling uses source_fps / target_fps index mapping (not every-N drop).
Writes frames_raw/frame_XXXXXX.png. Clears old frames before writing.
"""
from __future__ import annotations

import argparse
import os
import sys

from _common import clear_frame_pngs, ensure_dir, fatal, frame_stem, imwrite_unicode, log


def parse_args(argv=None):
    p = argparse.ArgumentParser(prog="extract_frames")
    p.add_argument("--video", required=True, help="input video path")
    p.add_argument("--out-dir", required=True, help="output frames_raw directory")
    p.add_argument("--fps", type=float, default=15.0, help="target FPS (default 15)")
    return p.parse_args(argv)


def extract(video_path: str, out_dir: str, target_fps: float) -> int:
    import cv2

    if not os.path.isfile(video_path):
        fatal(f"video not found: {video_path}")
    if target_fps <= 0:
        fatal(f"fps must be > 0, got {target_fps}")

    # OpenCV VideoCapture may fail on non-ASCII paths on Windows; copy to temp if needed.
    open_path = video_path
    tmp_copy = None
    try:
        video_path.encode("ascii")
    except UnicodeEncodeError:
        import tempfile
        import shutil

        fd, tmp_copy = tempfile.mkstemp(suffix=os.path.splitext(video_path)[1] or ".mp4")
        os.close(fd)
        shutil.copy2(video_path, tmp_copy)
        open_path = tmp_copy
        log(f"[extract] non-ASCII video path; using temp copy: {open_path}")

    cap = cv2.VideoCapture(open_path)
    if not cap.isOpened():
        if tmp_copy and os.path.isfile(tmp_copy):
            os.remove(tmp_copy)
        fatal(f"failed to open video: {video_path}")

    src_fps = float(cap.get(cv2.CAP_PROP_FPS) or 0.0)
    frame_count = int(cap.get(cv2.CAP_PROP_FRAME_COUNT) or 0)
    if src_fps <= 0:
        src_fps = 30.0
        log(f"[extract] WARN unknown source fps; assuming {src_fps}")

    clear_frame_pngs(out_dir)
    ensure_dir(out_dir)

    # Time-uniform: pick source indices round(i * src_fps / target_fps)
    if frame_count <= 0:
        # Fallback: read sequentially and pick by timestamp
        duration = 0.0
        # Will discover duration while reading
        indices = None
    else:
        duration = frame_count / src_fps
        n_out = max(1, int(round(duration * target_fps)))
        indices = []
        for i in range(n_out):
            t = i / target_fps
            idx = int(round(t * src_fps))
            if idx >= frame_count:
                idx = frame_count - 1
            indices.append(idx)
        # Deduplicate while preserving order (short clips / fps mismatch)
        dedup = []
        seen = set()
        for idx in indices:
            if idx not in seen:
                seen.add(idx)
                dedup.append(idx)
        # Prefer keeping evenly spaced count; if too many dups, keep unique ascending
        if len(dedup) < max(1, int(duration * target_fps * 0.5)):
            # Very short / low src fps: just use unique indices
            indices = dedup
        else:
            # Keep mapped indices even with rare dups skipped — recompute unique preserving spacing
            indices = []
            last = -1
            for i in range(n_out):
                idx = int(round((i / target_fps) * src_fps))
                if idx >= frame_count:
                    idx = frame_count - 1
                if idx != last:
                    indices.append(idx)
                    last = idx

    written = 0
    if indices is not None:
        for out_i, src_i in enumerate(indices):
            cap.set(cv2.CAP_PROP_POS_FRAMES, src_i)
            ok, frame = cap.read()
            if not ok or frame is None:
                log(f"[extract] WARN failed read source frame {src_i}, skip")
                continue
            out_path = os.path.join(out_dir, frame_stem(written) + ".png")
            imwrite_unicode(out_path, frame)
            written += 1
            if written % 10 == 0 or written == len(indices):
                log(f"PROGRESS stage=extract {written}/{len(indices)}")
    else:
        # Sequential timestamp sampling
        next_t = 0.0
        frame_i = 0
        while True:
            ok, frame = cap.read()
            if not ok or frame is None:
                break
            t = frame_i / src_fps
            if t + 1e-6 >= next_t:
                out_path = os.path.join(out_dir, frame_stem(written) + ".png")
                imwrite_unicode(out_path, frame)
                written += 1
                next_t = written / target_fps
                log(f"PROGRESS stage=extract {written}/?")
            frame_i += 1

    cap.release()
    if tmp_copy and os.path.isfile(tmp_copy):
        try:
            os.remove(tmp_copy)
        except OSError:
            pass

    if written <= 0:
        fatal("no frames extracted")
    log(f"[extract] wrote {written} frames to {out_dir} (src_fps={src_fps:.3f}, target_fps={target_fps})")
    return written


def main(argv=None):
    args = parse_args(argv)
    extract(args.video, args.out_dir, args.fps)
    return 0


if __name__ == "__main__":
    sys.exit(main())
