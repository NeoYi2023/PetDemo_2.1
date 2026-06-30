// SPEC §9.8.17（v3.112）：主界面 Canvas sortingOrder 频段常量。
namespace PetDemo.UI
{
    public static class MainUiSortTier
    {
        /// <summary>§9.1.4 家园世界 Y 排序上界（含）。</summary>
        public const int WorldMax = 499;

        public const int HudChrome = 1000;
        public const int HudScreen = 1100;
        public const int HudModal = 1200;
        public const int HudOverlay = 1300;
        public const int HudTop = 1400;
        public const int HudPopup = 1500;
    }
}
