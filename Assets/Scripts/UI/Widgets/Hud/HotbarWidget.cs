using UnityEngine;

namespace UI.Widgets.Hud
{
    public class HotbarWidget : MonoBehaviour
    {
        [SerializeField] private HotbarSlot[] _slots;

        public int Count => _slots.Length;

        public void SetAvailable(int index, bool isAvailable)
        {
            if (IsValid(index))
            {
                _slots[index].SetAvailable(isAvailable);
            }
        }

        public void Select(int index)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i].SetSelected(i == index);
            }
        }

        public void SetCharge(int index, float? value)
        {
            if (IsValid(index))
            {
                _slots[index].SetCharge(value);
            }
        }

        private bool IsValid(int index) => index >= 0 && index < _slots.Length;
    }
}
