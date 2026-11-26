using Gameplay;
using Services;
using System;
using UI.Managers;
using UI.Screens;
using UnityEngine;

[Serializable]
public sealed class GameManager
{
    [SerializeField] private GameplayManager _gameplay = new();
    [SerializeField] private ShaderWarmup _shaderWarmup = new();

    public void Initialize()
    {
        UIManager uiManager = UIManager.Instance;
        uiManager.Init();
        uiManager.ScreensManager.ShowScreen(ScreenTypes.Gameplay);
        _gameplay.Initialize(uiManager);
        _shaderWarmup.Initialize();
    }

    public void Tick(float deltaTime)
    {
        _shaderWarmup.Tick();
        _gameplay.Tick(deltaTime);
    }

    public void LateTick() => _gameplay.LateTick();

    public void Dispose()
    {
        _gameplay.Dispose();
        _shaderWarmup.Dispose();
    }
}
