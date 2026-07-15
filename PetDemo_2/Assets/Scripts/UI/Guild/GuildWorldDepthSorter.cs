// SPEC §9.8.9.16 (v3.231)：公会场景角色 Y 轴深度遮挡 —
// 主角与全部 NPC 同挂 Npcs 组，按各自在 GongHuiWorldContent 的局部 Y 动态重排 sibling：
// Y 越低（屏幕越靠下）sibling index 越大 → 越晚绘制 → 越靠前，遮挡 Y 更高的角色。
// 与 §9.1.4 家园语义一致，但不用 Canvas.overrideSorting（避免破坏公会屏顶层 UI 覆盖关系），改用同父 sibling 重排。
using System.Collections.Generic;
using UnityEngine;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class GuildWorldDepthSorter : MonoBehaviour
    {
        private RectTransform worldContentRt;
        private RectTransform characterParentRt;
        private readonly List<RectTransform> characters = new List<RectTransform>();

        // 复用缓冲：每帧过滤激活角色并按 Y 降序排序，避免 GC。
        private readonly List<RectTransform> sortBuffer = new List<RectTransform>();

        public void Initialize(RectTransform worldContent, RectTransform characterParent)
        {
            worldContentRt = worldContent;
            characterParentRt = characterParent;
        }

        /// <summary>登记参与深度排序的角色根（主角 + 各 NPC marker）。</summary>
        public void SetCharacters(IList<RectTransform> characterRoots)
        {
            characters.Clear();
            if (characterRoots == null)
                return;
            for (int i = 0; i < characterRoots.Count; i++)
            {
                if (characterRoots[i] != null)
                    characters.Add(characterRoots[i]);
            }
        }

        private void LateUpdate()
        {
            if (worldContentRt == null || characters.Count == 0)
                return;

            // 收集当前激活、且父节点确为角色容器的角色。
            sortBuffer.Clear();
            for (int i = 0; i < characters.Count; i++)
            {
                var rt = characters[i];
                if (rt == null || !rt.gameObject.activeInHierarchy)
                    continue;
                if (characterParentRt != null && rt.parent != characterParentRt)
                    continue;
                sortBuffer.Add(rt);
            }

            if (sortBuffer.Count <= 1)
                return;

            // 按 content 局部 Y 降序：Y 大者排前（sibling 小、靠后），Y 小者排后（sibling 大、靠前）。
            // sortY = RectTransform 枢轴（非 Sprite Custom Pivot）；脚底装饰须 pivot.y=0。
            sortBuffer.Sort(CompareByContentYDescending);

            // 节流：目标顺序与现状一致则整帧跳过 SetSiblingIndex。
            if (AlreadyOrdered(sortBuffer))
                return;

            for (int i = 0; i < sortBuffer.Count; i++)
                sortBuffer[i].SetSiblingIndex(i);
        }

        private int CompareByContentYDescending(RectTransform a, RectTransform b)
        {
            float ya = worldContentRt.InverseTransformPoint(a.position).y;
            float yb = worldContentRt.InverseTransformPoint(b.position).y;
            return yb.CompareTo(ya);
        }

        // 已按目标顺序排列：sorted[i] 的当前 sibling index 应严格递增。
        private static bool AlreadyOrdered(List<RectTransform> sorted)
        {
            int prev = -1;
            for (int i = 0; i < sorted.Count; i++)
            {
                int idx = sorted[i].GetSiblingIndex();
                if (idx <= prev)
                    return false;
                prev = idx;
            }
            return true;
        }
    }
}
