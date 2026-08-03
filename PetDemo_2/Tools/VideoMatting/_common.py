# -*- coding: utf-8 -*-
# SPEC_VideoMattingAtlas.md v1.0 — shared helpers for VideoMatting pipeline.
from __future__ import annotations

import os
import re
import shutil
import sys
from typing import Iterable, List


FRAME_RE = re.compile(r"^frame_(\d{6})\.png$", re.IGNORECASE)


def log(msg: str) -> None:
    print(msg, flush=True)


def fatal(msg: str, code: int = 1) -> None:
    log("FATAL " + msg)
    sys.exit(code)


def ensure_dir(path: str) -> None:
    os.makedirs(path, exist_ok=True)


def clear_frame_pngs(dir_path: str) -> None:
    """Remove frame_*.png in dir_path (create dir if missing)."""
    ensure_dir(dir_path)
    for name in os.listdir(dir_path):
        if FRAME_RE.match(name):
            try:
                os.remove(os.path.join(dir_path, name))
            except OSError as ex:
                fatal(f"failed to remove {name}: {ex}")


def clear_atlas_outputs(atlas_dir: str) -> None:
    ensure_dir(atlas_dir)
    for name in os.listdir(atlas_dir):
        lower = name.lower()
        if lower.startswith("atlas_") or lower == "sprites.json":
            try:
                os.remove(os.path.join(atlas_dir, name))
            except OSError as ex:
                fatal(f"failed to remove {name}: {ex}")


def list_frame_paths(dir_path: str) -> List[str]:
    if not os.path.isdir(dir_path):
        return []
    frames = []
    for name in os.listdir(dir_path):
        m = FRAME_RE.match(name)
        if m:
            frames.append((int(m.group(1)), os.path.join(dir_path, name)))
    frames.sort(key=lambda x: x[0])
    return [p for _, p in frames]


def frame_stem(index: int) -> str:
    return f"frame_{index:06d}"


def imwrite_unicode(path: str, bgr_or_bgra) -> None:
    """Write image via imencode to support non-ASCII paths on Windows."""
    import cv2  # lazy

    ext = os.path.splitext(path)[1] or ".png"
    ok, buf = cv2.imencode(ext, bgr_or_bgra)
    if not ok:
        fatal(f"cv2.imencode failed for {path}")
    ensure_dir(os.path.dirname(path) or ".")
    with open(path, "wb") as f:
        f.write(buf.tobytes())


def imread_unicode(path: str, flags=None):
    """Read image via bytes to support non-ASCII paths on Windows."""
    import cv2  # lazy
    import numpy as np

    if flags is None:
        flags = cv2.IMREAD_UNCHANGED
    with open(path, "rb") as f:
        data = np.frombuffer(f.read(), dtype=np.uint8)
    img = cv2.imdecode(data, flags)
    if img is None:
        fatal(f"failed to read image: {path}")
    return img


def path_has_non_ascii(path: str) -> bool:
    try:
        path.encode("ascii")
        return False
    except UnicodeEncodeError:
        return True


def any_non_ascii(paths: Iterable[str]) -> bool:
    return any(path_has_non_ascii(p) for p in paths)


def copytree_contents(src: str, dst: str) -> None:
    ensure_dir(dst)
    for name in os.listdir(src):
        s = os.path.join(src, name)
        d = os.path.join(dst, name)
        if os.path.isdir(s):
            if os.path.exists(d):
                shutil.rmtree(d)
            shutil.copytree(s, d)
        else:
            shutil.copy2(s, d)
