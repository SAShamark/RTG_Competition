using Services;
using Services.Audio;
using System;
using System.Collections.Generic;
using Gameplay.Entities.Characters;
using Gameplay.Entities.Characters.Player;
using Gameplay.Entities.Characters.Residents;
using UI.Screens.Variants.Gameplay;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Gameplay.Dialogue
{
    public struct DialogueLine
    {
        public string Speaker;
        public Transform SpeakerHead;
        public Transform ListenerHead;
        public string Text;

        public DialogueLine(string speaker, Transform speakerHead, Transform listenerHead, string text)
        {
            Speaker = speaker;
            SpeakerHead = speakerHead;
            ListenerHead = listenerHead;
            Text = text;
        }
    }

    [Serializable]
    public sealed class DialogueDirector
    {
        private const float UntilCleared = 9999f;
        private const float MinDirectionSqr = 0.001f;

        [SerializeField] private CinemachineCamera _camera;
        [SerializeField] private float _shoulderBack = 0.75f;
        [SerializeField] private float _shoulderSide = 0.38f;
        [SerializeField] private float _shoulderUp = 0.08f;
        [SerializeField, Range(0f, 1f)] private float _shoulderLevelling = 0.5f;
        [SerializeField] private float _pushInSpeed = 0.04f;
        [SerializeField] private float _charsPerSecond = 45f;
        [SerializeField] private float _holdAfterTyping = 1.6f;
        [SerializeField] private float _moveSharpness = 6f;

        [SerializeField] private DialogLibrary _dialogLibrary;
        [SerializeField] private float _lineGap = 0.25f;
        [SerializeField] private float _headAimDrop = 0.08f;
        [SerializeField] private float _nearShoulderFactor = 0.75f;
        [SerializeField] private float _farShoulderFactor = 1.3f;
        [SerializeField] private float _narrowSideFactor = 0.4f;
        [SerializeField] private float[] _faceDistances = { 1.2f, 0.9f, 1.6f };
        [SerializeField] private float[] _faceSides = { 0.35f, -0.35f, 0f };
        [SerializeField] private float _cameraLift = 0.05f;
        [SerializeField] private float _overListenerDistance = 0.45f;
        [SerializeField] private float _boundsPadding = 0.25f;
        [SerializeField] private float _cameraRadius = 0.15f;
        [SerializeField] private float _rayShortening = 0.25f;
        [SerializeField] private float[] _orbitDistances = { 1.1f, 1.5f, 0.8f };
        [SerializeField] private int _orbitSteps = 12;
        [SerializeField] private float _minTalkDistance = 1.25f;
        [SerializeField] private float _talkDistance = 1.5f;
        [SerializeField] private int _standSteps = 8;
        [SerializeField] private float _groundProbe = 0.5f;

        private readonly Queue<DialogueLine> _lines = new();
        private AudioService _audio;
        private GameplayScreen _screen;
        private FirstPersonController _player;
        private CharacterController _playerCollider;
        private Renderer[] _playerBody;
        private ShadowCastingMode[] _bodyModes;
        private Action _onFinished;
        private DialogueLine _current;
        private Transform _companionHead;
        private Rescuable _companion;
        private float _timeLeft;
        private float _voiceLeft;
        private float _talkLeft;
        private JawControl _speakerJaw;
        private bool _snapCamera;
        private bool _isWaiting;
        private bool _isWalking;

        public bool IsPlaying { get; private set; }
        public DialogLibrary Library => _dialogLibrary;

        public DialogueLine Line(string id, Transform speaker, Transform listener)
        {
            DialogPhrase phrase = _dialogLibrary.Get(id);
            return new DialogueLine(phrase.Speaker, speaker, listener, phrase.Text);
        }

        public void Init(GameplayScreen screen, FirstPersonController player, Transform playerBody, AudioService audio)
        {
            _audio = audio;
            _screen = screen;
            _player = player;
            _playerCollider = player.GetComponent<CharacterController>();
            _playerBody = playerBody.GetComponentsInChildren<Renderer>(true);
            _bodyModes = new ShadowCastingMode[_playerBody.Length];
            _camera.gameObject.SetActive(false);
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
            _snapCamera = true;
            _player.BeginConversation();
            _player.enabled = false;
            _audio.SetDucked(true);

            SetBodyVisible(true);
            _screen.SetCrosshairVisible(false);
            _camera.gameObject.SetActive(true);

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
            _companionHead = _current.SpeakerHead == _player.Head ? _current.ListenerHead : _current.SpeakerHead;
            _companion = _companionHead.GetComponentInParent<Rescuable>();
            KeepTalkDistance();
            _timeLeft = _lineGap;
            bool hasVoice = _dialogLibrary.TryGetClip(_current.Text, out AudioClip clip);
            _voiceLeft = hasVoice ? clip.length : float.MaxValue;
            _talkLeft = hasVoice ? clip.length : _current.Text.Length / _charsPerSecond;
            SetSpeakerJaw(_current.SpeakerHead.GetComponentInParent<JawControl>());
            _screen.ClearDialog();
            _screen.CharacterSay(_current.Speaker, _current.Text, UntilCleared, _current.SpeakerHead);
            _snapCamera = true;
            ReleaseIfCarried();
        }

        private bool IsCompanionCarried => _companion != null && _companion.State == RescueState.Carried;

        private void ReleaseIfCarried()
        {
            if (_isWalking || !IsCompanionCarried)
            {
                return;
            }

            _isWalking = true;
            ReleasePlayer();
        }

        private void ReleasePlayer()
        {
            _audio.SetDucked(false);
            _screen.SetCrosshairVisible(true);
            SetBodyVisible(false);
            _camera.gameObject.SetActive(false);
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
                SetSpeakerJaw(null);
            }

            if (_isWalking)
            {
                if (_voiceLeft <= 0f || _talkLeft <= -_holdAfterTyping)
                {
                    NextLine();
                }

                return;
            }

            _player.FaceConversation(_companionHead.position, deltaTime);
            if (_companion != null)
            {
                _companion.Face(_player.Head.position, deltaTime);
            }

            bool next = Keyboard.current.spaceKey.wasPressedThisFrame || Mouse.current.leftButton.wasPressedThisFrame;
            if ((next && _timeLeft <= 0f) || _voiceLeft <= 0f)
            {
                NextLine();
            }
        }

        public void LateTick()
        {
            if (!IsPlaying || _isWalking)
            {
                return;
            }

            Vector3 speaker = _current.SpeakerHead.position;
            Vector3 listener = _current.ListenerHead.position;
            Vector3 dir = Vector3.ProjectOnPlane(speaker - listener, Vector3.up);
            dir = dir.sqrMagnitude > MinDirectionSqr ? dir.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, dir);

            Transform cam = _camera.transform;
            if (_snapCamera)
            {
                _snapCamera = false;
                ChooseShot(speaker, listener, dir, right, out Vector3 position, out Quaternion rotation);
                cam.SetPositionAndRotation(position, rotation);
                return;
            }

            Quaternion aim = Quaternion.LookRotation(speaker + Vector3.down * _headAimDrop - cam.position, Vector3.up);
            cam.rotation = Quaternion.Slerp(cam.rotation, aim, Easing.Damp(_moveSharpness, Time.deltaTime));
            cam.position += cam.forward * (_pushInSpeed * Time.deltaTime);
        }

        private void KeepTalkDistance()
        {
            if (_player.Carry.IsCarrying)
            {
                return;
            }

            Vector3 feet = _player.transform.position;
            Vector3 companion = _companionHead.position;
            companion.y = feet.y;
            Vector3 away = feet - companion;
            if (away.sqrMagnitude >= _minTalkDistance * _minTalkDistance)
            {
                return;
            }

            away = away.sqrMagnitude > MinDirectionSqr ? away.normalized : Vector3.ProjectOnPlane(_companionHead.forward, Vector3.up).normalized;
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

            Vector3 from = _companionHead.position;
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

        private void ChooseShot(Vector3 speaker, Vector3 listener, Vector3 dir, Vector3 right, out Vector3 position, out Quaternion rotation)
        {
            float[] backs = { _shoulderBack, _shoulderBack * _nearShoulderFactor, _shoulderBack * _farShoulderFactor };
            float[] sides = { _shoulderSide, -_shoulderSide, _shoulderSide * _narrowSideFactor, -_shoulderSide * _narrowSideFactor };
            Vector3 target = speaker + Vector3.down * _headAimDrop;
            foreach (float back in backs)
            {
                foreach (float side in sides)
                {
                    Vector3 candidate = listener - dir * back + right * side + Vector3.up * _shoulderUp;
                    candidate.y += (speaker.y - listener.y) * _shoulderLevelling;
                    if (IsClear(candidate, target, listener))
                    {
                        position = candidate;
                        rotation = Quaternion.LookRotation(target - candidate, Vector3.up);
                        return;
                    }
                }
            }

            float[] dists = _faceDistances;
            float[] faceSides = _faceSides;
            foreach (float dist in dists)
            {
                foreach (float side in faceSides)
                {
                    Vector3 candidate = speaker - dir * dist + right * side + Vector3.up * _cameraLift;
                    if (IsClear(candidate, target, target))
                    {
                        position = candidate;
                        rotation = Quaternion.LookRotation(target - candidate, Vector3.up);
                        return;
                    }
                }
            }

            // The speaker can be around a corner from the listener: orbit him until a shot actually sees his face.
            float step = 360f / _orbitSteps;
            foreach (float dist in _orbitDistances)
            {
                for (int i = 0; i < _orbitSteps; i++)
                {
                    Vector3 around = Quaternion.AngleAxis(step * i, Vector3.up) * -dir;
                    Vector3 candidate = speaker + around * dist + Vector3.up * _cameraLift;
                    if (IsClear(candidate, target, target))
                    {
                        position = candidate;
                        rotation = Quaternion.LookRotation(target - candidate, Vector3.up);
                        return;
                    }
                }
            }

            position = listener + dir * _overListenerDistance + Vector3.up * _cameraLift;
            rotation = Quaternion.LookRotation(target - position, Vector3.up);
        }

        private bool InsideCharacter(Vector3 point)
        {
            foreach (Transform head in new[] { _current.SpeakerHead, _current.ListenerHead })
            {
                Animator body = head.GetComponentInParent<Animator>();
                Transform root = body != null ? body.transform : head.root;
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
                {
                    Bounds b = r.bounds;
                    b.Expand(_boundsPadding);
                    if (b.Contains(point))
                    {
                        return true;
                    }
                }
            }

            foreach (Renderer r in _playerBody)
            {
                Bounds b = r.bounds;
                b.Expand(_boundsPadding);
                if (b.Contains(point))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsClear(Vector3 cameraPosition, Vector3 target, Vector3 listener)
        {
            if (InsideCharacter(cameraPosition))
            {
                return false;
            }

            if (Physics.CheckSphere(cameraPosition, _cameraRadius, ~0, QueryTriggerInteraction.Ignore) && !OnlyCharacters(Physics.OverlapSphere(cameraPosition, _cameraRadius, ~0, QueryTriggerInteraction.Ignore)))
            {
                return false;
            }

            foreach (Vector3 to in new[] { target, listener })
            {
                Vector3 d = to - cameraPosition;
                foreach (RaycastHit hit in Physics.RaycastAll(cameraPosition, d.normalized, d.magnitude - _rayShortening, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!IsCharacter(hit.collider))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool OnlyCharacters(Collider[] colliders)
        {
            foreach (Collider c in colliders)
            {
                if (!IsCharacter(c))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsCharacter(Collider c) =>
            c is CharacterController || c.GetComponentInParent<Animator>() != null || c.GetComponentInParent<Rescuable>() != null;

        private void SetSpeakerJaw(JawControl jaw)
        {
            if (_speakerJaw != null)
            {
                _speakerJaw.SetTalking(false);
            }

            _speakerJaw = jaw;
            if (_speakerJaw != null)
            {
                _speakerJaw.SetTalking(true);
            }
        }

        private void Finish()
        {
            IsPlaying = false;
            SetSpeakerJaw(null);
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

        private void SetBodyVisible(bool isVisible)
        {
            for (int i = 0; i < _playerBody.Length; i++)
            {
                if (isVisible)
                {
                    _bodyModes[i] = _playerBody[i].shadowCastingMode;
                    _playerBody[i].shadowCastingMode = ShadowCastingMode.On;
                }
                else
                {
                    _playerBody[i].shadowCastingMode = _bodyModes[i];
                }
            }
        }
    }
}
