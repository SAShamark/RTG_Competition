using System;
using Gameplay.Cinematics;
using Gameplay.Entities.Interactive;
using Gameplay.Entities.Interactive.Items;
using Gameplay.Entities.Player;
using Gameplay.Mission;
using Gameplay.Mission.Floor3;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Debugging
{
    public sealed class GameplayCheats
    {
        private const float TeleportLift = 0.1f;

        private readonly FirstPersonController _player;
        private readonly IntroCutscene _intro;
        private readonly UncleBurstCutscene _uncleBurst;
        private readonly EntranceController _entrance;
        private readonly EvacuationController _evacuation;
        private readonly Floor1Setup _floor1;
        private readonly Floor2Setup _floor2;
        private readonly Floor3Setup _floor3;
        private readonly Action _openPantry;

        public GameplayCheats(FirstPersonController player, IntroCutscene intro, UncleBurstCutscene uncleBurst, EntranceController entrance,
            EvacuationController evacuation, Floor1Setup floor1, Floor2Setup floor2, Floor3Setup floor3, Action openPantry)
        {
            _player = player;
            _intro = intro;
            _uncleBurst = uncleBurst;
            _entrance = entrance;
            _evacuation = evacuation;
            _floor1 = floor1;
            _floor2 = floor2;
            _floor3 = floor3;
            _openPantry = openPantry;
        }

        public void SkipIntro()
        {
            if (_intro.IsPlaying)
            {
                _intro.Skip();
            }
        }

        public void OpenEntrance()
        {
            SkipIntro();
            if (_entrance.IsOpen)
            {
                return;
            }

            _entrance.ForceOpen();
            if (_uncleBurst.IsPlaying)
            {
                _uncleBurst.Skip();
            }
        }

        public void PutOutFloor1()
        {
            foreach (FireSource fire in _floor1.Fires)
            {
                fire.ForceOut();
            }
        }

        public void GiveTools()
        {
            foreach (ToolItem tool in Object.FindObjectsByType<ToolItem>(FindObjectsInactive.Include))
            {
                if (tool.CanInteract && !_player.Items.Owns(tool.Type))
                {
                    tool.Interact(_player.transform);
                }
            }
        }

        public void RescueGrandma()
        {
            OpenEntrance();
            PutOutFloor1();
            _evacuation.ForceRescue(_floor2.Grandma, _floor2.Window);
        }

        public void GiveWheels()
        {
            foreach (StackItem wheel in new[] { _floor2.Flywheel, _floor3.Handle })
            {
                if (!wheel.IsTaken)
                {
                    wheel.Interact(_player.transform);
                }
            }
        }

        public void OpenPantry() => _openPantry();

        public void RescueNeighbor()
        {
            RescueGrandma();
            _floor3.Valve.ForceClose();
            _floor3.StoveFire.SetReignite(false);
            _floor3.StoveFire.ForceOut();
            _evacuation.ForceRescue(_floor3.Neighbor, _floor3.Window);
        }

        public void Teleport(Transform point)
        {
            var controller = _player.GetComponent<CharacterController>();
            controller.enabled = false;
            _player.transform.SetPositionAndRotation(point.position + Vector3.up * TeleportLift, Quaternion.Euler(0f, point.eulerAngles.y, 0f));
            controller.enabled = true;
        }
    }
}
