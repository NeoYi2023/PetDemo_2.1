// SPEC §12.14.2 / §12.14.3 / §12.14.4：确定性种子与哈希（零 Unity 依赖）。
using System;
using System.Collections.Generic;

namespace PetDemo.Battle
{
    /// <summary>SPEC §12.14：多单位战随机种子工具。</summary>
    public static class GridBattleSeedUtil
    {
        public const string AllyPlacementSalt = "ally_placement";
        public const string EnemyPlacementSalt = "enemy_placement";
        public const int TargetTieBreakSalt = unchecked((int)0x54415247); // "TARG"
        public const int TieBreakRoundMultiplier = 397;

        /// <summary>FNV-1a 32-bit，跨平台稳定。</summary>
        public static int HashString(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0;
            unchecked
            {
                int hash = (int)2166136261;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619;
                }
                return hash;
            }
        }

        public static int MixSeed(int seed, params int[] parts)
        {
            unchecked
            {
                int h = seed;
                for (int i = 0; i < parts.Length; i++)
                    h = (h * 397) ^ parts[i];
                return h;
            }
        }

        public static GridBattleRng CreateRng(int seed) => new GridBattleRng(seed);

        public static void Shuffle<T>(IList<T> list, GridBattleRng rng)
        {
            if (list == null || rng == null || list.Count <= 1)
                return;
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.NextInt(i + 1);
                if (j == i)
                    continue;
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }

    /// <summary>轻量确定性 RNG（与 <see cref="System.Random"/> 同算法，固定种子可复现）。</summary>
    public sealed class GridBattleRng
    {
        private int state;

        public GridBattleRng(int seed)
        {
            state = seed;
        }

        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax <= 0)
                return 0;
            return (int)(NextUInt() % (uint)exclusiveMax);
        }

        private uint NextUInt()
        {
            state = unchecked(state * 1664525 + 1013904223);
            return (uint)state;
        }
    }
}
