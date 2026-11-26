using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public sealed class ShutterView : MonoBehaviour
    {
        [SerializeField] private Hatch _hatch;
        [SerializeField] private Transform[] _parts;
        [SerializeField] private int _samples;
        [SerializeField] private Vector3[] _positions;
        [SerializeField] private Quaternion[] _rotations;

        private void Awake() => _hatch.Moved += HandleMoved;

        private void OnDestroy() => _hatch.Moved -= HandleMoved;

        private void HandleMoved(float openness)
        {
            float frame = Mathf.Clamp01(openness) * (_samples - 1);
            int from = Mathf.Min(Mathf.FloorToInt(frame), _samples - 2);
            float k = frame - from;
            for (int i = 0; i < _parts.Length; i++)
            {
                int a = from * _parts.Length + i;
                int b = a + _parts.Length;
                _parts[i].SetLocalPositionAndRotation(Vector3.Lerp(_positions[a], _positions[b], k), Quaternion.Slerp(_rotations[a], _rotations[b], k));
            }
        }
    }
}
