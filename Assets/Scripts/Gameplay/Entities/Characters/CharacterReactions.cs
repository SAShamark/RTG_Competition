using System;
using Gameplay.Entities.Characters.Residents;
using Gameplay.Entities.Interactive;
using UnityEngine;

namespace Gameplay.Entities.Characters
{
    public sealed class CharacterReactions : MonoBehaviour, IToolReactive
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private Rescuable _rescuable;
        [SerializeField] private ResidentReactionLogic _reactions = new();

        public Transform Head { get; private set; }

        public event Action<CharacterReactions, string> Bothered;

        private bool CanReact => _rescuable == null || _rescuable.State is RescueState.Waiting or RescueState.Rescued;

        public void React(in ToolHit hit)
        {
            if (CanReact && _reactions.Hit(hit, out string lineId))
            {
                Bothered?.Invoke(this, lineId);
            }
        }

        private void Awake()
        {
            Head = _animator.GetBoneTransform(HumanBodyBones.Head);
            _reactions.Init(_animator, GetComponentsInChildren<SkinnedMeshRenderer>());
        }

        private void LateUpdate() => _reactions.Tick(Time.deltaTime, CanReact);
    }
}
