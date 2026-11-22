using UnityEngine;

namespace UI.Screens.Variants.Gameplay.Widgets
{
    public sealed class HotbarWidget : MonoBehaviour
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


        public void SetItem(int index, Sprite icon, int count)
        {
            if (IsValid(index))
            {
                _slots[index].SetItem(icon, count);
            }
        }

        public void SetCount(int index, int count)
        {
            if (IsValid(index))
            {
                _slots[index].SetCount(count);
            }
        }

        private bool IsValid(int index) => index >= 0 && index < _slots.Length;
    }
}
