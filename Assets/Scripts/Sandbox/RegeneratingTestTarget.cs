using Gameplay.Entities.Interactive;
using UnityEngine;

namespace Sandbox
{
    public sealed class RegeneratingTestTarget : MonoBehaviour
    {
        [SerializeField] private Breakable _breakable;
        [SerializeField] private BreakableGlass _glass;
        [SerializeField] private float _restoreDelay = 8f;
        [SerializeField] private GameObject _restoreEffect;
        [SerializeField] private float _effectLifetime = 2f;

        private float _sinceBroken;

        private bool IsBroken => (_breakable != null && _breakable.IsBroken) || (_glass != null && _glass.IsShattered);

        private void Update()
        {
            if (!IsBroken)
            {
                _sinceBroken = 0f;
                return;
            }

            _sinceBroken += Time.deltaTime;
            if (_sinceBroken < _restoreDelay)
            {
                return;
            }

            _sinceBroken = 0f;
            if (_breakable != null)
            {
                _breakable.Restore();
            }

            if (_glass != null)
            {
                _glass.Restore();
            }

            if (_restoreEffect != null)
            {
                Destroy(Instantiate(_restoreEffect, transform.position, Quaternion.identity), _effectLifetime);
            }
        }
    }
}
