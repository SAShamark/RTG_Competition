using Gameplay.Dialogue;
using Services.Audio;
using Services;
using UnityEngine;

namespace UI.Popups.Variants.Puzzles
{
    public sealed class LockpickPopup : PuzzlePopup
    {
        private const float FullTurn = 90f;
        private const float PickRange = 85f;
        private const float TurnEpsilon = 0.5f;

        [Header("Feel")]
        [SerializeField] private float _sensitivity = 0.25f;
        [SerializeField] private float _tolerance = 9f;
        [SerializeField] private float _feelRange = 55f;
        [SerializeField] private float _turnSpeed = 140f;
        [SerializeField] private float _returnSpeedFactor = 1.5f;
        [SerializeField] private float _sweetSpotRange = 70f;

        [Header("Stuck Shake")]
        [SerializeField] private float _stuckShakeFrequency = 70f;
        [SerializeField] private float _stuckShakeAngle = 2.5f;

        [Header("Layout")]
        [SerializeField] private RectTransform _cylinder;
        [SerializeField] private RectTransform _pick;

        private float _pickAngle;
        private float _sweetSpot;
        private float _turn;

        protected override bool ShowsCursor => false;

        protected override void OnOpened()
        {
            _sweetSpot = Random.Range(-_sweetSpotRange, _sweetSpotRange);
            _pickAngle = 0f;
            _turn = 0f;
        }

        protected override void Tick(float deltaTime)
        {
            bool isTurning = GameInput.IsPrimaryHeld;
            if (!isTurning)
            {
                _pickAngle = Mathf.Clamp(_pickAngle + GameInput.Look.x * _sensitivity, -PickRange, PickRange);
            }

            float allowed = AllowedTurn();
            float shake = 0f;
            if (isTurning)
            {
                _turn = Mathf.MoveTowards(_turn, allowed, _turnSpeed * deltaTime);
                if (_turn >= allowed - TurnEpsilon && allowed < FullTurn)
                {
                    shake = Mathf.Sin(Time.unscaledTime * _stuckShakeFrequency) * _stuckShakeAngle;
                }
            }
            else
            {
                _turn = Mathf.MoveTowards(_turn, 0f, _turnSpeed * _returnSpeedFactor * deltaTime);
            }

            _cylinder.localRotation = Quaternion.Euler(0f, 0f, -_turn);
            _pick.localRotation = Quaternion.Euler(0f, 0f, -_pickAngle + shake);

            if (_turn >= FullTurn - TurnEpsilon)
            {
                Solve(TextId.StatusLockOpened, SoundType.LockpickOpen);
            }
        }

        // full turn inside tolerance, then linear falloff to zero over feelRange
        private float AllowedTurn()
        {
            float miss = Mathf.Abs(_pickAngle - _sweetSpot);
            return miss <= _tolerance ? FullTurn : FullTurn * Mathf.Clamp01(1f - (miss - _tolerance) / _feelRange);
        }
    }
}
