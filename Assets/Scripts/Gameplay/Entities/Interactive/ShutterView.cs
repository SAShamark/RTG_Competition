using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public sealed class ShutterView : MonoBehaviour
    {
        [SerializeField] private Hatch _hatch;
        [SerializeField] private AnimationClip _clip;
        [SerializeField] private GameObject _model;

        private void Awake() => _hatch.Moved += HandleMoved;

        private void OnDestroy() => _hatch.Moved -= HandleMoved;

        private void HandleMoved(float openness) => _clip.SampleAnimation(_model, openness * _clip.length);
    }
}
