// SPEC §13：启动时存档槽选择上下文。
using UnityEngine;

namespace PetDemo.Save
{
    public static class GameBootContext
    {
        public const int SaveSlotCount = 3;

        public static bool HasEnteredGame { get; private set; }
        public static int ActiveSlotIndex { get; private set; } = -1;
        public static GameSaveSnapshot PendingSnapshot { get; private set; }
        public static bool IsNewGame { get; private set; }

        public static void EnterSlot(int slotIndex, GameSaveSnapshot snapshot, bool isNewGame)
        {
            ActiveSlotIndex = slotIndex;
            PendingSnapshot = snapshot;
            IsNewGame = isNewGame;
            HasEnteredGame = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            HasEnteredGame = false;
            ActiveSlotIndex = -1;
            PendingSnapshot = null;
            IsNewGame = false;
        }
    }
}
