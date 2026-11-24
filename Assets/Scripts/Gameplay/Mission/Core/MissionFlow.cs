using Gameplay.Mission.Entrance;
using Gameplay.Mission.Floor1;
using Gameplay.Mission.Floor2;
using Gameplay.Mission.Floor3;
using System;
using UnityEngine;

namespace Gameplay.Mission.Core
{
    [Serializable]
    public sealed class MissionFlow : IDisposable
    {
        [SerializeField] private EntranceSetup _entranceSetup = new();
        [SerializeField] private Floor1Setup _floor1Setup = new();
        [SerializeField] private Floor2Setup _floor2Setup = new();
        [SerializeField] private Floor3Setup _floor3Setup = new();

        private EntranceStage _entrance;
        private Floor1Stage _floor1;
        private Floor2Stage _floor2;
        private Floor3Stage _floor3;
        private MissionStage[] _stages;

        public string RadioHint
        {
            get
            {
                for (int i = _stages.Length - 1; i > 0; i--)
                {
                    if (_stages[i].IsActive)
                    {
                        return _stages[i].RadioHint;
                    }
                }

                return _entrance.RadioHint;
            }
        }

        public event Action EntranceOpened;
        public event Action Completed;

        public void Initialize(MissionContext context)
        {
            _entrance = new EntranceStage(_entranceSetup, context);
            _floor1 = new Floor1Stage(_floor1Setup, context);
            _floor2 = new Floor2Stage(_floor2Setup, context);
            _floor3 = new Floor3Stage(_floor3Setup, _floor2Setup.Flywheel, context);
            _stages = new MissionStage[] { _entrance, _floor1, _floor2, _floor3 };

            foreach (MissionStage stage in _stages)
            {
                stage.Init();
            }

            _entrance.Finished += HandleEntranceOpened;
            _floor1.Finished += _floor2.ShowRouteToFlat;
            _floor2.Finished += _floor3.Begin;
            _floor3.Finished += HandleCompleted;
        }

        public void Tick(float deltaTime)
        {
            foreach (MissionStage stage in _stages)
            {
                stage.Tick(deltaTime);
            }
        }

        public void Dispose()
        {
            _entrance.Finished -= HandleEntranceOpened;
            _floor1.Finished -= _floor2.ShowRouteToFlat;
            _floor2.Finished -= _floor3.Begin;
            _floor3.Finished -= HandleCompleted;

            foreach (MissionStage stage in _stages)
            {
                stage.Dispose();
            }
        }

        public void Start() => _entrance.Begin();

        public void BeginFloor1() => _floor1.Begin();

        private void HandleEntranceOpened() => EntranceOpened?.Invoke();

        private void HandleCompleted() => Completed?.Invoke();
    }
}
