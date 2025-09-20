using UnityEngine;
using MikaUISystem;

public class EntryPoint : MonoBehaviour
{

    [SerializeField]
    DefaultUIElementSource uiElementSource;

    [SerializeField]
    Transform poolRoot;

    [SerializeField]
    RectTransform canvasRoot;

    [SerializeField]
    Canvas canvasTemplate;


    void Start()
    {
        var mikaUIManager = CreateMikaUIManager();
        DisplayUI(mikaUIManager);
    }

    async void DisplayUI(UIManager uiManager)
    {
        string name = "";
        using (var token = await uiManager.Create<UI_HelloWorld>(sortingOrder: 0))
        {
            await token.UI.Invoke(name);
        }
    }

    UIManager CreateMikaUIManager()
    {
        var uiElementProvider = new DefaultUIElementProvider(uiElementSource.GetSources(), poolRoot);
        var plugins = MikaUISystem.Plugin.PluginCreator.Create();

        var mikaUIManager = new UIManager(
            uiElementProvider,
            canvasRoot,
            canvasTemplate,
            plugins
        );

        return mikaUIManager;
    }
}
