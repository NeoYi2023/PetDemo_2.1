// SPEC §13：退出 Play 时自动写回当前槽位。
using PetDemo.Farm;
using UnityEngine;

namespace PetDemo.Save
{
    [DisallowMultipleComponent]
    public sealed class GameSaveCoordinator : MonoBehaviour
    {
        private void OnApplicationQuit()
        {
            TrySaveActiveSlot();
        }

        private void OnDestroy()
        {
            TrySaveActiveSlot();
        }

        public static void TrySaveActiveSlot()
        {
            if (!GameBootContext.HasEnteredGame || GameBootContext.ActiveSlotIndex < 0)
                return;

            var service = PlantingService.Instance;
            if (service == null)
                return;

            var snapshot = service.ExportSnapshot();
            if (snapshot == null)
                return;

            GameSaveRepository.Save(GameBootContext.ActiveSlotIndex, snapshot);
        }
    }
}
