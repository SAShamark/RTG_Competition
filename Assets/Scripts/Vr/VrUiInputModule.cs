using UnityEngine;
using UnityEngine.EventSystems;

namespace Vr
{
    public sealed class VrUiInputModule : PointerInputModule
    {
        private const float DoubleClickTime = 0.3f;

        private Vector2 _position;
        private bool _hasPointer;
        private bool _isPressed;
        private bool _wasPressed;

        public void SetPointer(bool hasPointer, Vector2 position, bool isPressed)
        {
            _hasPointer = hasPointer;
            _position = position;
            _isPressed = hasPointer && isPressed;
        }

        public override void Process()
        {
            GetPointerData(kMouseLeftId, out PointerEventData pointer, true);
            bool isPressedNow = _isPressed && !_wasPressed;
            bool isReleasedNow = !_isPressed && _wasPressed;
            _wasPressed = _isPressed;

            pointer.Reset();
            pointer.delta = _hasPointer ? _position - pointer.position : Vector2.zero;
            pointer.position = _position;
            pointer.scrollDelta = Vector2.zero;
            pointer.button = PointerEventData.InputButton.Left;
            if (_hasPointer)
            {
                eventSystem.RaycastAll(pointer, m_RaycastResultCache);
                pointer.pointerCurrentRaycast = FindFirstRaycast(m_RaycastResultCache);
                m_RaycastResultCache.Clear();
            }
            else
            {
                pointer.pointerCurrentRaycast = default;
            }

            ProcessPress(pointer, isPressedNow, isReleasedNow);
            ProcessMove(pointer);
            ProcessDrag(pointer);
        }

        private void ProcessPress(PointerEventData pointer, bool isPressedNow, bool isReleasedNow)
        {
            GameObject currentOverGo = pointer.pointerCurrentRaycast.gameObject;
            if (isPressedNow)
            {
                pointer.eligibleForClick = true;
                pointer.delta = Vector2.zero;
                pointer.dragging = false;
                pointer.useDragThreshold = true;
                pointer.pressPosition = pointer.position;
                pointer.pointerPressRaycast = pointer.pointerCurrentRaycast;
                DeselectIfSelectionChanged(currentOverGo, pointer);

                GameObject newClick = ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentOverGo);
                GameObject newPressed = ExecuteEvents.ExecuteHierarchy(currentOverGo, pointer, ExecuteEvents.pointerDownHandler) ?? newClick;
                float time = Time.unscaledTime;
                bool isRepeat = newPressed == pointer.lastPress && time - pointer.clickTime < DoubleClickTime;
                pointer.clickCount = isRepeat ? pointer.clickCount + 1 : 1;
                pointer.pointerPress = newPressed;
                pointer.rawPointerPress = currentOverGo;
                pointer.pointerClick = newClick;
                pointer.clickTime = time;
                pointer.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(currentOverGo);
                if (pointer.pointerDrag != null)
                {
                    ExecuteEvents.Execute(pointer.pointerDrag, pointer, ExecuteEvents.initializePotentialDrag);
                }
            }

            if (!isReleasedNow)
            {
                return;
            }

            ExecuteEvents.Execute(pointer.pointerPress, pointer, ExecuteEvents.pointerUpHandler);
            GameObject clickHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentOverGo);
            if (pointer.pointerClick == clickHandler && pointer.eligibleForClick)
            {
                ExecuteEvents.Execute(pointer.pointerClick, pointer, ExecuteEvents.pointerClickHandler);
            }

            bool wasDragging = pointer.pointerDrag != null && pointer.dragging;
            if (wasDragging)
            {
                ExecuteEvents.ExecuteHierarchy(currentOverGo, pointer, ExecuteEvents.dropHandler);
                ExecuteEvents.Execute(pointer.pointerDrag, pointer, ExecuteEvents.endDragHandler);
            }

            pointer.eligibleForClick = false;
            pointer.pointerPress = null;
            pointer.rawPointerPress = null;
            pointer.pointerClick = null;
            pointer.dragging = false;
            pointer.pointerDrag = null;
            if (currentOverGo != pointer.pointerEnter)
            {
                HandlePointerExitAndEnter(pointer, null);
                HandlePointerExitAndEnter(pointer, currentOverGo);
            }
        }
    }
}
