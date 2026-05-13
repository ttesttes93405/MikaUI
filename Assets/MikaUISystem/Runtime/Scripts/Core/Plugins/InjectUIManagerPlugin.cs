using System.Reflection;
using MikaUISystem.Plugin;

namespace MikaUISystem
{
    public class InjectUIManagerPlugin<TUI, TContainer> :
        IPlugin<TUI, TContainer>,
        IPluginUICreatedHandler<TUI, TContainer>,
        IPluginVirtualUICreatedHandler
        where TUI : class where TContainer : class
    {
        UIManager<TUI, TContainer> manager;

        public int SortingOrder => -1;

        public void Install(UIManager<TUI, TContainer> manager)
        {
            this.manager = manager;
        }

        public void OnUICreated<T>(string name, UIControlToken<T, TContainer> token, TContainer container, IUI parentUI, SlotRectConfigs slotRectConfigs, TUI template) where T : TUI, IUI
        {
            InjectUIManager(token.UI, manager);
        }

        public void OnVirtualUICreated<T>(VirtualUIControlToken<T> token, IUI parentUI) where T : IVirtualUI, new()
        {
            InjectUIManager(token.UI, manager);
        }

        static void InjectUIManager(object target, UIManager<TUI, TContainer> uiManager)
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