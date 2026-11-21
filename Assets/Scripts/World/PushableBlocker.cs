using System;
using Game.Interaction;
using Game.Items;
using Game.Player;
using UnityEngine;

namespace Game.World
{
    public class PushableBlocker : MonoBehaviour, IInteractable
    {
        [SerializeField] private string _displayName = "комод";
        [SerializeField] private Vector3 _pushOffset = new(-0.9f, 0f, -0.6f);
        [SerializeField] private float _pushDuration = 1.4f;
        [SerializeField] private ItemType _smashItem = ItemType.Crowbar;
        [SerializeField] private GameObject _smashEffect;
        [SerializeField] private AudioSource _audio;
        [SerializeField] private AudioClip _pushClip;
        [SerializeField] private AudioClip _smashClip;

        private Vector3 _from;
        private Vector3 _to;
        private float _progress = -1f;
        private bool _isCleared;
        private bool _smashNow;

        public bool IsCleared => _isCleared;
        public bool CanInteract => enabled && !_isCleared && _progress < 0f;
        public HandGesture Gesture => _smashNow ? HandGesture.Pry : HandGesture.Push;

        public event Action<PushableBlocker, bool> OnCleared;

        public string GetHint(Transform interactor)
        {
            _smashNow = HolderHas(interactor, _smashItem);
            return _smashNow
                ? $"[E] Розтрощити {_displayName}"
                : $"[E] Відсунути {_displayName}";
        }

        public void Interact(Transform interactor)
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
            _progress = 0f;
            Play(_pushClip);
        }

        private void Update()
        {
            if (_progress < 0f || _isCleared)
            {
                return;
            }

            _progress = Mathf.Min(1f, _progress + Time.deltaTime / _pushDuration);
            // heavy shove: jerky steps instead of a smooth slide
            float stepped = Mathf.SmoothStep(0f, 1f, _progress) + Mathf.Sin(_progress * Mathf.PI * 6f) * 0.015f;
            transform.position = Vector3.Lerp(_from, _to, stepped);
            if (_progress >= 1f)
            {
                transform.position = _to;
                Finish(false);
            }
        }

        private void Smash()
        {
            Play(_smashClip);
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
            _isCleared = true;
            _progress = -1f;
            OnCleared?.Invoke(this, smashed);
        }

        private void Play(AudioClip clip)
        {
            if (clip != null)
            {
                _audio.PlayOneShot(clip);
            }
        }

        private static bool HolderHas(Transform interactor, ItemType item) =>
            item != ItemType.None && interactor.TryGetComponent(out IItemHolder holder) && holder.HeldItem == item;
    }
}
