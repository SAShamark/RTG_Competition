using System;

namespace UI.Popups
{
    public interface IPopupsManager
    {
        int OpenCount { get; }
        event Action<PopupTypes> PopupShowed; 
        event Action<PopupTypes> PopupHidden; 
        void ShowPopup(PopupTypes popupType);
        T ShowPopup<T>(PopupTypes popupType) where T : BasePopup;

        void HidePopup(PopupTypes popupType);
        BasePopup GetPopup(PopupTypes popupType);

        void HideAllPopups();
    }
}