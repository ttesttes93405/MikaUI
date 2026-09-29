using UnityEngine;
using MikaUI;

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

    async void DisplayUI(UnityUIManager uiManager)
    {
        string name = "";
        using (var token = await uiManager.Create<UI_HelloWorld>(sortingOrder: 0))
        {
            await token.UI.Invoke(name);
        }
    }

    UnityUIManager CreateMikaUIManager()
    {
        var pool = DefaultUIElementProvider.CreateBoundedPool(static _ => 8);
        var uiElementProvider = new DefaultUIElementProvider(uiElementSource.GetSources(), pool, poolRoot);

        var mikaUIManager = new UnityUIManager(
            uiElementProvider,
            canvasRoot,
            canvasTemplate,
            plugins: null
        );

        return mikaUIManager;
    }
}
