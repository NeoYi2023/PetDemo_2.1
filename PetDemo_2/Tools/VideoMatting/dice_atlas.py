#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# SPEC_VideoMattingAtlas.md v1.0 — Step D: SpriteDicing CLI atlas pack.
"""Dice frames_sprite via SpriteDicing CLI → atlas/atlas_*.png + sprites.json.

Downloads dice-windows-x64.exe from GitHub releases when missing.
Non-ASCII paths: copy to ASCII temp dirs, run CLI, copy results back.
"""
from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import tempfile
import zipfile
from urllib.request import urlopen, Request

from _common import (
    any_non_ascii,
    clear_atlas_outputs,
    ensure_dir,
    fatal,
    list_frame_paths,
    log,
)

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
DEFAULT_BIN_DIR = os.path.join(SCRIPT_DIR, "bin")
DEFAULT_DICE_NAME = "dice-windows-x64.exe"
# Pin a known release asset name; update when bumping.
GITHUB_RELEASE_API = "https://api.github.com/repos/elringus/sprite-dicing/releases/latest"


def parse_args(argv=None):
    p = argparse.ArgumentParser(prog="dice_atlas")
    p.add_argument("--in-dir", required=True, help="frames_sprite directory")
    p.add_argument("--out-dir", required=True, help="atlas output directory")
    p.add_argument("--unit-size", type=int, default=64)
    p.add_argument("--dice-pad", type=int, default=2)
    p.add_argument("--atlas-limit", type=int, default=2048)
    p.add_argument("--atlas-format", choices=["png", "webp", "tga"], default="png")
    p.add_argument("--trim", action="store_true", help="enable -t trim (OFF by default for animation)")
    p.add_argument("--dice-cli", default="", help="path to SpriteDicing CLI executable")
    p.add_argument("--ppu", type=float, default=100.0)
    return p.parse_args(argv)


def default_dice_path() -> str:
    return os.path.join(DEFAULT_BIN_DIR, DEFAULT_DICE_NAME)


def find_dice_cli(explicit: str) -> str:
    if explicit:
        if not os.path.isfile(explicit):
            fatal(f"dice CLI not found: {explicit}")
        return explicit
    candidate = default_dice_path()
    if os.path.isfile(candidate):
        return candidate
    # Also check PATH
    which = shutil.which("dice-windows-x64") or shutil.which("dice-windows-x64.exe") or shutil.which("dice")
    if which:
        return which
    return download_dice_cli()


def download_dice_cli() -> str:
    ensure_dir(DEFAULT_BIN_DIR)
    dest = default_dice_path()
    log(f"[dice] downloading SpriteDicing CLI → {dest}")
    try:
        req = Request(GITHUB_RELEASE_API, headers={"User-Agent": "PetDemo-VideoMatting"})
        with urlopen(req, timeout=60) as resp:
            release = json.loads(resp.read().decode("utf-8"))
    except Exception as ex:
        fatal(f"failed to query GitHub releases: {ex}")

    assets = release.get("assets") or []
    # Prefer windows x64 exe or zip containing it
    preferred = None
    for a in assets:
        name = (a.get("name") or "").lower()
        if "windows" in name and ("x64" in name or "amd64" in name):
            preferred = a
            break
    if preferred is None:
        for a in assets:
            name = (a.get("name") or "").lower()
            if "dice" in name and ("win" in name or "windows" in name):
                preferred = a
                break
    if preferred is None:
        fatal("no Windows SpriteDicing CLI asset found in latest release")

    url = preferred.get("browser_download_url")
    name = preferred.get("name") or "dice.zip"
    tmp_path = os.path.join(DEFAULT_BIN_DIR, name)
    try:
        req = Request(url, headers={"User-Agent": "PetDemo-VideoMatting"})
        with urlopen(req, timeout=300) as resp, open(tmp_path, "wb") as f:
            shutil.copyfileobj(resp, f)
    except Exception as ex:
        fatal(f"failed to download {url}: {ex}")

    if name.lower().endswith(".zip"):
        with zipfile.ZipFile(tmp_path, "r") as zf:
            members = zf.namelist()
            exe_member = None
            for m in members:
                base = os.path.basename(m).lower()
                if base == DEFAULT_DICE_NAME.lower() or (base.startswith("dice") and base.endswith(".exe")):
                    exe_member = m
                    break
            if exe_member is None:
                fatal(f"zip has no dice exe: {members}")
            zf.extract(exe_member, DEFAULT_BIN_DIR)
            extracted = os.path.join(DEFAULT_BIN_DIR, exe_member)
            # Flatten if nested
            if os.path.abspath(extracted) != os.path.abspath(dest):
                ensure_dir(os.path.dirname(dest))
                if os.path.isfile(dest):
                    os.remove(dest)
                shutil.move(extracted, dest)
        try:
            os.remove(tmp_path)
        except OSError:
            pass
    else:
        # Assume raw exe
        if os.path.abspath(tmp_path) != os.path.abspath(dest):
            if os.path.isfile(dest):
                os.remove(dest)
            shutil.move(tmp_path, dest)

    if not os.path.isfile(dest):
        fatal(f"dice CLI download did not produce {dest}")
    log(f"[dice] CLI ready: {dest}")
    return dest


def build_cmd(dice_cli, in_dir, out_dir, args) -> list:
    cmd = [
        dice_cli,
        in_dir,
        "-o", out_dir,
        "-s", str(args.unit_size),
        "-p", str(args.dice_pad),
        "-l", str(args.atlas_limit),
        "-f", args.atlas_format,
        "--ppu", str(args.ppu),
    ]
    if args.trim:
        cmd.append("-t")
    return cmd


def run_dice(args) -> int:
    frames = list_frame_paths(args.in_dir)
    if not frames:
        fatal(f"no frames in {args.in_dir}")

    dice_cli = find_dice_cli(args.dice_cli or "")
    clear_atlas_outputs(args.out_dir)

    need_temp = any_non_ascii([args.in_dir, args.out_dir, dice_cli])
    if need_temp:
        log("[dice] non-ASCII path detected; using ASCII temp dirs")
        with tempfile.TemporaryDirectory(prefix="vm_dice_") as tmp:
            tmp_in = os.path.join(tmp, "in")
            tmp_out = os.path.join(tmp, "out")
            ensure_dir(tmp_in)
            ensure_dir(tmp_out)
            for path in frames:
                shutil.copy2(path, os.path.join(tmp_in, os.path.basename(path)))
            cmd = build_cmd(dice_cli, tmp_in, tmp_out, args)
            log("[dice] CMD: " + " ".join(cmd))
            proc = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
            if proc.stdout:
                print(proc.stdout, end="" if proc.stdout.endswith("\n") else "\n", flush=True)
            if proc.stderr:
                print(proc.stderr, end="" if proc.stderr.endswith("\n") else "\n", flush=True)
            if proc.returncode != 0:
                fatal(f"SpriteDicing CLI failed with code {proc.returncode}")
            # Copy results back
            for name in os.listdir(tmp_out):
                shutil.copy2(os.path.join(tmp_out, name), os.path.join(args.out_dir, name))
    else:
        cmd = build_cmd(dice_cli, args.in_dir, args.out_dir, args)
        log("[dice] CMD: " + " ".join(cmd))
        proc = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
        if proc.stdout:
            print(proc.stdout, end="" if proc.stdout.endswith("\n") else "\n", flush=True)
        if proc.stderr:
            print(proc.stderr, end="" if proc.stderr.endswith("\n") else "\n", flush=True)
        if proc.returncode != 0:
            fatal(f"SpriteDicing CLI failed with code {proc.returncode}")

    # Normalize / locate sprites.json
    sprites_json = os.path.join(args.out_dir, "sprites.json")
    if not os.path.isfile(sprites_json):
        # Some CLI versions may nest or use different names — search
        found = None
        for name in os.listdir(args.out_dir):
            if name.lower().endswith(".json"):
                found = os.path.join(args.out_dir, name)
                break
        if found and os.path.abspath(found) != os.path.abspath(sprites_json):
            shutil.move(found, sprites_json)
        elif not found:
            fatal(f"sprites.json missing in {args.out_dir}")

    # Count atlas textures
    atlases = sorted(
        n for n in os.listdir(args.out_dir)
        if n.lower().startswith("atlas_") and n.lower().endswith(("." + args.atlas_format, ".png", ".webp", ".tga"))
    )
    # Validate entry count
    with open(sprites_json, "r", encoding="utf-8") as f:
        data = json.load(f)
    if not isinstance(data, list):
        fatal("sprites.json root must be an array")
    if len(data) != len(frames):
        log(f"[dice] WARN sprites.json entries={len(data)} frames={len(frames)} (may differ if CLI skipped empty)")

    log(f"[dice] atlases={len(atlases)} sprites={len(data)} → {args.out_dir}")
    log(f"PROGRESS stage=dice {len(data)}/{len(frames)}")
    return len(data)


def main(argv=None):
    args = parse_args(argv)
    run_dice(args)
    return 0


if __name__ == "__main__":
    sys.exit(main())
