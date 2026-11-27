using Gameplay.Dialogue;
using Gameplay.Entities.Interactive;
using Gameplay.Mission.Systems;
using System;
using UI.Screens.Variants.Gameplay;
using Gameplay.Entities.Characters;
using UnityEngine;

namespace Gameplay.Mission.Core
{
    public abstract class MissionPart
    {
        protected MissionPart(MissionContext context)
        {
            Context = context;
        }

        protected MissionContext Context { get; }
        protected GameplayScreen Screen => Context.Screen;
        protected DialogueDirector Dialogue => Context.Dialogue;
        protected NavigationGuide Navigation => Context.Navigation;
        protected TextLibrary Texts => Context.Texts;
        protected Transform PlayerHead => Context.Player.Head;

        protected void SetObjective(TextId id) => Screen.SetObjective(Texts[id]);

        protected void ShowToast(TextId id) => Screen.ShowToast(Texts[id]);

        protected void Say(string lineId) => Screen.Say(Dialogue.Library.Get(lineId));

        protected void Talk(DialogueLine[] lines, Action onFinished = null) => Dialogue.Play(lines, onFinished);

        protected DialogueLine PlayerLine(string lineId, CharacterControl listener) => Dialogue.Line(lineId, Context.Player, listener);

        protected DialogueLine LineToPlayer(string lineId, CharacterControl speaker) => Dialogue.Line(lineId, speaker, Context.Player);

        protected void Guide(Transform[] route, Collider zone, TextId label) => Navigation.SetRoute(route, zone, Texts[label]);

        protected void GuideTo(Transform point, TextId label) => Guide(new[] { point }, null, label);

        protected void GuideToWindow(EvacuationWindow window) => GuideTo(window.transform, TextId.NavWindow);
    }
}
