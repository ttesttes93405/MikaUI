using System;
using System.Collections.Generic;
using MikaUISystem.Plugin;

namespace MikaUISystem
{
    public class UIInitPlugin<TUI, TContainer> :
        IPlugin<TUI, TContainer>,
        IPluginUICreatedHandler<TUI, TContainer>,
        IPluginVirtualUICreatedHandler
        where TUI : class where TContainer : class
    {

        readonly HashSet<Guid> uiInitSet = new();

        public int SortingOrder => 0;

        public void Install(UIManager<TUI, TContainer> manager)
        {
        }

        public void OnUICreated<T>(string name, UIControlToken<T, TContainer> token, TContainer container, IUI parentUI, SlotRectConfigs slotRectConfigs, TUI template) where T : TUI, IUI
        {
            var ui = token.UI;
            var id = token.ElementID;
            if (ui is IUIInit uiInit && uiInitSet.Contains(id) == false)
            {
                uiInit.Init();
                uiInitSet.Add(id);
            }
        }

        public void OnVirtualUICreated<T>(VirtualUIControlToken<T> token, IUI parentUI) where T : IVirtualUI, new()
        {
            var ui = token.UI;
            var id = token.ElementID;
            if (ui is IUIInit uiInit)
            {
                uiInit.Init();
                uiInitSet.Add(id);
            }
        }

    }

}