using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace HarmonicEngineV4.UI.Lab2
{
    /// <summary>Ensures UGUI uses the new Input System when Player Settings require it.</summary>
    public static class V4Lab2EventSystemUtility
    {
        public static void EnsureCompatibleInputModule()
        {
#if ENABLE_INPUT_SYSTEM
            EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var go = new GameObject("EventSystem");
                go.AddComponent<EventSystem>();
                go.AddComponent<InputSystemUIInputModule>();
                return;
            }

            StandaloneInputModule legacy = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacy != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(legacy);
                }
                else
                {
                    Object.DestroyImmediate(legacy);
                }
            }

            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }
#else
            EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var go = new GameObject("EventSystem");
                go.AddComponent<EventSystem>();
                go.AddComponent<StandaloneInputModule>();
            }
#endif
        }
    }
}
