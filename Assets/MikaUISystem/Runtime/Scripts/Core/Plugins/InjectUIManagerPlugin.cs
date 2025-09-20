using System.Reflection;
using MikaUISystem.Plugin;

namespace MikaUISystem
{
    public class InjectUIManagerPlugin<TUI, TContiner> :
        IPlugin<TUI, TContiner>,
        IPluginUICreatedHandler<TUI, TContiner>,
        IPluginVirtualUICreatedHandler
        where TUI : class where TContiner : class
    {
        UIManager<TUI, TContiner> manager;

        public int SortingOrder => -1;

        public void Install(UIManager<TUI, TContiner> manager)
        {
            this.manager = manager;
        }

        public void OnUICreated<T>(string name, UILifeToken<T> token, TContiner container, IUI parentUI, SlotRectConfigs slotRectConfigs, TUI template) where T : TUI, IUI
        {
            InjectUIManager(token.UI, manager);
        }

        public void OnVirtualUICreated<T>(VirtualUILifeToken<T> token, IUI parentUI) where T : IVirtualUI, new()
        {
            InjectUIManager(token.UI, manager);
        }

        static void InjectUIManager(object target, UIManager<TUI, TContiner> uiManager)
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