using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace YagmurRotasi2D.UI2D
{
    /// <summary>
    /// The gameplay top bar's direct "Back" button - returns to
    /// LevelSelectScene2D in one click, without first opening the pause
    /// menu, but routes through UIManager2D.ReturnToLevelSelect() so it gets
    /// the exact same cleanup (cancels any active water-flow animation and
    /// unlocks input, hides the pause menu if it happens to be open, then
    /// loads LevelSelectScene2D) as the pause menu's own "Bölüm Seçimine
    /// Dön" button - never a second, drifting implementation.
    ///
    /// Self-wires its own Button.onClick in Awake() (idempotent - same
    /// pattern as UIButtonSound2D), rather than relying on
    /// GameSceneWebLayoutBuilder2D to call Button.onClick.AddListener() at
    /// Editor time: a plain runtime AddListener() call made from Editor
    /// tooling is never actually saved into the scene's serialized
    /// Button.m_OnClick.m_PersistentCalls (confirmed empty in the saved
    /// GameScene2D.unity) - it only ever existed in memory for the rest of
    /// that one Editor session, which is why the button silently stopped
    /// doing anything after the next Editor restart/domain reload (the
    /// click sound still played, since UIButtonSound2D wires itself the
    /// same way this component now does). Self-wiring at runtime fixes this
    /// permanently and needs no scene resave.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class GameSceneBackButtonForwarder2D : MonoBehaviour
    {
        [Tooltip("Fallback only - used if UIManager2D cannot be found in the scene (should never happen in GameScene2D). The normal path always routes through UIManager2D.ReturnToLevelSelect() so water-flow cleanup/pause-menu-close happen consistently with every other way back to Level Select.")]
        [SerializeField] private string levelSelectSceneName = "LevelSelectScene2D";

        private Button button;
        private UIManager2D uiManager;
        private bool listenerRegistered;

        private void Awake()
        {
            button = GetComponent<Button>();
            uiManager = FindFirstObjectByType<UIManager2D>();
            RegisterListenerOnce();
        }

        private void OnEnable()
        {
            // Defensive - covers a component added at runtime after Awake
            // already ran elsewhere, or the object being disabled/re-enabled.
            RegisterListenerOnce();
        }

        private void RegisterListenerOnce()
        {
            if (listenerRegistered || button == null)
                return;

            button.onClick.AddListener(GoToLevelSelect);
            listenerRegistered = true;
        }

        public void GoToLevelSelect()
        {
            if (uiManager != null)
            {
                uiManager.ReturnToLevelSelect();
            }
            else
            {
                Debug.LogWarning("GameSceneBackButtonForwarder2D: UIManager2D not found in the scene - falling back to a direct scene load without water-flow/pause-menu cleanup.");
                SceneManager.LoadScene(levelSelectSceneName);
            }
        }
    }
}
