using Services;
using Services.Audio;
using System;
using System.Collections.Generic;
using Gameplay.Entities.Characters;
using Gameplay.Entities.Characters.Player;
using UI.Screens.Variants.Gameplay;
using UnityEngine;

namespace Gameplay.Dialogue
{
    public struct DialogueLine
    {
        public DialogPhrase Phrase;
        public IDialogueActor Speaker;
        public IDialogueActor Listener;

        public DialogueLine(DialogPhrase phrase, IDialogueActor speaker, IDialogueActor listener)
        {
            Phrase = phrase;
            Speaker = speaker;
            Listener = listener;
        }
    }

    [Serializable]
    public sealed class DialogueDirector
    {
        private const float UntilCleared = 9999f;
        private const float MinDirectionSqr = 0.001f;

        [SerializeField] private float _charsPerSecond = 45f;
        [SerializeField] private float _holdAfterTyping = 1.6f;

        [SerializeField] private DialogLibrary _dialogLibrary;
        [SerializeField] private float _lineGap = 0.25f;
        [SerializeField] private float _minTalkDistance = 1.25f;
        [SerializeField] private float _talkDistance = 1.5f;
        [SerializeField] private int _standSteps = 8;
        [SerializeField] private float _groundProbe = 0.5f;

        private readonly Queue<DialogueLine> _lines = new();
        private GameplayScreen _screen;
        private FirstPersonController _player;
        private CharacterController _playerCollider;
        private Action _onFinished;
        private DialogueLine _current;
        private CharacterControl _companion;
        private float _timeLeft;
        private float _voiceLeft;
        private float _talkLeft;
        private IDialogueActor _talker;
        private bool _isWaiting;
        private bool _isWalking;

        public bool IsPlaying { get; private set; }
        public DialogLibrary Library => _dialogLibrary;

        public DialogueLine Line(string id, IDialogueActor speaker, IDialogueActor listener)
        {
            return new DialogueLine(_dialogLibrary.Get(id), speaker, listener);
        }

        public void Init(GameplayScreen screen, FirstPersonController player)
        {
            _screen = screen;
            _player = player;
            _playerCollider = player.GetComponent<CharacterController>();
        }

        public void Play(IEnumerable<DialogueLine> lines, Action onFinished = null)
        {
            foreach (DialogueLine line in lines)
            {
                _lines.Enqueue(line);
            }

            _onFinished += onFinished;
            if (IsPlaying || _isWaiting)
            {
                return;
            }

            if (_screen.IsDialogBusy)
            {
                _isWaiting = true;
                return;
            }

            Begin();
        }

        private void Begin()
        {
            _isWaiting = false;
            IsPlaying = true;
            _player.BeginConversation();
            _player.enabled = false;
            AudioService.Instance.SetDucked(true);
            NextLine();
        }

        private void NextLine()
        {
            if (_lines.Count == 0)
            {
                Finish();
                return;
            }

            _current = _lines.Dequeue();
            _companion = (CharacterControl)(ReferenceEquals(_current.Speaker, _player) ? _current.Listener : _current.Speaker);
            KeepTalkDistance();
            _timeLeft = _lineGap;
            DialogPhrase phrase = _current.Phrase;
            bool hasVoice = phrase.Clip != null;
            _voiceLeft = hasVoice ? phrase.Clip.length : float.MaxValue;
            _talkLeft = hasVoice ? phrase.Clip.length : phrase.Text.Length / _charsPerSecond;
            SetTalker(_current.Speaker);
            _screen.ClearDialog();
            _screen.Say(phrase.WithHold(UntilCleared));
            ReleaseIfCarried();
        }

        private void ReleaseIfCarried()
        {
            if (_isWalking || !_companion.IsCarried)
            {
                return;
            }

            _isWalking = true;
            ReleasePlayer();
        }

        private void ReleasePlayer()
        {
            AudioService.Instance.SetDucked(false);
            _player.EndConversation();
            _player.enabled = true;
        }

        public void Tick(float deltaTime)
        {
            if (_isWaiting && !_screen.IsDialogBusy)
            {
                Begin();
                return;
            }

            if (!IsPlaying)
            {
                return;
            }

            ReleaseIfCarried();
            _timeLeft -= deltaTime;
            _voiceLeft -= deltaTime;
            _talkLeft -= deltaTime;
            if (_talkLeft <= 0f)
            {
                SetTalker(null);
            }

            if (_isWalking)
            {
                if (_voiceLeft <= 0f || _talkLeft <= -_holdAfterTyping)
                {
                    NextLine();
                }

                return;
            }

            _companion.Face(_player.Head.position, deltaTime);

            if ((GameInput.AdvancePressed && _timeLeft <= 0f) || _voiceLeft <= 0f)
            {
                NextLine();
            }
        }

        private void KeepTalkDistance()
        {
            if (_player.Carry.IsCarrying)
            {
                return;
            }

            Vector3 feet = _player.transform.position;
            Vector3 companion = _companion.Head.position;
            companion.y = feet.y;
            Vector3 away = feet - companion;
            if (away.sqrMagnitude >= _minTalkDistance * _minTalkDistance)
            {
                return;
            }

            away = away.sqrMagnitude > MinDirectionSqr ? away.normalized : Vector3.ProjectOnPlane(_companion.Head.forward, Vector3.up).normalized;
            float step = 360f / _standSteps;
            for (int i = 0; i < _standSteps; i++)
            {
                // 0, +1, -1, +2, -2... so the free spot closest to where the player already stands wins.
                int turn = (i + 1) / 2 * (i % 2 == 0 ? -1 : 1);
                Vector3 dir = Quaternion.AngleAxis(step * turn, Vector3.up) * away;
                Vector3 spot = companion + dir * _talkDistance;
                if (CanStand(spot))
                {
                    _player.Teleport(spot, Quaternion.LookRotation(-dir));
                    return;
                }
            }
        }

        private bool CanStand(Vector3 spot)
        {
            float radius = _playerCollider.radius;
            float lift = _playerCollider.stepOffset;
            Vector3 center = spot + _playerCollider.center;
            float half = Mathf.Max(_playerCollider.height / 2f - radius, 0f);
            Vector3 top = center + Vector3.up * half;
            Vector3 bottom = center + Vector3.down * half + Vector3.up * lift;
            foreach (Collider c in Physics.OverlapCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c != _playerCollider)
                {
                    return false;
                }
            }

            if (!Physics.Raycast(spot + Vector3.up * lift, Vector3.down, lift + _groundProbe, ~0, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            Vector3 from = _companion.Head.position;
            Vector3 to = top - from;
            foreach (RaycastHit hit in Physics.RaycastAll(from, to.normalized, to.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!IsCharacter(hit.collider))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsCharacter(Collider c) =>
            c is CharacterController || c.GetComponentInParent<Animator>() != null || c.GetComponentInParent<CharacterControl>() != null;

        private void SetTalker(IDialogueActor talker)
        {
            _talker?.SetTalking(false);
            _talker = talker;
            _talker?.SetTalking(true);
        }

        private void Finish()
        {
            IsPlaying = false;
            SetTalker(null);
            _screen.ClearDialog();
            if (!_isWalking)
            {
                ReleasePlayer();
            }

            _isWalking = false;

            Action done = _onFinished;
            _onFinished = null;
            done?.Invoke();
        }
    }
}
