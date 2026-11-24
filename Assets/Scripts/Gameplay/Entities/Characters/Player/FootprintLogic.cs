using System;
using Gameplay.Entities.Interactive.Items;
using UnityEngine;

namespace Gameplay.Entities.Characters.Player
{
    [Serializable]
    public sealed class FootprintLogic
    {
        [SerializeField] private Animator _body;
        [SerializeField] private float _halfWidth = 0.07f;
        [SerializeField] private float _halfLength = 0.14f;
        [SerializeField] private float _toeOffset = 0.05f;
        [SerializeField, Range(0f, 1f)] private float _keep = 0.2f;
        [SerializeField, Range(0f, 1f)] private float _landingKeep = 0.1f;
        [SerializeField] private float _plantedLift = 0.03f;

        private Transform _owner;
        private Transform _leftFoot;
        private Transform _rightFoot;

        public void Init(Transform owner)
        {
            _owner = owner;
            _leftFoot = _body.GetBoneTransform(HumanBodyBones.LeftFoot);
            _rightFoot = _body.GetBoneTransform(HumanBodyBones.RightFoot);
        }

        // A foot counts as planted while it is level with the lower one, so the print follows the sole instead of the step sound.
        public void Tick(bool isGrounded)
        {
            if (!isGrounded)
            {
                return;
            }

            float ground = Mathf.Min(_leftFoot.position.y, _rightFoot.position.y) + _plantedLift;
            if (_leftFoot.position.y <= ground)
            {
                Press(_leftFoot, _keep);
            }

            if (_rightFoot.position.y <= ground)
            {
                Press(_rightFoot, _keep);
            }
        }

        public void Land()
        {
            Press(_leftFoot, _landingKeep);
            Press(_rightFoot, _landingKeep);
        }

        private void Press(Transform foot, float keep)
        {
            Vector3 forward = _body.transform.forward;
            Vector3 point = foot.position + forward * _toeOffset;
            var chunks = PowderSnowChunk.Active;
            for (int i = 0; i < chunks.Count; i++)
            {
                chunks[i].Press(point, forward, _halfWidth, _halfLength, keep);
            }
        }
    }
}
