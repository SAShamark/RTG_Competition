using System;
using System.Collections.Generic;
using UI.Managers;
using UI.Screens.Base;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UI.Screens
{
    [Serializable]
    public sealed class ScreensManager : BaseWindowsManager<BaseScreen>, IScreensManager
    {
        private readonly Dictionary<ScreenTypes, ScreenModelData> _screenModelsParsed = new();
        private readonly Dictionary<ScreenTypes, BaseScreen> _screens = new();
        private readonly Stack<BaseScreen> _previousScreens = new();
        public event Action<ScreenTypes> ScreenShowed;

        public BaseScreen GetScreen(ScreenTypes screenType)
        {
            return _screens.TryGetValue(screenType, out BaseScreen baseScreen) ? baseScreen : null;
        }

        public override void OnConfigLoaded(WindowsConfig windowConfig)
        {
            base.OnConfigLoaded(windowConfig);

            foreach (ScreenModelData screenModelData in WindowConfig.ScreenModels)
            {
                if (!_screenModelsParsed.TryAdd(screenModelData.ScreenType, screenModelData))
                {
                    Debug.LogError("There is already set up " + screenModelData.ScreenType + " cant fill it twice!");
                }
            }
        }

        public void ShowScreen(ScreenTypes screenTypes, bool isPrevious = false)
        {
            if (ActiveWindow != null)
            {
                if (ActiveWindow.ScreenData.IsAddToStack)
                {
                    if (!isPrevious)
                    {
                        _previousScreens.Push(ActiveWindow);
                    }

                    ActiveWindow.Hide();
                }
                else
                {
                    RemoveScreen(ActiveWindow);
                    _screens.Remove(ActiveWindow.ScreenData.ScreenType);
                }
            }

            if (_screens.TryGetValue(screenTypes, out BaseScreen screen))
            {
                screen.Show();
                ActiveWindow = screen;
            }
            else
            {
                AddScreen(screenTypes);
            }

            ScreenShowed?.Invoke(screenTypes);
        }

        private void AddScreen(ScreenTypes screenType)
        {
            if (!_screenModelsParsed.ContainsKey(screenType))
            {
                Debug.LogError("Not filled screen by type: " + screenType);
                return;
            }

            ScreenModelData screenModelData = _screenModelsParsed[screenType];
            var baseScreen = Object.Instantiate(screenModelData.Template, _container);
            if (baseScreen == null)
            {
                Debug.LogError("There is no BaseScreen on the template of type " + screenType);
                return;
            }

            baseScreen.ScreenData = screenModelData;
            _screens.Add(screenType, baseScreen);
            ActiveWindow = baseScreen;
            baseScreen.Initialize();
            baseScreen.Show();
        }

        public void ShowPreviousScreen()
        {
            if (_previousScreens.Count > 0)
            {
                BaseScreen previousScreenType = _previousScreens.Pop();
                ShowScreen(previousScreenType.ScreenData.ScreenType, true);
            }
        }

        public void RemoveAllScreens()
        {
            if (ActiveWindow != null)
            {
                RemoveScreen(ActiveWindow);
            }

            while (_previousScreens.Count > 0)
            {
                RemoveScreen(_previousScreens.Pop());
            }
        }
    }
}