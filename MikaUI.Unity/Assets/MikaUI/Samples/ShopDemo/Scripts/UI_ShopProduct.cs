using System.Threading.Tasks;
using MikaUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_ShopProduct : MonoBehaviour, IVisualUI
{
    [SerializeField] TMP_Text productNameText;
    [SerializeField] TMP_Text priceText;
    [SerializeField] Button closeButton;

    TaskCompletionSource<bool> closed;

    public async Task ShowAsync(ShopProduct product)
    {
        closed = new TaskCompletionSource<bool>();
        closeButton.onClick.AddListener(RequestClose);
        productNameText.text = product.Name;
        priceText.text = $"{product.Price} Gold";

        await closed.Task;

        closeButton.onClick.RemoveListener(RequestClose);
    }

    public void RequestClose()
    {
        closed?.TrySetResult(true);
    }
}
