using System;
using System.Threading.Tasks;
using MikaUISystem;
using UnityEngine;
using UnityEngine.UI;

public class UI_HelloWorld : MonoBehaviour, IVisualUI, IUIEffectable
{

    [SerializeField]
    Text helloText;

    [SerializeField]
    Text mikaText;

    public async Task Invoke(string name)
    {
        var displayName = name switch
        {
            "" or null => "World",
            _ => name
        };

        string helloDisplayText = $"Hello, {displayName}!";
        string mikaDisplayText = "Enjoy Mika!";

        await Task.Delay(800);

        await TypeText(helloText, helloDisplayText);

        await Task.Delay(800);

        await TypeText(mikaText, mikaDisplayText);

        await Task.Delay(3000);

        await EraseText(mikaText);
        await EraseText(helloText);


        static async Task TypeText(Text uiText, string content)
        {
            for (int i = 0; i <= content.Length; i++)
            {
                uiText.text = content.Substring(0, i);
                await Task.Delay(UnityEngine.Random.Range(100, 200));
            }
        }

        static async Task EraseText(Text uiText)
        {
            var content = uiText.text;
            for (int i = content.Length; i >= 0; i--)
            {
                uiText.text = content.Substring(0, i);
                await Task.Delay(UnityEngine.Random.Range(30, 80));
            }
        }

    }

    public Action UseEffect()
    {
        helloText.text = "";
        mikaText.text = "";

        return () =>
        {

        };
    }
}
