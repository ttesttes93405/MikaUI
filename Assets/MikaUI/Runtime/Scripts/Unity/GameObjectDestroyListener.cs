using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace MikaUI
{
    public class GameObjectDestroyListener : MonoBehaviour
    {
        Dictionary<int, Action> onDestroyActions = new();


        private void OnDestroy()
        {
            // A callback can unregister itself while this object is being destroyed.
            foreach (var action in onDestroyActions.Values.ToArray())
            {
                action?.Invoke();
            }
        }

        public static Action AttachTo(int uiManagerHashCode, GameObject gameObject, Action onDestroy)
        {
            if (gameObject.TryGetComponent<GameObjectDestroyListener>(out var listener) == false)
            {
                listener = gameObject.AddComponent<GameObjectDestroyListener>();
            }

            if (listener.onDestroyActions.TryGetValue(uiManagerHashCode, out var existingAction))
            {
                existingAction += onDestroy;
                listener.onDestroyActions[uiManagerHashCode] = existingAction;
                return () => listener.onDestroyActions[uiManagerHashCode] -= onDestroy;
            }
            else
            {
                listener.onDestroyActions[uiManagerHashCode] = onDestroy;
                return () => listener.onDestroyActions[uiManagerHashCode] -= onDestroy;
            }

        }

        public static void RemoveFrom(GameObjectDestroyListener listener, int uiManagerHashCode)
        {
            if (listener == null)
                return;

            listener.onDestroyActions.Remove(uiManagerHashCode);

            if (listener.onDestroyActions.Count == 0)
            {
                GameObject.Destroy(listener);
            }
        }


    }
}
