using UnityEngine;

namespace Gameplay.Entities.Environment
{
    public sealed class TruckControl : MonoBehaviour
    {
        [SerializeField] private LightBarLogic _lightBar = new();
        [SerializeField] private WheelSpinLogic _wheels = new();
        [SerializeField] private TyreSmokeLogic _tyreSmoke = new();

        private void Awake()
        {
            _lightBar.Init();
            _wheels.Init();
            _tyreSmoke.Init();
        }

        private void LateUpdate()
        {
            _lightBar.Tick(Time.time);
            _wheels.Tick();
            _tyreSmoke.Tick(Time.deltaTime);
        }
    }
}
