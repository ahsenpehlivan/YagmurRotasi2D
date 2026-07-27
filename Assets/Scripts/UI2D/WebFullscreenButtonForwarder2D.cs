using UnityEngine;
using UnityEngine.UI;

namespace YagmurRotasi2D.UI2D
{
    /// <summary>
    /// Self-wires a "Tam Ekran" Button's onClick to the scene's shared
    /// WebFullscreenController2D in Awake() (idempotent - same pattern as
    /// GameSceneBackButtonForwarder2D/UIButtonSound2D), rather than relying
    /// on the Editor scene builders (MainMenuSceneBuilder2D,
    /// LevelSelectSceneBuilder2D) to call Button.onClick.AddListener() at
    /// Editor time: that call is never actually saved into the scene's
    /// serialized Button.m_OnClick.m_PersistentCalls, so it silently stopped
    /// doing anything after the next Editor restart/domain reload/build -
    /// the exact same root cause the Back button had. Self-wiring the
    /// component itself (not the listener) into the scene fixes this
    /// permanently, since AddComponent from Editor tooling IS persisted.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class WebFullscreenButtonForwarder2D : MonoBehaviour
    {
        [SerializeField] private WebFullscreenController2D fullscreenController;

        private Button button;
        private bool listenerRegistered;

        private void Awake()
        {
            button = GetComponent<Button>();
            if (fullscreenController == null) fullscreenController = FindFirstObjectByType<WebFullscreenController2D>();
            RegisterListenerOnce();
        }

        private void OnEnable()
        {
            RegisterListenerOnce();
        }

        private void RegisterListenerOnce()
        {
            if (listenerRegistered || button == null)
                return;

            button.onClick.AddListener(HandleFullscreenPressed);
            listenerRegistered = true;
        }

        public void HandleFullscreenPressed()
        {
            if (fullscreenController != null)
            {
                fullscreenController.ToggleFullscreen();
            }
            else
            {
                Debug.LogWarning("WebFullscreenButtonForwarder2D: no WebFullscreenController2D found in the scene - fullscreen button is inert.");
            }
        }
    }
}
