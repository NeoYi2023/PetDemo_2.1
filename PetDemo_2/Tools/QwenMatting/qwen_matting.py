#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# SPEC §9.14.11 v3.277/v3.278：Qwen-Image-Layered 分层抠图 CLI。
# 子命令：preview / batch / check / purge-cache。
"""Qwen-Image-Layered layered matting CLI for the Unity green-screen pipeline.

Sub-commands:
  preview      decompose one image and write a contact-sheet PNG of all layers
  batch        decompose every frame_*.png in a folder, keep one layer per frame
  check        print environment info (torch/CUDA/VRAM/disk/pipeline)
  purge-cache  remove project model cache and optional broken system cache
"""
import argparse
import os
import shutil
import sys

MODEL_ID = "Qwen/Qwen-Image-Layered"
# Qwen-Image-Layered 完整权重约数十 GB；下载+解压余量建议 ≥40GB。
MIN_FREE_GB = 40.0
REQUIRED_SUBDIRS = ("transformer", "text_encoder", "vae", "scheduler", "tokenizer")


def parse_args(argv):
    p = argparse.ArgumentParser(prog="qwen_matting")
    sub = p.add_subparsers(dest="cmd", required=True)

    def add_model_args(sp):
        sp.add_argument("--layers", type=int, default=4, help="number of RGBA layers (3~4 recommended)")
        sp.add_argument("--steps", type=int, default=50, help="num_inference_steps")
        sp.add_argument("--resolution", type=int, default=640, help="resolution bucket (640 recommended)")
        sp.add_argument("--true-cfg-scale", type=float, default=4.0)
        sp.add_argument("--seed", type=int, default=777)
        sp.add_argument("--prompt", default="", help="optional caption of the whole image")
        sp.add_argument("--source", choices=["modelscope", "hf", "hf-mirror", "local"],
                        default="modelscope", help="weight source (modelscope for CN network)")
        sp.add_argument("--model-path", default="", help="local weights dir (source=local)")
        sp.add_argument("--offload", choices=["model", "sequential", "none"], default="model",
                        help="model = enable_model_cpu_offload (default, for 12GB VRAM)")
        sp.add_argument("--cache-dir", default="",
                        help="model cache root (default: MODELSCOPE_CACHE / HF_HOME / ./model_cache)")
        sp.add_argument("--min-free-gb", type=float, default=MIN_FREE_GB,
                        help="abort download if free space on cache drive is below this")

    sp = sub.add_parser("preview", help="decompose one image, write layer contact sheet")
    sp.add_argument("--image", required=True)
    sp.add_argument("--out", required=True)
    add_model_args(sp)

    sp = sub.add_parser("batch", help="decompose all frame_*.png, keep --layer-index layer")
    sp.add_argument("--input-dir", required=True)
    sp.add_argument("--out-dir", required=True)
    sp.add_argument("--layer-index", type=int, default=1, help="subject layer index (0 = bottom)")
    sp.add_argument("--no-resize-back", action="store_true",
                    help="keep layer resolution instead of resizing to input size")
    add_model_args(sp)

    sp = sub.add_parser("check", help="print environment info and exit")
    sp.add_argument("--cache-dir", default="", help="model cache root to report free space for")

    sp = sub.add_parser("purge-cache", help="remove broken / project model caches")
    sp.add_argument("--cache-dir", default="", help="project model cache root to delete")
    sp.add_argument("--also-system", action="store_true",
                    help="also delete broken ~/.cache/modelscope/.../Qwen--Qwen-Image-Layered")
    return p.parse_args(argv)


def log(msg):
    print(msg, flush=True)


def fatal(msg, code=1):
    log("FATAL " + msg)
    sys.exit(code)


def default_cache_dir():
    for key in ("MODELSCOPE_CACHE", "HF_HOME", "HUGGINGFACE_HUB_CACHE"):
        val = os.environ.get(key, "").strip()
        if val:
            return os.path.abspath(val)
    # CLI 直接运行时的回退：脚本旁 model_cache（Unity 会注入工程 Library 路径）
    here = os.path.dirname(os.path.abspath(__file__))
    return os.path.join(here, "model_cache")


def resolve_cache_dir(args):
    if getattr(args, "cache_dir", None) and args.cache_dir.strip():
        return os.path.abspath(args.cache_dir.strip())
    return default_cache_dir()


def free_bytes(path):
    """Return free bytes on the volume that contains path (creating parents if needed)."""
    probe = os.path.abspath(path)
    # Find an existing ancestor so GetDiskFreeSpace works before mkdir.
    while probe and not os.path.exists(probe):
        parent = os.path.dirname(probe)
        if parent == probe:
            break
        probe = parent
    if not probe or not os.path.exists(probe):
        probe = os.path.abspath(os.sep)
    try:
        usage = shutil.disk_usage(probe)
        return usage.free
    except Exception:
        return -1


def ensure_free_space(cache_dir, min_free_gb):
    free = free_bytes(cache_dir)
    if free < 0:
        log("WARNING cannot determine free disk space for " + cache_dir)
        return
    free_gb = free / (1024.0 ** 3)
    log("CACHE_DIR " + cache_dir)
    log("FREE_GB %.2f" % free_gb)
    if free_gb < min_free_gb:
        fatal(
            "磁盘空间不足：缓存盘仅剩 %.2f GB，建议至少 %.0f GB 用于下载 Qwen-Image-Layered。\n"
            "请清理磁盘，或把缓存目录放到更大的盘（工程 Library/PetDemoQwenMatting/model_cache）。\n"
            "若系统盘 C: 上有残缺缓存，可运行: python qwen_matting.py purge-cache --also-system"
            % (free_gb, min_free_gb),
            code=4)


def apply_cache_env(cache_dir):
    """Force ModelScope / HuggingFace hubs to write into the project-drive cache."""
    os.makedirs(cache_dir, exist_ok=True)
    os.environ["MODELSCOPE_CACHE"] = cache_dir
    os.environ["HF_HOME"] = cache_dir
    os.environ["HUGGINGFACE_HUB_CACHE"] = os.path.join(cache_dir, "hub")
    os.environ["TRANSFORMERS_CACHE"] = os.path.join(cache_dir, "transformers")


def has_weight_file(directory):
    if not os.path.isdir(directory):
        return False
    for name in os.listdir(directory):
        lower = name.lower()
        if lower.endswith(".incomplete"):
            continue
        if lower.endswith(".safetensors") or lower.endswith(".bin") or lower.endswith(".ckpt"):
            return True
        if lower.endswith(".safetensors.index.json") or lower.endswith(".bin.index.json"):
            # sharded index alone is not enough — look for at least one shard
            continue
    # sharded: model-00001-of-0000N.safetensors
    for name in os.listdir(directory):
        if name.endswith(".safetensors") or name.endswith(".bin"):
            return True
    return False


def find_incomplete_files(root):
    hits = []
    if not os.path.isdir(root):
        return hits
    for dirpath, _, filenames in os.walk(root):
        for name in filenames:
            if name.endswith(".incomplete"):
                hits.append(os.path.join(dirpath, name))
    return hits


def validate_model_dir(model_dir):
    """Raise fatal if download looks incomplete."""
    if not os.path.isdir(model_dir):
        fatal("model directory not found: " + model_dir)
    incompletes = find_incomplete_files(model_dir)
    if incompletes:
        sample = "\n  ".join(incompletes[:8])
        more = "" if len(incompletes) <= 8 else ("\n  ... and %d more" % (len(incompletes) - 8))
        fatal(
            "模型缓存不完整（发现 %d 个 *.incomplete，常见原因：磁盘已满）。\n"
            "路径: %s\n"
            "残缺文件示例:\n  %s%s\n"
            "请先清理磁盘，再运行: python qwen_matting.py purge-cache --also-system\n"
            "然后重新 preview/batch 下载完整权重。"
            % (len(incompletes), model_dir, sample, more),
            code=4)
    missing = []
    for sub in REQUIRED_SUBDIRS:
        sub_path = os.path.join(model_dir, sub)
        if not os.path.isdir(sub_path):
            missing.append(sub + "/ (missing)")
            continue
        # tokenizer / scheduler may only have json configs; weight-bearing dirs need weights
        if sub in ("transformer", "text_encoder", "vae") and not has_weight_file(sub_path):
            missing.append(sub + "/ (no weight file)")
    if missing:
        fatal(
            "模型缓存缺少关键文件（可能下载被中断）：\n  - "
            + "\n  - ".join(missing)
            + "\n路径: " + model_dir
            + "\n请 purge-cache 后重新下载。",
            code=4)


def system_modelscope_model_dir():
    home = os.path.expanduser("~")
    return os.path.join(home, ".cache", "modelscope", "models", "Qwen--Qwen-Image-Layered")


def resolve_model_ref(args):
    """Return a model reference usable by from_pretrained."""
    if args.source == "local":
        if not args.model_path or not os.path.isdir(args.model_path):
            fatal("--model-path must point to a local weights dir when --source local")
        validate_model_dir(args.model_path)
        return args.model_path

    cache_dir = resolve_cache_dir(args)
    apply_cache_env(cache_dir)
    ensure_free_space(cache_dir, getattr(args, "min_free_gb", MIN_FREE_GB))

    if args.source == "modelscope":
        try:
            from modelscope import snapshot_download
        except ImportError:
            fatal("modelscope not installed; run setup_venv.bat or pip install modelscope")
        log("Downloading/locating weights via ModelScope: " + MODEL_ID)
        try:
            model_dir = snapshot_download(MODEL_ID, cache_dir=cache_dir)
        except OSError as ex:
            if getattr(ex, "errno", None) == 28 or "No space left" in str(ex):
                fatal(
                    "下载中断：磁盘已满 (ENOSPC)。请清理磁盘后 purge-cache 并重试。\n原始错误: %s" % ex,
                    code=4)
            raise
        validate_model_dir(model_dir)
        return model_dir

    # hf / hf-mirror
    return MODEL_ID


def load_pipeline(args, torch):
    from diffusers import QwenImageLayeredPipeline
    model_ref = resolve_model_ref(args)
    log("Loading pipeline from: " + str(model_ref))
    # dtype preferred over deprecated torch_dtype
    try:
        pipe = QwenImageLayeredPipeline.from_pretrained(model_ref, dtype=torch.bfloat16)
    except TypeError:
        pipe = QwenImageLayeredPipeline.from_pretrained(model_ref, torch_dtype=torch.bfloat16)
    if args.offload == "model":
        pipe.enable_model_cpu_offload()
    elif args.offload == "sequential":
        pipe.enable_sequential_cpu_offload()
    else:
        pipe = pipe.to("cuda", torch.bfloat16)
    pipe.set_progress_bar_config(disable=True)
    return pipe


def decompose(pipe, image, args, torch):
    inputs = {
        "image": image,
        "generator": torch.Generator(device="cuda").manual_seed(args.seed),
        "true_cfg_scale": args.true_cfg_scale,
        "negative_prompt": " ",
        "num_inference_steps": args.steps,
        "num_images_per_prompt": 1,
        "layers": args.layers,
        "resolution": args.resolution,
        "cfg_normalize": True,
        "use_en_prompt": True,
    }
    if args.prompt:
        inputs["prompt"] = args.prompt
    with torch.inference_mode():
        output = pipe(**inputs)
    result = output.images[0]
    return list(result) if isinstance(result, (list, tuple)) else [result]


def checkerboard(size, cell=16):
    from PIL import Image, ImageDraw
    w, h = size
    board = Image.new("RGB", (w, h), (255, 255, 255))
    draw = ImageDraw.Draw(board)
    for y in range(0, h, cell):
        for x in range(0, w, cell):
            if (x // cell + y // cell) % 2 == 0:
                draw.rectangle([x, y, x + cell - 1, y + cell - 1], fill=(180, 180, 180))
    return board


def on_checker(rgba):
    from PIL import Image
    rgba = rgba.convert("RGBA")
    board = checkerboard(rgba.size)
    board.paste(rgba, (0, 0), rgba)
    return board


def cmd_preview(args, torch):
    from PIL import Image, ImageDraw
    image = Image.open(args.image).convert("RGBA")
    layers = decompose(load_pipeline(args, torch), image, args, torch)

    thumb_h = 256
    label_h = 28
    cells = []
    for i, layer in enumerate(layers):
        cell = on_checker(layer)
        scale = thumb_h / float(cell.height)
        cell = cell.resize((max(1, int(cell.width * scale)), thumb_h), Image.LANCZOS)
        canvas = Image.new("RGB", (cell.width, thumb_h + label_h), (30, 30, 30))
        canvas.paste(cell, (0, label_h))
        draw = ImageDraw.Draw(canvas)
        draw.text((6, 6), "Layer %d%s" % (i, " (bottom)" if i == 0 else
                                           (" (top)" if i == len(layers) - 1 else "")),
                  fill=(255, 255, 0))
        cells.append(canvas)

    width = sum(c.width for c in cells) + 8 * (len(cells) + 1)
    height = thumb_h + label_h + 16
    sheet = Image.new("RGB", (width, height), (10, 10, 10))
    x = 8
    for c in cells:
        sheet.paste(c, (x, 8))
        x += c.width + 8
    out_dir = os.path.dirname(os.path.abspath(args.out))
    if out_dir and not os.path.isdir(out_dir):
        os.makedirs(out_dir)
    sheet.save(args.out)
    log("LAYERS %d" % len(layers))
    log("PREVIEW_SAVED " + os.path.abspath(args.out))


def cmd_batch(args, torch):
    from PIL import Image
    if not os.path.isdir(args.input_dir):
        fatal("input dir not found: " + args.input_dir)
    frames = sorted(f for f in os.listdir(args.input_dir)
                    if f.startswith("frame_") and f.lower().endswith(".png"))
    if not frames:
        fatal("no frame_*.png in " + args.input_dir)
    if not os.path.isdir(args.out_dir):
        os.makedirs(args.out_dir)

    pipe = load_pipeline(args, torch)
    total = len(frames)
    log("TOTAL %d" % total)
    for i, name in enumerate(frames):
        src = os.path.join(args.input_dir, name)
        image = Image.open(src).convert("RGBA")
        layers = decompose(pipe, image, args, torch)
        if args.layer_index < 0 or args.layer_index >= len(layers):
            fatal("layer-index %d out of range (got %d layers); re-run preview to pick a valid index"
                  % (args.layer_index, len(layers)), code=3)
        out = layers[args.layer_index].convert("RGBA")
        if not args.no_resize_back and out.size != image.size:
            out = out.resize(image.size, Image.LANCZOS)
        out.save(os.path.join(args.out_dir, "frame_%06d.png" % i))
        image.close()
        if torch.cuda.is_available():
            torch.cuda.empty_cache()
        log("PROGRESS %d/%d" % (i + 1, total))
    log("BATCH_DONE %d" % total)


def cmd_check(args):
    log("PYTHON " + sys.version.split()[0])
    cache_dir = resolve_cache_dir(args)
    log("CACHE_DIR " + cache_dir)
    free = free_bytes(cache_dir)
    if free >= 0:
        log("FREE_GB %.2f" % (free / (1024.0 ** 3)))
        log("MIN_FREE_GB %.1f" % MIN_FREE_GB)
        if free / (1024.0 ** 3) < MIN_FREE_GB:
            log("DISK_OK False")
        else:
            log("DISK_OK True")
    sys_dir = system_modelscope_model_dir()
    log("SYSTEM_MODELSCOPE_DIR " + sys_dir)
    log("SYSTEM_MODELSCOPE_EXISTS " + str(os.path.isdir(sys_dir)))
    if os.path.isdir(sys_dir):
        incompletes = find_incomplete_files(sys_dir)
        log("SYSTEM_INCOMPLETE_FILES %d" % len(incompletes))
    try:
        import torch
    except ImportError:
        fatal("torch not installed; run setup_venv.bat first", code=2)
    log("TORCH " + torch.__version__)
    try:
        import torchvision
        log("TORCHVISION " + getattr(torchvision, "__version__", "unknown"))
    except ImportError:
        fatal(
            "torchvision not installed; Qwen2VLVideoProcessor needs it.\n"
            "Fix: run setup_venv.bat, or:\n"
            "  .venv\\Scripts\\python.exe -m pip install torchvision --index-url https://download.pytorch.org/whl/cu121",
            code=2)
    cuda = torch.cuda.is_available()
    log("CUDA_AVAILABLE " + str(cuda))
    if cuda:
        props = torch.cuda.get_device_properties(0)
        log("GPU " + props.name)
        log("VRAM_GB %.1f" % (props.total_memory / (1024.0 ** 3)))
    try:
        from diffusers import QwenImageLayeredPipeline  # noqa: F401
        log("PIPELINE_AVAILABLE True")
    except Exception:
        log("PIPELINE_AVAILABLE False")
    if not cuda:
        fatal("CUDA not available; need an NVIDIA GPU with CUDA torch build", code=2)


def _rmtree(path):
    if not os.path.exists(path):
        log("SKIP (not found) " + path)
        return 0
    size = 0
    for dirpath, _, filenames in os.walk(path):
        for name in filenames:
            try:
                size += os.path.getsize(os.path.join(dirpath, name))
            except OSError:
                pass
    shutil.rmtree(path, ignore_errors=False)
    log("REMOVED %s (≈%.2f GB)" % (path, size / (1024.0 ** 3)))
    return size


def cmd_purge_cache(args):
    total = 0
    cache_dir = resolve_cache_dir(args)
    log("Purging project cache: " + cache_dir)
    if os.path.isdir(cache_dir):
        total += _rmtree(cache_dir)
    else:
        log("SKIP (not found) " + cache_dir)

    if args.also_system:
        sys_dir = system_modelscope_model_dir()
        log("Purging system ModelScope model dir: " + sys_dir)
        if os.path.isdir(sys_dir):
            total += _rmtree(sys_dir)
        else:
            log("SKIP (not found) " + sys_dir)
        # also wipe empty-ish parent leftovers of incomplete downloads under modelscope cache root
        ms_root = os.path.join(os.path.expanduser("~"), ".cache", "modelscope")
        if os.path.isdir(ms_root):
            incompletes = find_incomplete_files(ms_root)
            log("SYSTEM_INCOMPLETE_REMAINING %d" % len(incompletes))

    log("PURGE_DONE_BYTES %d" % total)
    log("PURGE_DONE_GB %.2f" % (total / (1024.0 ** 3)))


def main(argv):
    args = parse_args(argv)
    if getattr(args, "source", None) == "hf-mirror":
        # must be set before huggingface_hub is imported
        os.environ["HF_ENDPOINT"] = "https://hf-mirror.com"

    if args.cmd == "check":
        cmd_check(args)
        return
    if args.cmd == "purge-cache":
        try:
            cmd_purge_cache(args)
        except Exception as ex:
            fatal("%s: %s" % (type(ex).__name__, ex))
        return

    try:
        import torch
    except ImportError:
        fatal("torch not installed; run setup_venv.bat first", code=2)
    if not torch.cuda.is_available():
        fatal("CUDA not available; need an NVIDIA GPU with CUDA torch build", code=2)
    try:
        import torchvision  # noqa: F401
    except ImportError:
        fatal(
            "torchvision not installed; Qwen2VLVideoProcessor needs it.\n"
            "Fix: click「创建/修复 venv」in the Unity window, or:\n"
            "  .venv\\Scripts\\python.exe -m pip install torchvision --index-url https://download.pytorch.org/whl/cu121",
            code=2)

    try:
        if args.cmd == "preview":
            cmd_preview(args, torch)
        elif args.cmd == "batch":
            cmd_batch(args, torch)
    except SystemExit:
        raise
    except OSError as ex:
        if getattr(ex, "errno", None) == 28 or "No space left" in str(ex):
            fatal("磁盘已满 (ENOSPC): %s\n请清理磁盘并 purge-cache 后重试。" % ex, code=4)
        fatal("%s: %s" % (type(ex).__name__, ex))
    except Exception as ex:
        msg = str(ex)
        if "torchvision" in msg.lower() or "Torchvision" in msg:
            fatal(
                "%s: %s\nFix: install torchvision into the venv (setup_venv.bat / CUDA cu121 index)."
                % (type(ex).__name__, ex),
                code=2)
        if "No space left" in msg or "model.safetensors" in msg or "pytorch_model.bin" in msg:
            fatal(
                "%s: %s\n提示：若刚遇磁盘满，缓存可能残缺。请运行 purge-cache --also-system 后重下。"
                % (type(ex).__name__, ex),
                code=4)
        fatal("%s: %s" % (type(ex).__name__, ex))


if __name__ == "__main__":
    main(sys.argv[1:])
