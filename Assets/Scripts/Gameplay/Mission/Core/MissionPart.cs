using Gameplay.Dialogue;
using Gameplay.Entities.Interactive;
using Gameplay.Mission.Systems;
using System;
using UI.Screens.Variants.Gameplay;
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

        protected void Say(string lineId, Transform speaker = null) => Screen.Say(Dialogue.Library.Get(lineId), speaker);

        protected void Talk(DialogueLine[] lines, Action onFinished = null) => Dialogue.Play(lines, onFinished);

        protected DialogueLine PlayerLine(string lineId, Transform listener) => Dialogue.Line(lineId, PlayerHead, listener);

        protected DialogueLine LineToPlayer(string lineId, Transform speaker) => Dialogue.Line(lineId, speaker, PlayerHead);

        protected void Guide(Transform[] route, Collider zone, TextId label) => Navigation.SetRoute(route, zone, Texts[label]);

        protected void GuideTo(Transform point, TextId label) => Guide(new[] { point }, null, label);

        protected void GuideToWindow(EvacuationWindow window) => GuideTo(window.transform, TextId.NavWindow);
    }
}
