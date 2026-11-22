using Gameplay.Dialogue;
using Gameplay.Entities.Player;
using UI.Screens.Variants.Gameplay;

namespace Gameplay.Mission
{
    public sealed class MissionContext
    {
        public MissionContext(GameplayScreen screen, FirstPersonController player, DialogueDirector dialogue,
            NavigationGuide navigation, EvacuationController evacuation, TextLibrary texts)
        {
            Screen = screen;
            Player = player;
            Dialogue = dialogue;
            Navigation = navigation;
            Evacuation = evacuation;
            Texts = texts;
        }

        public GameplayScreen Screen { get; }
        public FirstPersonController Player { get; }
        public DialogueDirector Dialogue { get; }
        public NavigationGuide Navigation { get; }
        public EvacuationController Evacuation { get; }
        public TextLibrary Texts { get; }
    }
}
