using Gameplay.Entities.Interactive;
using System;
using Services;
using UnityEngine;

namespace Gameplay.Cinematics.Finale
{
    [Serializable]
    public sealed class FinaleWideShot
    {
        [SerializeField] private Transform _wideFrom;
        [SerializeField] private Transform _wideTo;
        [SerializeField] private Transform _wideLookAt;
        [SerializeField] private float _blastPower = 0.35f;

        public void Begin(Transform cam) => cam.position = _wideFrom.position;

        public void Tick(Transform cam, float progress, FireSource[] fires)
        {
            cam.position = Vector3.Lerp(_wideFrom.position, _wideTo.position, Easing.Smooth(progress));
            cam.rotation = Quaternion.LookRotation(_wideLookAt.position - cam.position, Vector3.up);

            foreach (FireSource fire in fires)
            {
                if (!fire.IsOut)
                {
                    fire.SetReignite(false);
                    fire.Extinguish(_blastPower, Time.deltaTime);
                }
            }
        }
    }
}
