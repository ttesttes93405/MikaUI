using System.Reflection;
using MikaUI.Plugin;

namespace MikaUI
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

        public void OnVirtualUICreated<T>(UIControlToken<T> token, IBaseUI parentUI) where T : class, IVirtualUI, new()
        {
            InjectUIManager(token.UI, manager);
        }

        static void InjectUIManager(object target, UIManager<TUI, TContainer, TSlotConfig> uiManager)
        {
            // Search the most-derived type first, including private fields declared
            // on each base type. GetFields alone omits inherited private fields.
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                foreach (var field in fields)
                {
                    // Broad fields such as IDisposable and object may accept a manager,
                    // but they are not declarations asking for manager injection.
                    if (typeof(UIManager<TUI, TContainer, TSlotConfig>).IsAssignableFrom(field.FieldType) == false
                        || field.FieldType.IsAssignableFrom(uiManager.GetType()) == false)
                    {
                        continue;
                    }

                    field.SetValue(target, uiManager);
                    return;
                }
            }
        }


    }
}
