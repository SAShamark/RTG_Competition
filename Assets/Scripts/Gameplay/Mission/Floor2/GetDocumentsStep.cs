using Gameplay.Dialogue;
using Gameplay.Entities.Characters.Residents;
using Gameplay.Entities.Interactive;
using Gameplay.Mission.Core;
using Gameplay.Entities.Characters;
using UnityEngine;

namespace Gameplay.Mission.Floor2
{
    public sealed class GetDocumentsStep : MissionStep
    {
        private readonly Floor2Setup _setup;
        private int _refusals;

        public GetDocumentsStep(Floor2Setup setup, MissionContext context) : base(context)
        {
            _setup = setup;
        }

        public override string RadioHint => Texts[_setup.IsSafeFound ? TextId.RadioOpenSafe : TextId.RadioFindSafe];

        private CharacterControl Grandma => _setup.Grandma.Character;

        public override void Enter()
        {
            if (_setup.Documents.IsTaken)
            {
                Complete();
                return;
            }

            RefreshObjective();
            Screen.ClearDialog();
            Talk(new[]
            {
                PlayerLine(DialogIds.PlayerEvacuateGrandma, Grandma),
                LineToPlayer(DialogIds.GrandmaNoDocuments, Grandma),
                PlayerLine(DialogIds.PlayerWhichDocuments, Grandma),
                LineToPlayer(DialogIds.GrandmaNowhereWithout, Grandma),
                PlayerLine(DialogIds.PlayerWhereAreThey, Grandma),
                LineToPlayer(DialogIds.GrandmaSafeBehindPainting, Grandma),
            });

            foreach (WallPainting painting in _setup.Paintings)
            {
                painting.Moved += HandlePaintingMoved;
            }

            _setup.Safe.Opened += HandleSafeOpened;
            _setup.Grandma.Refused += HandleRefused;
            _setup.Documents.Taken += Complete;
        }

        public override void Dispose()
        {
            foreach (WallPainting painting in _setup.Paintings)
            {
                painting.Moved -= HandlePaintingMoved;
            }

            _setup.Safe.Opened -= HandleSafeOpened;
            _setup.Grandma.Refused -= HandleRefused;
            _setup.Documents.Taken -= Complete;
        }

        private void RefreshObjective()
        {
            if (_setup.Safe.IsOpen)
            {
                SetObjective(TextId.ObjectiveTakeDocuments);
            }
            else
            {
                SetObjective(_setup.IsSafeFound ? TextId.ObjectiveOpenSafe : TextId.ObjectiveFindDocuments);
            }
        }

        private void HandlePaintingMoved(WallPainting painting) => RefreshObjective();

        private void HandleSafeOpened(WallSafe safe) => RefreshObjective();

        private void HandleRefused(Rescuable grandma)
        {
            string line = ++_refusals % 2 == 1 ? DialogIds.GrandmaInsists
                : _setup.IsSafeFound ? DialogIds.GrandmaOpenSafe : DialogIds.GrandmaLookAtPaintings;
            Say(line, Grandma.Head);
        }
    }
}
