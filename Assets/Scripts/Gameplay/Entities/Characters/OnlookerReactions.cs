using Gameplay.Entities.Interactive;
using UnityEngine;

namespace Gameplay.Entities.Characters
{
    public sealed class OnlookerReactions : MonoBehaviour, IToolReactive
    {
        [SerializeField] private Transform _body;
        [SerializeField] private bool _holdsPose = true;
        [SerializeField] private CharacterReactionLogic _reactions = new();

        public void React(in ToolHit hit) => _reactions.Hit(hit, out _);

        private void Awake() => _reactions.Init(_body, GetComponentsInChildren<Renderer>(), _holdsPose);

        private void LateUpdate() => _reactions.Tick(Time.deltaTime, true);
    }
}
