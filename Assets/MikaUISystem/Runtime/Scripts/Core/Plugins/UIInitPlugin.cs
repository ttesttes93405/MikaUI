using System;
using System.Collections.Generic;
using MikaUISystem.Plugin;

namespace MikaUISystem
{
    public class UIInitPlugin<TUI, TContainer, TSlotConfig> :
        IPlugin<TUI, TContainer, TSlotConfig>,
        IPluginUICreatedHandler<TUI, TContainer, TSlotConfig>
        where TUI : class where TContainer : class where TSlotConfig : class
    {

        readonly HashSet<Guid> uiInitSet = new();

        public int SortingOrder => 0;

        public void Install(UIManager<TUI, TContainer, TSlotConfig> manager)
        {
        }

        public void Uninstall(UIManager<TUI, TContainer, TSlotConfig> manager)
        {
            uiInitSet.Clear();
        }

        public void OnUICreated<T>(string name, UIControlToken<T, TContainer> token, TContainer container, IBaseUI parentUI, TSlotConfig slotRectConfigs, TUI template) where T : TUI, IVisualUI
        {
            var ui = token.UI;
            var id = token.ElementID;
            if (ui is IUIInit uiInit && uiInitSet.Contains(id) == false)
            {
                uiInit.Init();
                uiInitSet.Add(id);
            }
        }

    }

}