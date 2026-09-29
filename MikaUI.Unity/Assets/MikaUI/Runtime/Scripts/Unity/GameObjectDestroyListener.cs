using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace MikaUI
{
    public class GameObjectDestroyListener : MonoBehaviour
    {
        Dictionary<int, Action> onDestroyActions = new();
        bool isDestroying;
        bool isPendingRemoval;


        private void OnDestroy()
        {
            isDestroying = true;
            var actions = onDestroyActions.Values.ToArray();
            onDestroyActions.Clear();

            foreach (var action in actions)
            {
                action?.Invoke();
            }
        }

        public static Action AttachTo(int uiManagerHashCode, GameObject gameObject, Action onDestroy)
        {
            gameObject.TryGetComponent<GameObjectDestroyListener>(out var listener);
            if (listener != null && (listener.isPendingRemoval || listener.isDestroying))
            {
                listener = gameObject.GetComponents<GameObjectDestroyListener>()
                    .FirstOrDefault(candidate => candidate.isPendingRemoval == false &&
                                                 candidate.isDestroying == false);
            }

            if (listener == null)
            {
                listener = gameObject.AddComponent<GameObjectDestroyListener>();
            }

            listener.onDestroyActions.TryGetValue(uiManagerHashCode, out var existingAction);
            listener.onDestroyActions[uiManagerHashCode] = existingAction + onDestroy;

            var unsubscribed = false;
            return () =>
            {
                if (unsubscribed)
                    return;

                unsubscribed = true;
                if (listener == null || listener.isDestroying ||
                    listener.onDestroyActions.TryGetValue(uiManagerHashCode, out var action) == false)
                    return;

                action -= onDestroy;
                if (action == null)
                    RemoveFrom(listener, uiManagerHashCode);
                else
                    listener.onDestroyActions[uiManagerHashCode] = action;
            };
        }

        public static void RemoveFrom(GameObjectDestroyListener listener, int uiManagerHashCode)
        {
            if (listener == null)
                return;

            listener.onDestroyActions.Remove(uiManagerHashCode);

            if (listener.onDestroyActions.Count == 0 && listener.isDestroying == false &&
                listener.isPendingRemoval == false)
            {
                listener.isPendingRemoval = true;
                GameObject.Destroy(listener);
            }
        }


    }
}
