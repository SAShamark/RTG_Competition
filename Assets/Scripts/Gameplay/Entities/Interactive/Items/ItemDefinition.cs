using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    [CreateAssetMenu(fileName = "Item", menuName = "Game/Item Definition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [SerializeField] private ItemType _type;
        [SerializeField] private string _displayName;
        [SerializeField] private Sprite _icon;

        public ItemType Type => _type;
        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
    }
}
