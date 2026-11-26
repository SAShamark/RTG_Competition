using System;
using Gameplay.Entities.Characters.Residents;
using Gameplay.Entities.Interactive;
using UnityEngine;

namespace Gameplay.Entities.Characters
{
    public sealed class CharacterControl : MonoBehaviour, IToolReactive, IDialogueActor
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private Rescuable _rescuable;
        [SerializeField] private JawControl _jaw;
        [SerializeField] private float _bodyCenterHeight = 0.85f;
        [SerializeField] private CharacterPoseLogic _pose = new();
        [SerializeField] private CharacterReactionLogic _reactions = new();
        [SerializeField] private HeadLookLogic _look = new();

        public Animator Animator => _animator;
        public Transform Head { get; private set; }
        public Vector3 Pelvis => _pose.Pelvis;
        public float BodyCenterHeight => _bodyCenterHeight;
        public bool IsCarried => _rescuable != null && _rescuable.State == RescueState.Carried;

        public event Action<CharacterControl, string> Bothered;

        private bool CanReact => _rescuable == null || _rescuable.State is RescueState.Waiting or RescueState.Rescued;
        private bool CanTurn => _rescuable == null || _rescuable.State == RescueState.Waiting;

        public void SetTalking(bool isTalking) => _jaw.SetTalking(isTalking);

        public void SetPose(CharacterPose pose) => _pose.SetMode(pose);

        public void SetCenterPose(Vector3 center, Quaternion rotation)
        {
            transform.SetPositionAndRotation(center - rotation * Vector3.up * _bodyCenterHeight, rotation);
        }

        public void Face(Vector3 point, float deltaTime)
        {
            if (CanTurn)
            {
                _look.Face(point, deltaTime);
            }
        }

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
            _pose.Init(_animator);
            _reactions.Init(_animator, GetComponentsInChildren<SkinnedMeshRenderer>());
            _look.Init(transform, Head);
        }

        private void LateUpdate()
        {
            float deltaTime = Time.deltaTime;
            _pose.Tick(deltaTime);
            _reactions.Tick(deltaTime, CanReact);
            _look.Tick(deltaTime);
        }
    }
}
