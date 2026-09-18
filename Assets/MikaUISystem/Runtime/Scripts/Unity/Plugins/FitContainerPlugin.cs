using System;
using UnityEngine;

namespace MikaUISystem.Plugin
{
    public sealed class FitContainerPlugin :
        IPlugin<MonoBehaviour, Transform, SlotRectConfigs>,
        IPluginUICreatedHandler<MonoBehaviour, Transform, SlotRectConfigs>
    {
        public int SortingOrder => 1;
        public void Install(UIManager<MonoBehaviour, Transform, SlotRectConfigs> manager)
        {
        }
        public void Uninstall(UIManager<MonoBehaviour, Transform, SlotRectConfigs> manager)
        {
        }

        public void OnUICreated<T>(string name, UIControlToken<T, Transform> token, Transform container, IBaseUI parentUI, SlotRectConfigs slotRectConfigs, MonoBehaviour template) where T : MonoBehaviour, IVisualUI
        {
            RectTransform templateRectTrans = template.GetComponent<RectTransform>();

            var ui = token.UI;

            if (templateRectTrans == null && slotRectConfigs == null)
            {
                throw new Exception($"Cannot get fit slotRectConfigs : {typeof(T).Name}");
            }

            SlotRectConfigs useSlotRectConfigs = slotRectConfigs ?? new SlotRectConfigs()
            {
                AnchorMin = templateRectTrans.anchorMin,
                AnchorMax = templateRectTrans.anchorMax,
                Pivot = templateRectTrans.pivot,
                SizeDelta = templateRectTrans.sizeDelta,
                AnchorPosition = templateRectTrans.anchoredPosition,
            };
            DoFitContainer(ui, useSlotRectConfigs);

            static void DoFitContainer(T ui, SlotRectConfigs slotRectConfigs)
            {
                if (slotRectConfigs == null)
                    return;

                var rootTrans = ui.transform;

                if (rootTrans is RectTransform rootRectTrans)
                {
                    rootRectTrans.anchorMin = slotRectConfigs.AnchorMin;
                    rootRectTrans.anchorMax = slotRectConfigs.AnchorMax;

                    rootRectTrans.pivot = slotRectConfigs.Pivot;

                    rootRectTrans.sizeDelta = slotRectConfigs.SizeDelta;

                    rootRectTrans.anchoredPosition = slotRectConfigs.AnchorPosition;
                }
            }
        }



    }

}