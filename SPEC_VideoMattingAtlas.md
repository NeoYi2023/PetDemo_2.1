# SPEC：绿幕/实拍动画 → SpriteDicing 图集流水线

**版本：** 1.9  
**日期：** 2026-08-03  
**状态：** 实现中  
**关联：** 与 `SPEC_FarmBattleDemo.md` §9.14.11 既有 Qwen / Unity-package `DicedSpriteAtlas` 路径**并行共存**；本 SPEC 定义 BiRefNet（或色键）+ SpriteDicing **CLI**（`sprites.json` + `atlas_*.png`）+ mesh 运行时播放器方案。不替换 LangRen_DZ / `DicedSpriteSequencePlayer` 既有链路。**自 v1.3**：`ZJDH_rest_2` / `ZJDH_study_2` 接入 HomeTab 点击特效随机池。**自 v1.4**：HomeTab 改为 `DicedSpriteAtlasSequencePlayer` 烘焙 Sprite + uGUI Image（弃用 Overlay 直画 CLI mesh）。**自 v1.9**：新增与 CLI **并存** 的 Unity-package 并行路径（`frames_sprite` → `diced_sprites`），HomeTab 默认走 package，可用开关回退 CLI bake。

> **v1.2 抠图精度：** BiRefNet 加载强制 `float32`（`torch_dtype=float32` + `model.float()`），推理输入 dtype 与模型参数对齐，避免 CUDA 上 Half 权重与 float 输入混用报错。

---

## 1. 目标与原则

把一段动作剧烈的绿幕（或纯色背景）动画视频，压成游戏可加载的 diced sprite atlas，并在 Unity 中按固定帧率顺序播放。

### 1.1 体积策略优先级（高 → 低）

1. 减帧（默认 **15fps**，不用源视频全帧）
2. 全序列统一 bbox 裁掉透明空白
3. 最长边降采样（默认 **≤512**）
4. SpriteDicing 切块去重打包（辅）

### 1.2 关键约束

- **禁止**对每帧独立 trim 到不同尺寸（会导致锚点跳动）；必须先求「全帧并集 bbox」，再统一裁切，保证每帧画布同尺寸。
- Editor **不重写算法**，只负责收集参数、调用外部流水线、导入资源、生成 Prefab。
- 界面默认值必须与流水线 CLI / 本 SPEC 默认值一致，避免两套参数漂移。

---

## 2. 端到端流水线

### 2.1 输入 / 输出

| 项 | 约定 |
|----|------|
| 输入 | 一个视频文件（`mp4` / `mov` / `webm` 等） |
| 输出根目录 | `output/<视频文件名不含扩展名>/`（相对流水线工作目录，或 Editor 指定的绝对路径） |

子目录：

| 子目录 | 职责 |
|--------|------|
| `frames_raw/` | 按目标 fps 抽稀的 RGB PNG 序列 |
| `frames_rgba/` | 抠图后的 RGBA PNG（原分辨率，透明背景） |
| `frames_sprite/` | 统一裁切 + 降采样后的 RGBA（喂给打图集） |
| `atlas/` | 图集纹理 `atlas_*.png` + 元数据 `sprites.json` |

文件命名：`frame_{index:06d}.png`（从 `000000` 起，连续递增）。

### 2.2 编排入口

- 一条命令跑完全流程：`run_pipeline.py`
- 分步跳过：`--skip-extract` / `--skip-matte` / `--skip-prepare` / `--skip-dice`

### 2.3 默认参数

| 参数 | 默认值 |
|------|--------|
| `fps` | `15` |
| `max_side` | `512` |
| `prepare_pad` | `4` |
| `alpha_threshold` | `8` |
| dice `unit_size` (`-s`) | `64` |
| dice `pad` (`-p`) | `2` |
| `atlas_limit` (`-l`) | `2048` |
| `trim` (`-t`) | `false`（动画默认关闭 per-sprite trim） |
| atlas format (`-f`) | `png` |
| 抠图模型 | HuggingFace `ZhengPeng7/BiRefNet`（CUDA 优先，无 CUDA 回退 CPU） |

### 2.4 工具目录（本仓库）

```
PetDemo_2/Tools/VideoMatting/
  extract_frames.py
  matte_frames.py
  prepare_sprites.py
  dice_atlas.py
  run_pipeline.py
  requirements.txt
  setup_venv.bat
  README.md
  bin/                    # SpriteDicing CLI（可自动下载，gitignore）
  .venv/                  # 本地 venv（gitignore）
  output/                 # 默认输出根（gitignore 可选）
```

依赖：`opencv-python`、`Pillow`、`torch`、`torchvision`、`transformers`、`einops`（BiRefNet modeling 必需）、`timm`、`tqdm` 等（见 `requirements.txt`）；打图集用 SpriteDicing CLI（不自写打包器）。

---

## 3. 各步骤算法规格

### 3.1 步骤 A：抽帧（`extract_frames.py`）

1. OpenCV 读视频，按目标 fps **时间均匀采样**（用 `src_fps / target_fps` 映射源帧索引；**不要**简单每隔 N 帧丢弃）。
2. 写出 `frames_raw/frame_XXXXXX.png`。
3. Windows 中文路径：`cv2.imwrite` 可能失败 → 用 `imencode` 后写 bytes。
4. 重跑前清空该目录旧 `frame_*.png`，避免残留脏索引。

### 3.2 步骤 B：抠图（`matte_frames.py`）

1. 输入 `frames_raw`，输出 `frames_rgba`（同名 PNG，RGBA）。
2. 默认 BiRefNet 分割前景，把 mask 作为 alpha 写回。
3. 可选绿幕 chroma key（`--chroma-key` + key color + tolerance）；接口契约不变：输出必须是带 alpha 的 RGBA 序列。
4. 推理输入可 resize 到 **1024** 做分割，再把 mask **双线性**缩回原图尺寸。
5. 重跑前清空旧 RGBA。
6. 设备：`auto` / `cuda` / `cpu`（`auto` = 有 CUDA 用 CUDA，否则 CPU）。

### 3.3 步骤 C：精灵预处理（`prepare_sprites.py`，关键不可省）

输入 `frames_rgba` → 输出 `frames_sprite`：

1. 对每帧：取 `alpha > alpha_threshold` 的像素包围盒；
2. 对所有帧包围盒求**并集**；
3. 并集外扩 `pad` 像素，并钳制到图像范围内；
4. 所有帧按**同一矩形**裁切（结果同宽同高）；
5. 若 `max(w,h) > max_side`：整体等比缩放到最长边 = `max_side`（LANCZOS）；
6. 写出 `frames_sprite/frame_XXXXXX.png`。

**验收：** 全部同尺寸，且最长边 ≤ `max_side`。

### 3.4 步骤 D：打图集（`dice_atlas.py`）

1. 对 `frames_sprite` 调用 SpriteDicing CLI（Windows：`dice-windows-x64.exe`；不存在则从 GitHub `elringus/sprite-dicing` releases 下载）。
2. 产出到 `atlas/`：`atlas_0.png`, `atlas_1.png`, … 与 `sprites.json`。
3. 参数：`-s 64 -p 2 -l 2048 -f png`；动画**不要**开 `-t` trim（除非用户显式开启）。
4. 若输入/输出路径含非 ASCII（中文路径）：先复制到 ASCII 临时目录跑 CLI，再拷回目标目录。
5. 重跑前清空旧 `atlas_*.*` 与 `sprites.json`。

---

## 4. `sprites.json` 契约

`sprites.json` 是**数组**，长度 = 序列帧数。每项大致为：

```json
{
  "id": "frame_000000",
  "atlas": 0,
  "rect": { "x": 0, "y": 0, "width": 100, "height": 100 },
  "vertices": [{ "x": 0, "y": 0 }, ...],
  "uvs": [{ "u": 0, "v": 0 }, ...],
  "indices": [0, 1, 2, ...]
}
```

注意：

- 数组顺序不一定按 `id` 排序；加载后必须按 `id`（或解析 `frame_XXXXXX` 数字）排序再播放。
- `vertices` 与 `uvs` 一一对应；用 `indices` 组成三角形网格。
- `vertices` 为局部坐标（已按 CLI `--ppu` 换算；默认 PPU=100）；播放时应用节点位置/缩放/旋转。

---

## 5. Unity 运行时播放

### 5.1 组件

`PetDemo.UI.VideoMatting.DicedSpriteAnimationPlayer`（`MonoBehaviour`）

资源：

- `TextAsset spritesJson`
- `Texture2D[] atlases`（按 `atlas` 索引取用）

接口：

| 方法 | 行为 |
|------|------|
| `Play(bool loop, float fps = 15)` | 从第 0 帧开始播放 |
| `Stop()` | 停止并重置时间 |
| `Pause()` / `Resume()` | 暂停 / 继续 |

每帧：`frameIndex = floor(t * fps) % count`（loop）；或播完停在末帧（非 loop）。

渲染：

- `MeshFilter` + `MeshRenderer`（或等价），用当前帧的 `vertices` + `uvs` + `indices` 更新 mesh，采样对应 atlas 纹理。
- 开启透明混合（PNG 常见非预乘；材质用 `Sprites/Default` 或等效 Alpha Blend）。
- UV：若与 Unity 约定不一致，运行时对 V 做翻转（组件暴露 `flipUvV`，默认 `false`；按实测调整）。
- **不要**每帧新建纹理；只切换 mesh/UV 与 atlas 材质主贴图索引。

与既有 `DicedSpriteSequencePlayer`（uGUI Image + Unity-package diced sprites）**独立**，互不替换。

### 5.2 HomeTab 点击特效接入（自 v1.3；v1.4 改烘焙 Sprite）

`HomeTabPanel` 营救完成后点击 `RoleMount`/`RoleClickHitbox` 时，从以下三者等概率选 1 个播一次（详见 `SPEC_FarmBattleDemo.md` §9.14.11 v3.282）：

| 候选 | 播放器 | 运行时资源 |
|------|--------|------------|
| LangRen_DZ | `DicedSpriteSequencePlayer` | `Resources/SpriteDicing/LangRen_DZ _1/diced_sprites` |
| ZJDH_rest_2 | 见 §5.3（默认 package） | package 或 CLI，由开关选择 |
| ZJDH_study_2 | 见 §5.3（默认 package） | package 或 CLI，由开关选择 |

`PetDemo.UI.VideoMatting.DicedSpriteAtlasSequencePlayer`（静态；**CLI 方案，保留不动**）：

- 首次加载时按 CLI dice 块局部坐标把 `atlas_*` 像素**CPU 粘贴**成逐帧 `Sprite` 并缓存（`atlas_*.png` 需 `isReadable=true`）；之后与 LangRen 相同，在 `DicedFxImage` 上按 15fps 切帧播一次。
- **不**在 Overlay Canvas 上直接画 CLI mesh（`DicedSpriteCanvasPlayer` / `MaskableGraphic` 路径已弃用：会把整张 atlas 以 UV0–1 铺满 Rect，表现为图集碎片）。
- **UV 坐标约定（v1.8 客观验证，IoU 0.978）**：SpriteDicing CLI 输出的 `sprites.json` 中 `uv.u/v` 以**图集 PNG 左上角为原点**（y 向下）：`pilRow = v * atlasH`。`Texture2D.GetPixel` 为 y-up，采样必须 `GetPixel(sx, atlasH-1-pilRow)`；块内 `fv` **不翻转**。顶点 `vertices.x/y` 实际为 y-down 屏幕语义（`bl.y` 小值 = 帧顶部）。**写入 `Texture2D` 时先按 `row = oy` 写全部块，再对整帧做一次垂直翻转**；块内逐行翻转会把每个 dice 块竖直撕开（图集碎片）。验证方法：`_objective_test.py` 以 `frames_sprite` 原帧为基准对 8 种组合做像素级 diff。
- 显示约定与 LangRen 一致：`localScale.xy=0.7`、`anchoredPosition.y=277`。

Editor Prefab 预览仍用 `DicedSpriteAnimationPlayer`（`MeshRenderer`）。`Assets/Art/VFX/ZJDH_*` 可作预览目录；CLI 运行时以 `Resources/VideoMatting/` 为准。

### 5.3 Unity-package 并行播放路径（A/B，自 v1.9）

与 §5.2 CLI bake **并存、不互相覆盖**。目的：用已验证的 LangRen 同款链路提升 ZJDH HomeTab 画质，便于对比。

| 项 | 约定 |
|----|------|
| 源帧 | `Tools/VideoMatting/output/ZJDH_*/frames_sprite/`（流水线已产出） |
| 导入 | `Assets/Resources/SpriteDicing/ZJDH_rest_2/frames_sprite/`、`.../ZJDH_study_2/frames_sprite/` |
| 解耦输出 | `Assets/Resources/SpriteDicing/ZJDH_*/diced_sprites/` |
| Atlas | `Assets/Art/Animations/ZJDH_*/ZJDH_*_Atlas.asset` |
| 构建 | `Tools/PetDemo/Build ZJDH Unity Package Atlases` → 拷贝帧（若空）→ Sprite importer → `DicedAtlasBuilder.BuildForAnim` |
| 播放 | `DicedSpriteSequencePlayer.PlayOnce(Image, resourcesPath)`（泛化路径缓存） |

**HomeTab 开关**（`HomeTabPanelView.UseZjdhUnityPackagePath`，默认 `true`）：

- `true`：ZJDH 走 `SpriteDicing/ZJDH_*/diced_sprites` + `DicedSpriteSequencePlayer`
- `false`：ZJDH 走 `VideoMatting/ZJDH_*` + `DicedSpriteAtlasSequencePlayer`（原 CLI bake）

随机池仍为三选一（LangRen / rest / study）；仅 ZJDH 后端可切换。`Resources/VideoMatting/**`、CLI Prefab、bake 播放器代码**不得删除**。

---

## 6. Unity 编辑器操作界面

### 6.1 形态

- `EditorWindow`：`PetDemo.EditorTools.VideoMattingAtlasPipelineWindow`
- 菜单：`Tools/PetDemo/Video Matting Atlas Pipeline`
- 状态持久化：`EditorPrefs`（前缀 `PetDemo.VideoMatting.`）

### 6.2 可编辑字段

与 §2.3 / CLI 一一对应：

**输入：** 原视频绝对路径（浏览 mp4/mov/webm）；输出根目录（可选；默认 `Tools/VideoMatting/output/<stem>/`）。

**抽帧：** Target FPS（15）

**抠图：** 设备 Auto/CUDA/CPU；模型 ID（`ZhengPeng7/BiRefNet`）；「使用绿幕色键」+ Key Color + Tolerance

**预处理：** Max Side / BBox Pad / Alpha Threshold

**SpriteDicing：** Unit Size / Dice Pad / Atlas Limit / Format / Trim

**分步：** Skip Extract/Matte/Prepare/Dice；或分按钮「仅抽帧」「仅抠图」「仅预处理」「仅打图集」「全流程」

**环境与导入：** Python 解释器、`run_pipeline.py`、dice CLI 路径（可浏览+可用性检测）；播放 FPS；Unity 导入目标目录（默认 `Assets/Art/VFX/<Name>/`）；是否自动导入

### 6.3 操作与反馈

按钮：运行全流程 / 停止 / 打开输出目录 / 导入刷新到工程 / 生成更新播放 Prefab

反馈：滚动日志、成功失败与耗时、参数校验阻断、处理中禁用重复运行、Python/dice 缺失明确报错。

复用 `PetDemo.EditorTools.AsyncProcessRunner` 异步跑子进程。

### 6.4 对接约定

收集参数 → 拼命令行 → 调 `run_pipeline.py` → 监控退出码 → 导入 `atlas_*.png` + `sprites.json` → 生成 Prefab（挂好 `DicedSpriteAnimationPlayer` 引用）。中文路径如实传递；非 ASCII 兼容由流水线临时目录处理。

---

## 7. CLI 接口

### 7.1 `run_pipeline.py`

```
python run_pipeline.py --video <path> [--out-root <dir>]
  [--fps 15] [--max-side 512] [--prepare-pad 4] [--alpha-threshold 8]
  [--device auto|cuda|cpu] [--model-id ZhengPeng7/BiRefNet]
  [--chroma-key] [--key-color R,G,B] [--key-tolerance 40]
  [--unit-size 64] [--dice-pad 2] [--atlas-limit 2048] [--atlas-format png] [--trim]
  [--dice-cli <path>]
  [--skip-extract] [--skip-matte] [--skip-prepare] [--skip-dice]
```

各子脚本也可独立调用，参数子集与上表一致。

### 7.2 退出码

- `0`：成功
- 非 0：失败（stderr / stdout 含 `FATAL ...`）

进度约定：stdout 打印可读进度（如 `PROGRESS stage=extract n/total`），供 Editor 解析（可选）。

---

## 8. 实现优先级

| 优先级 | 内容 |
|--------|------|
| P0 | 本 SPEC |
| P1 | 抽帧 / 抠图 / 预处理 / 打图集 + `run_pipeline` |
| P2 | `DicedSpriteAnimationPlayer` |
| P3 | EditorWindow（参数、调用、日志） |
| P4 | 自动导入 + Prefab 生成 |
| P5 | 真实视频验收 |

---

## 9. 明确不做 / 可变通

- 不做时序稳定抠图、不接 Qwen-Image-Layered（既有 Qwen 窗口仍保留）。
- 抠图模型可换，RGBA 输出契约不变。
- 从更高 fps 旧结果迁移时：应重新按目标 fps 抽帧并重新抠图，不要对旧序列隔帧丢弃。
- 不做成运行时玩家可操作的调参 UI（仅 Editor）。

---

## 10. 验收标准

流水线：

1. 默认参数下能从视频产出 `frames_raw` / `frames_rgba` / `frames_sprite` / `atlas`
2. `frames_sprite` 全部同尺寸，最长边 ≤ `max_side`
3. `atlas` 含 `atlas_*.png` + `sprites.json`，条目数 = 序列帧数
4. 相对「全帧原分辨率未裁切」方案，atlas 张数或总体积有明显下降

Unity 运行时：

5. Prefab/场景中可按设定 fps 循环播放，锚点稳定不乱跳
6. 帧顺序与 `frame_XXXXXX` 一致；透明混合正确

Unity 编辑器：

7. 从菜单打开窗口，只改视频路径与少量参数即可跑通全流程
8. 参数会保存，重启编辑器仍在
9. 跑完后 Assets 内出现 atlas 与 json，Prefab 引用正确可预览
10. Skip 分步、「打开输出目录」、非法路径/缺失 Python 的错误提示均可用

---

## 11. 变更记录

| 版本 | 日期 | 说明 |
|------|------|------|
| 1.9 | 2026-08-03 | 新增 §5.3 Unity-package 并行路径：`ZJDH_*` 的 `frames_sprite` → `DicedAtlasBuilder` → `diced_sprites`；HomeTab `UseZjdhUnityPackagePath` 默认 true，可回退 CLI bake；不覆盖 `Resources/VideoMatting/`。 |
| 1.8 | 2026-08-03 | 客观验证（`_objective_test.py`，对 frames_sprite 原帧像素级 diff，IoU 0.978）确定唯一正确组合：src=uv原点左上 + fv不翻转 + dst整帧翻转一次。修复 `GetPixel` y-up 未换算（`atlasH-1-pilRow`）导致的碎片。Resources 数据从 190px 宽新帧重新 dice 刷新。 |
| 1.7 | 2026-08-03 | 修正 `DicedSpriteAtlasSequencePlayer.BakeFrame`：写入 `Texture2D` 时先按 `row = oy` 不翻转写全部块，再对整帧做一次垂直翻转；此前块内逐行 `row = texH-1-oy` 会把每个 dice 块竖直撕开，导致图集碎片。 |
| 1.6 | 2026-08-03 | 实测确认 CLI `sprites.json` UV 以**图集左上角**为原点（y 向下），块内 fv 不翻转；修复 ZJDH 烘焙出"图集碎片/黑图"的根因。重跑 `dice_atlas.py` 刷新 `ZJDH_rest_2`/`ZJDH_study_2` 的 `Resources/VideoMatting/` 数据。 |
| 1.5 | 2026-08-03 | HomeTab ZJDH 烘焙改 CPU 像素粘贴（按 dice 块局部坐标把 atlas 像素贴回源帧），弃用 GL/RT；`atlas_*.png` 需 `isReadable=true`。 |
| 1.3 | 2026-08-03 | HomeTab 点击特效接入：`DicedSpriteCanvasPlayer` + `Resources/VideoMatting/ZJDH_rest_2`/`ZJDH_study_2`；与 LangRen_DZ 组成三选一随机池（`SPEC_FarmBattleDemo.md` v3.281）。 |
| 1.2 | 2026-08-03 | BiRefNet CUDA 推理强制 float32，并对齐输入/权重 dtype，修复 `Input type (float) and bias type (Half)`。 |
| 1.1 | 2026-08-03 | 补 BiRefNet 依赖 `einops`（`requirements.txt`）；缺包会导致 modeling 加载 FATAL。 |
| 1.0 | 2026-08-03 | 初版：BiRefNet/色键 + 统一 bbox 预处理 + SpriteDicing CLI + Editor + mesh 播放器；与 §9.14.11 Qwen 路径并行。落地目录：`PetDemo_2/Tools/VideoMatting/`；菜单 `Tools/PetDemo/Video Matting Atlas Pipeline`；运行时 `PetDemo.UI.VideoMatting.DicedSpriteAnimationPlayer`。 |
