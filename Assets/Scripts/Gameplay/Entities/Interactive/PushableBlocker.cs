using Gameplay.Dialogue;
using Gameplay.Entities.Interactive.Items;
using System;
using Gameplay.Entities.Characters.Player;
using Services.Audio;
using Services.Motion;
using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public sealed class PushableBlocker : Interactable
    {
        [SerializeField] private string _displayName = "комод";
        [SerializeField] private Vector3 _pushOffset = new(-0.9f, 0f, -0.6f);
        [SerializeField] private float _pushDuration = 1.4f;
        [SerializeField] private ItemType _smashItem = ItemType.Axe;
        [SerializeField] private GameObject _smashEffect;
        [SerializeField] private float _wobbleCount = 6f;
        [SerializeField] private float _wobbleAmount = 0.015f;

        private Vector3 _from;
        private Vector3 _to;
        private MotionHandle _push = MotionHandle.Idle;
        private bool _smashNow;

        public bool IsCleared { get; private set; }
        public override bool CanInteract => enabled && !IsCleared && !_push.IsPlaying;
        public event Action<PushableBlocker, bool> Cleared;

        public override string GetHint(Transform interactor)
        {
            _smashNow = HolderHas(interactor, _smashItem);
            return _smashNow
                ? _texts.Format(TextId.HintSmash, _displayName)
                : _texts.Format(TextId.HintPush, _displayName);
        }

        protected override void OnInteract(Transform interactor)
        {
            if (!CanInteract)
            {
                return;
            }

            if (HolderHas(interactor, _smashItem))
            {
                Smash();
                return;
            }

            _from = transform.position;
            _to = _from + _pushOffset;
            _push = MotionService.Instance.Play(_pushDuration, Shove, () => Finish(false));
            Play(SoundType.BlockerPush);
        }

        private void Shove(float progress)
        {
            // heavy shove: jerky steps instead of a smooth slide
            float stepped = Mathf.SmoothStep(0f, 1f, progress) + Mathf.Sin(progress * Mathf.PI * _wobbleCount) * _wobbleAmount;
            transform.position = progress >= 1f ? _to : Vector3.Lerp(_from, _to, stepped);
        }

        private void Smash()
        {
            Play(SoundType.BlockerSmash);
            if (_smashEffect != null)
            {
                _smashEffect.transform.SetParent(null, true);
                _smashEffect.SetActive(true);
            }

            gameObject.SetActive(false);
            Finish(true);
        }

        private void Finish(bool smashed)
        {
            IsCleared = true;
            Cleared?.Invoke(this, smashed);
        }

        private void Play(SoundType sound) => AudioService.Instance.PlayAt(sound, transform.position);

        private static bool HolderHas(Transform interactor, ItemType item) =>
            item != ItemType.None && interactor.TryGetComponent(out IItemHolder holder) && holder.HeldItem == item;
    }
}
