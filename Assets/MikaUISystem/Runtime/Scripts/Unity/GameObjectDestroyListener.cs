using System;
using System.Collections.Generic;
using UnityEngine;


namespace MikaUISystem
{
    public class GameObjectDestroyListener : MonoBehaviour
    {
        Dictionary<int, Action> onDestroyActions = new();


        private void OnDestroy()
        {
            foreach (var action in onDestroyActions.Values)
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

