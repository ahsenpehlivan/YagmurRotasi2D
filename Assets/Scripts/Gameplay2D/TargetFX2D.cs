using UnityEngine;

namespace YagmurRotasi2D.Gameplay2D
{
    /// <summary>Placeholder hooks for target-reached success FX (flower bloom, duck walk). Swap the child objects/sprites for real animations later.</summary>
    public class TargetFX2D : MonoBehaviour
    {
        [SerializeField] private GameObject flowerBloomFX;
        [SerializeField] private GameObject duckWalkFX;

        public void PlaySuccessFX()
        {
            if (flowerBloomFX != null) flowerBloomFX.SetActive(true);
            if (duckWalkFX != null) duckWalkFX.SetActive(true);
        }

        public void ResetFX()
        {
            if (flowerBloomFX != null) flowerBloomFX.SetActive(false);
            if (duckWalkFX != null) duckWalkFX.SetActive(false);
        }
    }
}
