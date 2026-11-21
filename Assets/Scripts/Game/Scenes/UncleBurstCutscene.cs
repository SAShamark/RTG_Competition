using System;
using Game.World;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;

namespace Game.Scenes
{
    public class UncleBurstCutscene : MonoBehaviour, ICinematic
    {
        [SerializeField] private PlayableDirector _director;
        [SerializeField] private CinemachineCamera _lookCamera;
        [SerializeField] private GameObject _actors;
        [SerializeField] private Transform _uncle;
        [SerializeField] private Transform _lookTarget;
        [SerializeField] private Vector3 _doorCenter;
        [SerializeField] private float _duration = 5.6f;

        [SerializeField] private CinematicSettings _settings = new() { BarsInTime = 0.45f, BarsOutTime = 1.2f };
        [SerializeField] private SubtitleCue[] _subtitles =
        {
            new() { Time = 0.25f, Duration = 2.6f, Speaker = "Дядько", SpeakerColor = new Color(1f, 0.55f, 0.25f), Text = "Дорогу! Я тут усіх рятую!" },
        };

        [Header("Path")]
        [SerializeField] private float _passDistance = 1.1f;
        [SerializeField] private float[] _keyTimes = { 0f, 0.5f, 1.25f, 2.05f, 2.95f, 4.2f, 5.6f };
        [SerializeField, Range(0.3f, 1f)] private float _runSpeed = 0.78f;

        [Header("Camera")]
        [SerializeField] private float _lookSharpness = 3.5f;
        [SerializeField] private float _maxTurnSpeed = 110f;
        [SerializeField] private float _maxPitchDown = 25f;

        private FirstPersonController _player;
        private Vector3[] _path;
        private bool _isPlaying;

        public bool IsPlaying => _isPlaying;
        public PlayableDirector Director => _director;
        public CinematicSettings Settings => _settings;
        public SubtitleCue[] Subtitles => _subtitles;

        public event Action OnStarted;
        public event Action OnFinished;

        public void Skip()
        {
            if (_isPlaying)
            {
                _director.Stop();
            }
        }

        public void Init(FirstPersonController player)
        {
            _player = player;
            _actors.SetActive(false);
        }

        public void Play()
        {
            if (_isPlaying)
            {
                return;
            }

            _isPlaying = true;
            Vector3 playerPos = _player.transform.position;
            _path = BuildPath(playerPos);
            _uncle.position = _path[0];

            _player.enabled = false;
            _lookCamera.transform.SetPositionAndRotation(_player.Head.position, _player.Head.rotation);

            _director.stopped += HandleStopped;
            _director.time = 0;
            _director.Play();
            OnStarted?.Invoke();
        }

        [Header("Run-out")]
        [SerializeField] private float _runOutSpeed = 3.2f;
        [SerializeField] private float _runOutMaxTime = 12f;

        private bool _isRunningOut;
        private float _runOutTime;
        private float _unseenTime;
        private Vector3 _runOutDirection;
        private Renderer[] _actorRenderers;

        private void Update()
        {
            if (_isRunningOut)
            {
                TickRunOut();
                return;
            }

            if (!_isPlaying)
            {
                return;
            }

            float t = (float)_director.time;
            _uncle.position = SamplePath(t * _runSpeed);
            if (t >= _duration - 0.02f)
            {
                _director.Stop();
            }
        }

        private void LateUpdate()
        {
            if (!_isPlaying)
            {
                return;
            }

            Transform cam = _lookCamera.transform;
            Vector3 toTarget = _lookTarget.position - cam.position;
            if (toTarget.sqrMagnitude < 0.01f)
            {
                return;
            }

            Quaternion desired = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            Vector3 e = desired.eulerAngles;
            float pitch = e.x > 180f ? e.x - 360f : e.x;
            desired = Quaternion.Euler(Mathf.Clamp(pitch, -40f, _maxPitchDown), e.y, 0f);
            Quaternion smoothed = Quaternion.Slerp(cam.rotation, desired, 1f - Mathf.Exp(-_lookSharpness * Time.deltaTime));
            cam.rotation = Quaternion.RotateTowards(cam.rotation, smoothed, _maxTurnSpeed * Time.deltaTime);

            Vector3 camEuler = cam.rotation.eulerAngles;
            _player.transform.rotation = Quaternion.Euler(0f, camEuler.y, 0f);
            _player.Head.localRotation = Quaternion.Euler(camEuler.x, 0f, 0f);
        }

        private void StartRunOut()
        {
            if (!_actors.activeSelf)
            {
                return;
            }

            float end = _keyTimes[_keyTimes.Length - 1];
            Vector3 a = SamplePath(end - 0.2f);
            Vector3 b = SamplePath(end);
            _runOutDirection = Vector3.ProjectOnPlane(b - a, Vector3.up).normalized;
            if (_runOutDirection.sqrMagnitude < 0.01f)
            {
                _runOutDirection = Vector3.right;
            }

            _actorRenderers = _actors.GetComponentsInChildren<Renderer>();
            _runOutTime = 0f;
            _unseenTime = 0f;
            _isRunningOut = true;
        }

        private void TickRunOut()
        {
            _runOutTime += Time.deltaTime;
            Vector3 next = _uncle.position + _runOutDirection * _runOutSpeed * Time.deltaTime;
            next.y = GroundY(next, _uncle.position.y + 0.4f);
            _uncle.position = next;

            bool seen = false;
            Camera cam = Camera.main;
            if (cam != null)
            {
                foreach (Renderer r in _actorRenderers)
                {
                    Vector3 v = cam.WorldToViewportPoint(r.bounds.center);
                    if (v.z > 0f && v.x > -0.1f && v.x < 1.1f && v.y > -0.1f && v.y < 1.1f)
                    {
                        seen = true;
                        break;
                    }
                }
            }

            _unseenTime = seen ? 0f : _unseenTime + Time.deltaTime;
            if (_unseenTime > 0.4f || _runOutTime > _runOutMaxTime)
            {
                _isRunningOut = false;
                _actors.SetActive(false);
            }
        }

        private Vector3[] BuildPath(Vector3 playerPos)
        {
            float side = playerPos.x >= _doorCenter.x ? -1f : 1f;
            float lane = _doorCenter.x + side * 0.6f;
            float beside = playerPos.x + side * _passDistance;
            float besideZ = Mathf.Clamp(playerPos.z, _doorCenter.z - 4.4f, _doorCenter.z - 0.6f);
            float exitSide = side < 0f ? -1f : 1f;

            var points = new[]
            {
                new Vector3(lane, _doorCenter.y, _doorCenter.z + 2.2f),
                new Vector3(lane, _doorCenter.y, _doorCenter.z - 0.1f),
                new Vector3(Mathf.Lerp(lane, beside, 0.85f), 0f, besideZ),
                new Vector3(_doorCenter.x + side * 0.5f, 0f, _doorCenter.z - 4.9f),
                new Vector3(_doorCenter.x + exitSide * 2.6f, 0f, _doorCenter.z - 6.8f),
                new Vector3(_doorCenter.x + exitSide * 8f, 0f, _doorCenter.z - 7.4f),
                new Vector3(_doorCenter.x + exitSide * 15f, 0f, _doorCenter.z - 7.6f),
            };

            for (int i = 2; i < points.Length; i++)
            {
                points[i].y = GroundY(points[i], _doorCenter.y + 0.4f);
            }

            return points;
        }

        private Vector3 SamplePath(float t)
        {
            int last = _path.Length - 1;
            if (t <= _keyTimes[0]) return _path[0];
            if (t >= _keyTimes[last]) return _path[last];

            int i = 0;
            while (i < last - 1 && t > _keyTimes[i + 1]) i++;
            float u = Mathf.InverseLerp(_keyTimes[i], _keyTimes[i + 1], t);
            Vector3 p0 = _path[Mathf.Max(0, i - 1)];
            Vector3 p1 = _path[i];
            Vector3 p2 = _path[i + 1];
            Vector3 p3 = _path[Mathf.Min(last, i + 2)];
            float u2 = u * u, u3 = u2 * u;
            return 0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
        }

        private static float GroundY(Vector3 p, float maxY)
        {
            RaycastHit[] hits = Physics.RaycastAll(new Vector3(p.x, maxY + 3f, p.z), Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MinValue;
            foreach (RaycastHit hit in hits)
            {
                if (hit.point.y > maxY || hit.collider.GetComponentInParent<Door>() != null
                    || hit.collider.GetComponentInParent<CharacterController>() != null)
                {
                    continue;
                }

                Renderer hitRenderer = hit.collider.GetComponent<Renderer>();
                if (hitRenderer != null && !hitRenderer.enabled)
                {
                    continue;
                }

                best = Mathf.Max(best, hit.point.y);
            }

            return best == float.MinValue ? 0.05f : best;
        }

        private void HandleStopped(PlayableDirector director)
        {
            _director.stopped -= HandleStopped;
            _isPlaying = false;
            StartRunOut();

            _player.enabled = true;

            OnFinished?.Invoke();
        }
    }
}
