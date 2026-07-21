using UnityEngine;
using UnityEngine.SceneManagement;

namespace YagmurRotasi2D.UI2D
{
    /// <summary>
    /// Phase 9B: the gameplay top bar's direct "Back" button loads
    /// LevelSelectScene2D immediately, without first opening the pause menu -
    /// a separate one-click path alongside the existing pause menu's own
    /// "Bölüm Seçimine Dön" (UIManager2D.HandleReturnToLevelSelectRequested).
    /// Kept as its own tiny component (rather than a new UIManager2D field)
    /// so the scene builder can wire it without needing to touch
    /// UIManager2D's serialized fields for a single extra button reference.
    /// </summary>
    public class GameSceneBackButtonForwarder2D : MonoBehaviour
    {
        [SerializeField] private string levelSelectSceneName = "LevelSelectScene2D";

        public void GoToLevelSelect()
        {
            SceneManager.LoadScene(levelSelectSceneName);
        }
    }
}
