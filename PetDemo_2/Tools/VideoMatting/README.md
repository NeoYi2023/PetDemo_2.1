# Video Matting Atlas 流水线

对应 SPEC：仓库根目录 [`SPEC_VideoMattingAtlas.md`](../../../SPEC_VideoMattingAtlas.md)。

把绿幕/实拍视频压成 SpriteDicing CLI 图集（`atlas_*.png` + `sprites.json`），再在 Unity 中用 `DicedSpriteAnimationPlayer` 按帧播放。

与既有 Qwen 绿幕窗口（`Tools/PetDemo/Green Screen Video To Frames`）**并行共存**，互不替换。

---

## 一键跑通（推荐：Unity 编辑器）

1. **准备 Python 环境（首次）**  
   - 菜单：`Tools/PetDemo/Video Matting Atlas Pipeline`  
   - 点「创建/修复 venv」（或手动运行本目录 `setup_venv.bat`）  
   - 需要本机 `py -3.11`（或 3.10~3.12）与可用网络（下载 torch / BiRefNet）

2. **打开窗口**  
   `Tools/PetDemo/Video Matting Atlas Pipeline`

3. **填写视频路径**（浏览 mp4/mov/webm），其余默认即可：  
   - FPS=15，Max Side=512，Pad=4，Alpha=8  
   - Dice Unit=64，Pad=2，Limit=2048，Trim=关  
   - 模型=`ZhengPeng7/BiRefNet`

4. **运行全流程** → 等待日志出现 `DONE`  
   - 成功后若勾选「完成后自动导入」，会把 `atlas/` 拷到 `Assets/Art/VFX/<视频名>/` 并生成 Prefab

5. **预览**  
   - 将 `DicedSpriteAnimation.prefab` 拖入场景，进 Play Mode（默认循环 15fps）

---

## 命令行跑通

```bat
cd PetDemo_2\Tools\VideoMatting
setup_venv.bat
.venv\Scripts\python.exe run_pipeline.py --video "D:\path\to\clip.mp4"
```

输出：

```
output/<stem>/
  frames_raw/
  frames_rgba/
  frames_sprite/
  atlas/          # atlas_0.png … + sprites.json
```

分步跳过示例：

```bat
.venv\Scripts\python.exe run_pipeline.py --video clip.mp4 --skip-extract --skip-matte
```

绿幕色键（不跑 BiRefNet）：

```bat
.venv\Scripts\python.exe run_pipeline.py --video clip.mp4 --chroma-key --key-color 0,255,0 --key-tolerance 40
```

---

## 目录与契约

| 产物 | 说明 |
|------|------|
| `frames_raw` | 目标 fps 抽稀 RGB |
| `frames_rgba` | 抠图 RGBA（原分辨率） |
| `frames_sprite` | **全帧并集 bbox** 统一裁切 + 最长边≤max_side |
| `atlas/sprites.json` | 数组；加载后按 `frame_XXXXXX` 排序播放 |

默认参数见 SPEC §2.3；Unity 窗口默认值与 CLI 保持一致。

---

## 运行时组件

- `PetDemo.UI.VideoMatting.DicedSpriteAnimationPlayer`  
  - `Play(loop, fps)` / `Stop` / `Pause` / `Resume`  
  - 引用 `sprites.json`（TextAsset）+ `atlas_*` 纹理数组

既有 `DicedSpriteSequencePlayer`（LangRen_DZ / Unity-package diced sprites）不受影响。
