using Gameplay;
using System;
using UI.Managers;
using UI.Screens;
using UnityEngine;

[Serializable]
public sealed class GameManager
{
    [SerializeField] private GameplayManager _gameplay = new();

    public void Initialize()
    {
        UIManager uiManager = UIManager.Instance;
        uiManager.Init();
        uiManager.ScreensManager.ShowScreen(ScreenTypes.Gameplay);
        _gameplay.Initialize(uiManager);
    }

    public void Tick(float deltaTime) => _gameplay.Tick(deltaTime);

    public void LateTick() => _gameplay.LateTick();

    public void Dispose() => _gameplay.Dispose();
}
