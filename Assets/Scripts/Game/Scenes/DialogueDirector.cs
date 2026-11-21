using System;
using System.Collections.Generic;
using UI.Screens.Variants.Gameplay;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Game.Scenes
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
    public struct VoiceLine
    {
        public string Id;
        public string Text;
        public AudioClip Clip;
    }

    public class DialogueDirector : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera _camera;
        [SerializeField] private float _shoulderBack = 0.75f;
        [SerializeField] private float _shoulderSide = 0.38f;
        [SerializeField] private float _shoulderUp = 0.08f;
        [SerializeField] private float _charsPerSecond = 45f;
        [SerializeField] private float _holdAfterTyping = 1.6f;
        [SerializeField] private float _moveSharpness = 6f;

        private readonly Queue<DialogueLine> _lines = new();
        private GameplayScreen _screen;
        private FirstPersonController _player;
        private Renderer[] _playerBody;
        private ShadowCastingMode[] _bodyModes;
        private Action _onFinished;
        private DialogueLine _current;
        private float _timeLeft;
        private bool _isPlaying;
        private bool _snapCamera;

        public bool IsPlaying => _isPlaying;

        [SerializeField] private VoiceLine[] _voiceLines;
        [SerializeField] private AudioSource _voice;

        public void PlayVoice(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            foreach (VoiceLine line in _voiceLines)
            {
                if (line.Text == text)
                {
                    _voice.Stop();
                    _voice.clip = line.Clip;
                    _voice.Play();
                    return;
                }
            }
        }

        public static DialogueDirector Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            _camera.gameObject.SetActive(false);
        }

        public void Init(GameplayScreen screen, FirstPersonController player, Transform playerBody)
        {
            _screen = screen;
            _player = player;
            _playerBody = playerBody.GetComponentsInChildren<Renderer>(true);
            _bodyModes = new ShadowCastingMode[_playerBody.Length];
        }

        public void Play(IEnumerable<DialogueLine> lines, Action onFinished = null)
        {
            foreach (DialogueLine line in lines)
            {
                _lines.Enqueue(line);
            }

            _onFinished += onFinished;
            if (_isPlaying)
            {
                return;
            }

            _isPlaying = true;
            _snapCamera = true;
            _player.enabled = false;

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
            _timeLeft = 0.25f;
            _screen.ClearDialog();
            _screen.CharacterSay(_current.Speaker, _current.Text, 9999f, _current.SpeakerHead);
            _snapCamera = true;
        }

        private void Update()
        {
            if (!_isPlaying)
            {
                return;
            }

            _timeLeft -= Time.deltaTime;
            bool next = Keyboard.current.spaceKey.wasPressedThisFrame || Mouse.current.leftButton.wasPressedThisFrame;
            if (next && _timeLeft <= 0f)
            {
                NextLine();
            }
        }

        private void LateUpdate()
        {
            if (!_isPlaying)
            {
                return;
            }

            Vector3 speaker = _current.SpeakerHead.position;
            Vector3 listener = _current.ListenerHead.position;
            Vector3 dir = Vector3.ProjectOnPlane(speaker - listener, Vector3.up);
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, dir);

            Transform cam = _camera.transform;
            if (_snapCamera)
            {
                _snapCamera = false;
                ChooseShot(speaker, listener, dir, right, out Vector3 position, out Quaternion rotation);
                cam.SetPositionAndRotation(position, rotation);
                return;
            }

            Quaternion aim = Quaternion.LookRotation(speaker + Vector3.down * 0.08f - cam.position, Vector3.up);
            cam.rotation = Quaternion.Slerp(cam.rotation, aim, 1f - Mathf.Exp(-_moveSharpness * Time.deltaTime));
        }

        private void ChooseShot(Vector3 speaker, Vector3 listener, Vector3 dir, Vector3 right, out Vector3 position, out Quaternion rotation)
        {
            float[] backs = { _shoulderBack, _shoulderBack * 0.75f, _shoulderBack * 1.3f };
            float[] sides = { _shoulderSide, -_shoulderSide, _shoulderSide * 0.4f, -_shoulderSide * 0.4f };
            Vector3 target = speaker + Vector3.down * 0.08f;
            foreach (float back in backs)
            {
                foreach (float side in sides)
                {
                    Vector3 candidate = listener - dir * back + right * side + Vector3.up * _shoulderUp;
                    if (IsClear(candidate, target, listener))
                    {
                        position = candidate;
                        rotation = Quaternion.LookRotation(target - candidate, Vector3.up);
                        return;
                    }
                }
            }

            float[] dists = { 1.2f, 0.9f, 1.6f };
            float[] faceSides = { 0.35f, -0.35f, 0f };
            foreach (float dist in dists)
            {
                foreach (float side in faceSides)
                {
                    Vector3 candidate = speaker - dir * dist + right * side + Vector3.up * 0.05f;
                    if (IsClear(candidate, target, target))
                    {
                        position = candidate;
                        rotation = Quaternion.LookRotation(target - candidate, Vector3.up);
                        return;
                    }
                }
            }

            position = listener + dir * 0.45f + Vector3.up * 0.05f;
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
                    b.Expand(0.25f);
                    if (b.Contains(point))
                    {
                        return true;
                    }
                }
            }

            foreach (Renderer r in _playerBody)
            {
                Bounds b = r.bounds;
                b.Expand(0.25f);
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

            if (Physics.CheckSphere(cameraPosition, 0.15f, ~0, QueryTriggerInteraction.Ignore) && !OnlyCharacters(Physics.OverlapSphere(cameraPosition, 0.15f, ~0, QueryTriggerInteraction.Ignore)))
            {
                return false;
            }

            foreach (Vector3 to in new[] { target, listener })
            {
                Vector3 d = to - cameraPosition;
                foreach (RaycastHit hit in Physics.RaycastAll(cameraPosition, d.normalized, d.magnitude - 0.25f, ~0, QueryTriggerInteraction.Ignore))
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
            c is CharacterController || c.GetComponentInParent<Animator>() != null || c.GetComponentInParent<Game.Rescue.Rescuable>() != null;

        private void Finish()
        {
            _isPlaying = false;
            _screen.ClearDialog();
            _screen.SetCrosshairVisible(true);
            SetBodyVisible(false);
            _camera.gameObject.SetActive(false);

            _player.enabled = true;

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
