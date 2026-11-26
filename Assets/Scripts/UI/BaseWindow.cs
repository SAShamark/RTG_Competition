using System;
using System.Threading.Tasks;
using Services;
using UI.Managers.Components;
using UnityEngine;

namespace UI
{
    public abstract class BaseWindow : MonoBehaviour
    {
        [SerializeField] protected Canvas _canvas;
        [SerializeField] protected CanvasGroup _canvasGroup;
        [SerializeField] protected SafeAreaFitter _safeAreaFitter;
        [SerializeField] protected Animator _animator;
        protected readonly int IsEnable = Animator.StringToHash("IsEnable");

        protected async Task HideAnimation(string clipName)
        {
            _animator.SetBool(IsEnable, false);
            float endTime = Time.unscaledTime + ClipDataProvider.ClipDuration(_animator, clipName);
            try
            {
                // Task.Delay never completes in a single-threaded web build, so the wait is counted in frames
                while (Time.unscaledTime < endTime)
                {
                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                }
            }
            catch (OperationCanceledException exception)
            {
                Debug.Log("Popup hide exception: " + exception);
            }
        }
    }
}