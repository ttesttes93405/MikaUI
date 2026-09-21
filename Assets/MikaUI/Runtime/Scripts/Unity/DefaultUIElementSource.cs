
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MikaUI
{
    
    [CreateAssetMenu(fileName = "DefaultUIElementProvider", menuName = "MikaUI/DefaultUIElementProvider")]
    public class DefaultUIElementSource : ScriptableObject
    {
        [Serializable]
        public record UIElementSource : DefaultUIElementProvider.IUIElementSource
        {
            public string UIName;
            public MonoBehaviour UITemplate;

            string DefaultUIElementProvider.IUIElementSource.UIName => UIName;

            MonoBehaviour DefaultUIElementProvider.IUIElementSource.UITemplate => UITemplate;
        }


        [SerializeField]
        UIElementSource[] sources = new UIElementSource[0];


        public IEnumerable<UIElementSource> GetSources()
        {
            return sources;
        }
    }
}