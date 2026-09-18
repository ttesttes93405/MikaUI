using System.Reflection;
using MikaUISystem.Plugin;

namespace MikaUISystem
{
    public class InjectUIManagerPlugin<TUI, TContainer, TSlotConfig> :
        IPlugin<TUI, TContainer, TSlotConfig>,
        IPluginUICreatedHandler<TUI, TContainer, TSlotConfig>,
        IPluginVirtualUICreatedHandler
        where TUI : class where TContainer : class where TSlotConfig : class
    {
        UIManager<TUI, TContainer, TSlotConfig> manager;

        public int SortingOrder => -1;

        public void Install(UIManager<TUI, TContainer, TSlotConfig> manager)
        {
            this.manager = manager;
        }

        public void Uninstall(UIManager<TUI, TContainer, TSlotConfig> manager)
        {
            this.manager = null;
        }

        public void OnUICreated<T>(string name, UIControlToken<T, TContainer> token, TContainer container, IBaseUI parentUI, TSlotConfig slotRectConfigs, TUI template) where T : TUI, IVisualUI
        {
            InjectUIManager(token.UI, manager);
        }

        public void OnVirtualUICreated<T>(UIControlToken<T> token, IBaseUI parentUI) where T : IVirtualUI, new()
        {
            InjectUIManager(token.UI, manager);
        }

        static void InjectUIManager(object target, UIManager<TUI, TContainer, TSlotConfig> uiManager)
        {
            var fields = target.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            foreach (var field in fields)
            {
                if (field.FieldType != uiManager.GetType() &&
                    field.FieldType.IsSubclassOf(uiManager.GetType()) == false)
                {
                    continue;
                }

                field.SetValue(target, uiManager);
                break;
            }
        }


    }
}