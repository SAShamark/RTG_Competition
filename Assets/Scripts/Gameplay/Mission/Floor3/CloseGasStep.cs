using Gameplay.Dialogue;
using Gameplay.Entities.Interactive;
using Gameplay.Entities.Interactive.Items;
using Gameplay.Mission.Core;
using Gameplay.Entities.Characters;
using UnityEngine;

namespace Gameplay.Mission.Floor3
{
    public sealed class CloseGasStep : MissionStep
    {
        private const float GasHintSprayTime = 1f;

        private readonly Floor3Setup _setup;
        private readonly WheelSearchController _wheelSearch;
        private readonly ExtinguisherLogic _extinguisher;
        private float _stoveSprayTime;

        public CloseGasStep(Floor3Setup setup, WheelSearchController wheelSearch, MissionContext context) : base(context)
        {
            _setup = setup;
            _wheelSearch = wheelSearch;
            _extinguisher = context.Player.Extinguisher;
        }

        public override string RadioHint
        {
            get
            {
                if (_setup.Valve.IsClosed)
                {
                    return Texts[TextId.RadioGasClosed];
                }

                return Texts[_setup.Handle.IsTaken ? TextId.RadioValves : TextId.RadioPantry];
            }
        }

        private CharacterControl Neighbor => _setup.Neighbor.Character;

        public override void Enter()
        {
            SetObjective(TextId.ObjectiveReachNeighbor);
            Say(DialogIds.NeighborTrapped);
            _setup.Valve.Closed += Refresh;
            _setup.StoveFire.Extinguished += HandleStoveOut;
            if (_setup.Valve.IsClosed)
            {
                Refresh();
                return;
            }

            _extinguisher.FireHit += HandleFireHit;
        }

        public override void Dispose()
        {
            _setup.Valve.Closed -= Refresh;
            _setup.StoveFire.Extinguished -= HandleStoveOut;
            _extinguisher.FireHit -= HandleFireHit;
        }

        private void Refresh()
        {
            if (!_setup.Valve.IsClosed)
            {
                return;
            }

            if (_setup.StoveFire.IsOut)
            {
                Navigation.Clear();
                Complete();
                return;
            }

            SetObjective(TextId.ObjectivePutOutKitchen);
            Guide(_setup.RouteToKitchen, _setup.KitchenZone, TextId.NavKitchen);
        }

        private void HandleStoveOut(FireSource fire) => Refresh();

        private void HandleFireHit(FireSource fire)
        {
            if (fire != _setup.StoveFire || _setup.Valve.IsClosed)
            {
                return;
            }

            _stoveSprayTime += Time.deltaTime;
            if (_stoveSprayTime < GasHintSprayTime)
            {
                return;
            }

            _extinguisher.FireHit -= HandleFireHit;
            ShowToast(TextId.ToastGasFeedsFire);
            Talk(new[]
            {
                PlayerLine(DialogIds.PlayerGasBurning, Neighbor),
                LineToPlayer(DialogIds.NeighborValvesInPantry, Neighbor),
            }, ShowPantryRoute);
        }

        private void ShowPantryRoute()
        {
            if (_setup.Valve.IsClosed)
            {
                return;
            }

            SetObjective(TextId.ObjectiveCloseGas);
            _wheelSearch.Begin();
            Guide(_setup.RouteToPantry, _setup.PantryZone, TextId.NavPantry);
        }
    }
}
