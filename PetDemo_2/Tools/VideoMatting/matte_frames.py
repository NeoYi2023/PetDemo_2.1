#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# SPEC_VideoMattingAtlas.md v1.0 — Step B: matte / remove background → RGBA.
"""Matte frames_raw → frames_rgba.

Default: HuggingFace ZhengPeng7/BiRefNet (CUDA preferred, CPU fallback).
Optional: chroma key (--chroma-key).
Contract: output same-named RGBA PNGs with alpha channel.
"""
from __future__ import annotations

import argparse
import os
import sys

from _common import clear_frame_pngs, fatal, imread_unicode, imwrite_unicode, list_frame_paths, log


DEFAULT_MODEL = "ZhengPeng7/BiRefNet"
INFER_SIZE = 1024


def parse_args(argv=None):
    p = argparse.ArgumentParser(prog="matte_frames")
    p.add_argument("--in-dir", required=True, help="frames_raw directory")
    p.add_argument("--out-dir", required=True, help="frames_rgba directory")
    p.add_argument("--device", choices=["auto", "cuda", "cpu"], default="auto")
    p.add_argument("--model-id", default=DEFAULT_MODEL)
    p.add_argument("--chroma-key", action="store_true", help="use chroma key instead of BiRefNet")
    p.add_argument("--key-color", default="0,255,0", help="R,G,B key color (chroma mode)")
    p.add_argument("--key-tolerance", type=float, default=40.0, help="chroma tolerance")
    return p.parse_args(argv)


def resolve_device(name: str) -> str:
    import torch

    if name == "cpu":
        return "cpu"
    if name == "cuda":
        if not torch.cuda.is_available():
            fatal("device=cuda requested but CUDA is not available")
        return "cuda"
    # auto
    return "cuda" if torch.cuda.is_available() else "cpu"


def parse_key_color(s: str):
    parts = [int(x.strip()) for x in s.split(",")]
    if len(parts) != 3:
        fatal(f"key-color must be R,G,B got: {s}")
    for v in parts:
        if v < 0 or v > 255:
            fatal(f"key-color channel out of range: {s}")
    return tuple(parts)  # RGB


def chroma_matte(bgr, key_rgb, tolerance: float):
    import cv2
    import numpy as np

    key_bgr = np.array([key_rgb[2], key_rgb[1], key_rgb[0]], dtype=np.float32)
    diff = np.linalg.norm(bgr.astype(np.float32) - key_bgr, axis=2)
    # Soft-ish edge: alpha 255 when far from key, 0 when close
    alpha = np.clip((diff - tolerance) / max(tolerance, 1.0) * 255.0, 0, 255).astype(np.uint8)
    bgra = cv2.cvtColor(bgr, cv2.COLOR_BGR2BGRA)
    bgra[:, :, 3] = alpha
    return bgra


def load_birefnet(model_id: str, device: str):
    import torch
    from transformers import AutoModelForImageSegmentation
    from torchvision import transforms

    log(f"[matte] loading BiRefNet model={model_id} device={device}")
    # Force float32: remote BiRefNet weights often land as Half on CUDA; ToTensor inputs are float32
    # and mismatched dtypes raise: "Input type (float) and bias type (Half) should be the same".
    model = AutoModelForImageSegmentation.from_pretrained(
        model_id, trust_remote_code=True, torch_dtype=torch.float32
    )
    model.eval()
    model.to(device)
    model.float()
    # BiRefNet typically expects ImageNet-normalized RGB tensor
    tfm = transforms.Compose(
        [
            transforms.Resize((INFER_SIZE, INFER_SIZE)),
            transforms.ToTensor(),
            transforms.Normalize([0.485, 0.456, 0.406], [0.229, 0.224, 0.225]),
        ]
    )
    return model, tfm


def birefnet_matte(model, tfm, bgr, device: str):
    import cv2
    import numpy as np
    import torch
    from PIL import Image

    h, w = bgr.shape[:2]
    rgb = cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB)
    pil = Image.fromarray(rgb)
    inp = tfm(pil).unsqueeze(0).to(device)

    # Align input dtype with model parameters (fp32 or fp16).
    try:
        param = next(model.parameters())
        if inp.dtype != param.dtype:
            inp = inp.to(dtype=param.dtype)
    except StopIteration:
        pass

    with torch.no_grad():
        out = model(inp)
        # BiRefNet returns list/tuple of preds; last is full-res mask logits
        if isinstance(out, (list, tuple)):
            pred = out[-1]
        else:
            pred = out
        if isinstance(pred, (list, tuple)):
            pred = pred[-1]
        mask = pred.sigmoid().float().cpu().numpy()
        if mask.ndim == 4:
            mask = mask[0, 0]
        elif mask.ndim == 3:
            mask = mask[0]
        mask = mask.astype(np.float32)

    mask_u8 = (np.clip(mask, 0, 1) * 255.0).astype(np.uint8)
    mask_u8 = cv2.resize(mask_u8, (w, h), interpolation=cv2.INTER_LINEAR)

    bgra = cv2.cvtColor(bgr, cv2.COLOR_BGR2BGRA)
    bgra[:, :, 3] = mask_u8
    return bgra


def run_matte(args) -> int:
    frames = list_frame_paths(args.in_dir)
    if not frames:
        fatal(f"no frames in {args.in_dir}")

    clear_frame_pngs(args.out_dir)

    if args.chroma_key:
        key = parse_key_color(args.key_color)
        log(f"[matte] chroma-key color={key} tolerance={args.key_tolerance}")
        for i, path in enumerate(frames):
            bgr = imread_unicode(path)
            if bgr.ndim == 2:
                import cv2

                bgr = cv2.cvtColor(bgr, cv2.COLOR_GRAY2BGR)
            elif bgr.shape[2] == 4:
                bgr = bgr[:, :, :3]
            bgra = chroma_matte(bgr, key, args.key_tolerance)
            out_path = os.path.join(args.out_dir, os.path.basename(path))
            imwrite_unicode(out_path, bgra)
            log(f"PROGRESS stage=matte {i + 1}/{len(frames)}")
        log(f"[matte] wrote {len(frames)} RGBA frames (chroma) → {args.out_dir}")
        return len(frames)

    device = resolve_device(args.device)
    model, tfm = load_birefnet(args.model_id, device)
    for i, path in enumerate(frames):
        bgr = imread_unicode(path)
        if bgr.ndim == 2:
            import cv2

            bgr = cv2.cvtColor(bgr, cv2.COLOR_GRAY2BGR)
        elif bgr.shape[2] == 4:
            bgr = bgr[:, :, :3]
        bgra = birefnet_matte(model, tfm, bgr, device)
        out_path = os.path.join(args.out_dir, os.path.basename(path))
        imwrite_unicode(out_path, bgra)
        log(f"PROGRESS stage=matte {i + 1}/{len(frames)}")
    log(f"[matte] wrote {len(frames)} RGBA frames (BiRefNet) → {args.out_dir}")
    return len(frames)


def main(argv=None):
    args = parse_args(argv)
    run_matte(args)
    return 0


if __name__ == "__main__":
    sys.exit(main())
