using Gameplay.Entities.Interactive;
using Gameplay.Entities.Player;
using Unity.Cinemachine;
using UnityEngine;

namespace Gameplay.Cinematics
{
    public sealed class UncleBurstCutscene : CinematicBase
    {
        private const float LineTime = 0.25f;
        private const float LineDuration = 2.6f;
        private const float MinDirectionSqr = 0.01f;
        private const float SeenBoundsPadding = 0.5f;
        private const float FallbackGroundY = 0.05f;

        [SerializeField] private CinemachineCamera _lookCamera;
        [SerializeField] private GameObject _actors;
        [SerializeField] private Transform _uncle;
        [SerializeField] private Transform _lookTarget;
        [SerializeField] private Vector3 _doorCenter;
        [SerializeField] private float _duration = 5.6f;

        [SerializeField] private CinematicSettings _settings = new CinematicSettings(0.45f, 1.2f);
        [SerializeField] private SubtitleCue[] _subtitles =
        {
            new SubtitleCue(LineTime, LineDuration, "U01"),
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

        public override CinematicSettings Settings => _settings;
        public override SubtitleCue[] Subtitles => _subtitles;
        protected override float Duration => _duration;

        public void Init(FirstPersonController player)
        {
            _player = player;
            _actors.SetActive(false);
        }

        public void Play()
        {
            if (!TryBegin())
            {
                return;
            }

            Vector3 playerPos = _player.transform.position;
            _path = BuildPath(playerPos);
            _uncle.position = _path[0];

            _player.enabled = false;
            _lookCamera.transform.SetPositionAndRotation(_player.Head.position, _player.Head.rotation);

            PlayTimeline();
        }

        [Header("Run-out")]
        [SerializeField] private float _runOutSpeed = 3.2f;
        [SerializeField] private float _runOutMaxTime = 12f;
        [SerializeField] private float _maxPitchUp = -40f;
        [SerializeField] private float _exitSampleBack = 0.2f;
        [SerializeField] private float _groundProbeLift = 0.4f;
        [SerializeField] private float _unseenLimit = 0.4f;
        [SerializeField] private float _laneOffset = 0.6f;
        [SerializeField] private float _besideMaxBack = 4.4f;
        [SerializeField] private float _besideMinBack = 0.6f;
        [SerializeField] private float _pathStartFront = 2.2f;
        [SerializeField] private float _pathDoorBack = 0.1f;
        [SerializeField] private float _besideBlend = 0.85f;
        [SerializeField] private float _pathPassSide = 0.5f;
        [SerializeField] private float _pathPassBack = 4.9f;
        [SerializeField] private float _pathTurnSide = 2.6f;
        [SerializeField] private float _pathTurnBack = 6.8f;
        [SerializeField] private float _pathStreetSide = 8f;
        [SerializeField] private float _pathStreetBack = 7.4f;
        [SerializeField] private float _pathExitSide = 15f;
        [SerializeField] private float _pathExitBack = 7.6f;
        private const float GroundProbeHeight = 3f;
        private const float GroundProbeDistance = 10f;

        private bool _isRunningOut;
        private float _runOutTime;
        private float _unseenTime;
        private Vector3 _runOutDirection;
        private Renderer[] _actorRenderers;
        private readonly Plane[] _frustum = new Plane[6];

        private void Update()
        {
            if (_isRunningOut)
            {
                TickRunOut();
                return;
            }

            if (!IsPlaying)
            {
                return;
            }

            _uncle.position = SamplePath(ElapsedTime * _runSpeed);
            StopAtEnd();
        }

        private void LateUpdate()
        {
            if (!IsPlaying)
            {
                return;
            }

            Transform cam = _lookCamera.transform;
            Vector3 toTarget = _lookTarget.position - cam.position;
            if (toTarget.sqrMagnitude < MinDirectionSqr)
            {
                return;
            }

            Quaternion desired = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            Vector3 e = desired.eulerAngles;
            float pitch = e.x > 180f ? e.x - 360f : e.x;
            desired = Quaternion.Euler(Mathf.Clamp(pitch, _maxPitchUp, _maxPitchDown), e.y, 0f);
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
            Vector3 a = SamplePath(end - _exitSampleBack);
            Vector3 b = SamplePath(end);
            _runOutDirection = Vector3.ProjectOnPlane(b - a, Vector3.up).normalized;
            if (_runOutDirection.sqrMagnitude < MinDirectionSqr)
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
            next.y = GroundY(next, _uncle.position.y + _groundProbeLift);
            _uncle.position = next;

            bool seen = IsActorSeen();
            _unseenTime = seen ? 0f : _unseenTime + Time.deltaTime;
            if (_unseenTime > _unseenLimit || (_runOutTime > _runOutMaxTime && !seen))
            {
                _isRunningOut = false;
                _actors.SetActive(false);
            }
        }

        private bool IsActorSeen()
        {
            Camera cam = Camera.main;
            if (cam.TryGetComponent(out CinemachineBrain brain) && brain.IsBlending)
            {
                return true;
            }

            GeometryUtility.CalculateFrustumPlanes(cam, _frustum);
            foreach (Renderer actorRenderer in _actorRenderers)
            {
                Bounds bounds = actorRenderer.bounds;
                bounds.Expand(SeenBoundsPadding);
                if (GeometryUtility.TestPlanesAABB(_frustum, bounds))
                {
                    return true;
                }
            }

            return false;
        }

        private Vector3[] BuildPath(Vector3 playerPos)
        {
            float side = playerPos.x >= _doorCenter.x ? -1f : 1f;
            float lane = _doorCenter.x + side * _laneOffset;
            float beside = playerPos.x + side * _passDistance;
            float besideZ = Mathf.Clamp(playerPos.z, _doorCenter.z - _besideMaxBack, _doorCenter.z - _besideMinBack);
            float exitSide = side < 0f ? -1f : 1f;

            var points = new[]
            {
                new Vector3(lane, _doorCenter.y, _doorCenter.z + _pathStartFront),
                new Vector3(lane, _doorCenter.y, _doorCenter.z - _pathDoorBack),
                new Vector3(Mathf.Lerp(lane, beside, _besideBlend), 0f, besideZ),
                new Vector3(_doorCenter.x + side * _pathPassSide, 0f, _doorCenter.z - _pathPassBack),
                new Vector3(_doorCenter.x + exitSide * _pathTurnSide, 0f, _doorCenter.z - _pathTurnBack),
                new Vector3(_doorCenter.x + exitSide * _pathStreetSide, 0f, _doorCenter.z - _pathStreetBack),
                new Vector3(_doorCenter.x + exitSide * _pathExitSide, 0f, _doorCenter.z - _pathExitBack),
            };

            for (int i = 2; i < points.Length; i++)
            {
                points[i].y = GroundY(points[i], _doorCenter.y + _groundProbeLift);
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
            RaycastHit[] hits = Physics.RaycastAll(new Vector3(p.x, maxY + GroundProbeHeight, p.z), Vector3.down, GroundProbeDistance, ~0, QueryTriggerInteraction.Ignore);
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

            return best == float.MinValue ? FallbackGroundY : best;
        }

        protected override void OnStopped()
        {
            StartRunOut();
            _player.enabled = true;
        }
    }
}
