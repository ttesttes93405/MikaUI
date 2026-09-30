using MikaUI;
using UnityEngine;
using UnityEngine.UI;

public class ShopDemo : MonoBehaviour
{
    [SerializeField] DefaultUIElementSource uiElementSource;
    [SerializeField] RectTransform canvasRoot;
    [SerializeField] Canvas canvasTemplate;
    [SerializeField] Button openShopButton;

    readonly ShopProduct product = new ShopProduct(
        "Healing Potion", 100, "Restores 50 HP when used.");

    UnityUIManager uiManager;

    void Start()
    {
        uiManager = new UnityUIManager(
            new DefaultUIElementProvider(uiElementSource.GetSources(), pool: null),
            new CanvasProvider(canvasRoot, canvasTemplate),
            plugins: null);

        openShopButton.onClick.AddListener(OpenProductInfo);
    }

    public async void OpenProductInfo()
    {
        using (var token = await uiManager.Create<UI_ShopProduct>(sortingOrder: 0))
        {
            await token.UI.ShowAsync(product);
        }
    }

    void OnDestroy()
    {
        uiManager?.Dispose();
    }
}
