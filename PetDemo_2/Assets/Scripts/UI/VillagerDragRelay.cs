// SPEC §9.5.4 (v3.91)：主角拖动命中区，转发至 MainRoleCunminPresenter。
using UnityEngine;
using UnityEngine.EventSystems;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class VillagerDragRelay : MonoBehaviour,
        IPointerDownHandler, IBeginDragHandler, IDragHandler, IPointerUpHandler, IEndDragHandler
    {
        private MainRoleCunminPresenter _presenter;
        private Vector2 _pointerDownScreen;
        private bool _dragStarted;

        public void Initialize(MainRoleCunminPresenter presenter)
        {
            _presenter = presenter;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_presenter == null)
                return;
            _pointerDownScreen = eventData.position;
            _dragStarted = false;
            _presenter.TryArmDrag(eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            TryBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_presenter == null)
                return;

            if (!_dragStarted && HomeCharacterDragUtility.ExceedsDragThreshold(_pointerDownScreen, eventData.position))
                TryBeginDrag(eventData);

            if (_dragStarted)
                _presenter.UpdateDrag(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_presenter == null)
                return;
            if (_dragStarted)
                _presenter.EndDrag();
            else
                _presenter.CancelArmDrag();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_presenter == null || !_dragStarted)
                return;
            _presenter.EndDrag();
            _dragStarted = false;
        }

        private void TryBeginDrag(PointerEventData eventData)
        {
            if (_presenter == null || _dragStarted)
                return;
            if (!HomeCharacterDragUtility.ExceedsDragThreshold(_pointerDownScreen, eventData.position))
                return;
            if (!_presenter.BeginDrag(eventData))
                return;
            _dragStarted = true;
        }
    }
}
