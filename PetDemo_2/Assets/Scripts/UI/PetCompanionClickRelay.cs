// SPEC §9.5.3 (v3.89) / §9.5.4 (v3.91)：精灵点击与拖动，转发至 PetCompanionPresenter。
using UnityEngine;
using UnityEngine.EventSystems;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class PetCompanionClickRelay : MonoBehaviour,
        IPointerClickHandler,
        IPointerDownHandler, IBeginDragHandler, IDragHandler, IPointerUpHandler, IEndDragHandler
    {
        private PetCompanionPresenter _presenter;
        private int _agentIndex = -1;
        private Vector2 _pointerDownScreen;
        private bool _dragStarted;

        public void Initialize(PetCompanionPresenter presenter, int agentIndex)
        {
            _presenter = presenter;
            _agentIndex = agentIndex;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_presenter == null || _agentIndex < 0)
                return;
            _pointerDownScreen = eventData.position;
            _dragStarted = false;
            _presenter.TryArmDrag(_agentIndex, eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            TryBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_presenter == null || _agentIndex < 0)
                return;

            if (!_dragStarted && HomeCharacterDragUtility.ExceedsDragThreshold(_pointerDownScreen, eventData.position))
                TryBeginDrag(eventData);

            if (_dragStarted)
                _presenter.UpdateDrag(_agentIndex, eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_presenter == null || _agentIndex < 0)
                return;
            if (_dragStarted)
                _presenter.EndDrag(_agentIndex);
            else
                _presenter.CancelArmDrag(_agentIndex);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_presenter == null || _agentIndex < 0 || !_dragStarted)
                return;
            _presenter.EndDrag(_agentIndex);
            _dragStarted = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_presenter == null || _agentIndex < 0 || _dragStarted)
                return;
            _presenter.NotifyPetClicked(_agentIndex);
        }

        private void TryBeginDrag(PointerEventData eventData)
        {
            if (_presenter == null || _agentIndex < 0 || _dragStarted)
                return;
            if (!HomeCharacterDragUtility.ExceedsDragThreshold(_pointerDownScreen, eventData.position))
                return;
            if (!_presenter.BeginDrag(_agentIndex, eventData))
                return;
            _dragStarted = true;
        }
    }
}
