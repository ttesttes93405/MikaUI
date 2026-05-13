using System;
using UnityEngine;
using MikaUISystem.Plugin;

namespace MikaUISystem
{
    public sealed class FitContainerPlugin :
        IPlugin<MonoBehaviour, Transform>,
        IPluginUICreatedHandler<MonoBehaviour, Transform>
    {
        public int SortingOrder => 1;
        public void Install(UIManager<MonoBehaviour, Transform> manager)
        {
        }

        public void OnUICreated<T>(string name, UIControlToken<T, Transform> token, Transform container, IUI parentUI, SlotRectConfigs slotRectConfigs, MonoBehaviour template) where T : MonoBehaviour, IUI
        {
            RectTransform templateRectTrans = template.GetComponent<RectTransform>();

            var ui = token.UI;

            if (templateRectTrans == null && slotRectConfigs == null)
            {
                throw new Exception($"Cannot get fit slotRectConfigs : {typeof(T).Name}");
            }

            SlotRectConfigs useSlotRectConfigs = slotRectConfigs ?? new SlotRectConfigs()
            {
                AnchorMin = new(templateRectTrans.anchorMin.x, templateRectTrans.anchorMin.y),
                AnchorMax = new(templateRectTrans.anchorMax.x, templateRectTrans.anchorMax.y),
                Pivot = new(templateRectTrans.pivot.x, templateRectTrans.pivot.y),
                SizeDelta = new(templateRectTrans.sizeDelta.x, templateRectTrans.sizeDelta.y),
                AnchorPosition = new(templateRectTrans.anchoredPosition.x, templateRectTrans.anchoredPosition.y),
            };
            DoFitContainer(ui, useSlotRectConfigs);

            static void DoFitContainer(T ui, SlotRectConfigs slotRectConfigs)
            {
                if (slotRectConfigs == null)
                    return;

                var rootTrans = ui.transform;

                if (rootTrans is RectTransform rootRectTrans)
                {
                    rootRectTrans.anchorMin = new(slotRectConfigs.AnchorMin.X, slotRectConfigs.AnchorMin.Y);
                    rootRectTrans.anchorMax = new(slotRectConfigs.AnchorMax.X, slotRectConfigs.AnchorMax.Y);

                    rootRectTrans.pivot = new(slotRectConfigs.Pivot.X, slotRectConfigs.Pivot.Y);

                    rootRectTrans.sizeDelta = new(slotRectConfigs.SizeDelta.X, slotRectConfigs.SizeDelta.Y);

                    rootRectTrans.anchoredPosition = new(slotRectConfigs.AnchorPosition.X, slotRectConfigs.AnchorPosition.Y);
                }
            }
        }



    }

}