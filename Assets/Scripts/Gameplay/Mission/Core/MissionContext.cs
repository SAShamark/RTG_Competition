using Gameplay.Dialogue;
using Gameplay.Entities.Characters.Player;
using Gameplay.Mission.Systems;
using UI.Screens.Variants.Gameplay;

namespace Gameplay.Mission.Core
{
    public sealed class MissionContext
    {
        public MissionContext(GameplayScreen screen, FirstPersonController player, DialogueDirector dialogue,
            NavigationGuide navigation, EvacuationController evacuation, PuzzlePresenter puzzles, TextLibrary texts)
        {
            Screen = screen;
            Player = player;
            Dialogue = dialogue;
            Navigation = navigation;
            Evacuation = evacuation;
            Puzzles = puzzles;
            Texts = texts;
        }

        public GameplayScreen Screen { get; }
        public FirstPersonController Player { get; }
        public DialogueDirector Dialogue { get; }
        public NavigationGuide Navigation { get; }
        public EvacuationController Evacuation { get; }
        public PuzzlePresenter Puzzles { get; }
        public TextLibrary Texts { get; }
    }
}
