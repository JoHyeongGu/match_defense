using UnityEngine;

namespace MatchDefense.Match
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class BackgroundScaler : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private Camera mainCamera;
        private Vector3 originalScale;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            mainCamera = Camera.main;
            originalScale = transform.localScale;
        }

        private void OnEnable()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            if (mainCamera == null)
                mainCamera = Camera.main;

            originalScale = transform.localScale;
            ScaleToScreen();
        }

        private void ScaleToScreen()
        {
            if (spriteRenderer == null || mainCamera == null)
                return;

            float distance = Mathf.Abs(
                Vector3.Dot(
                    transform.position - mainCamera.transform.position,
                    mainCamera.transform.forward
                )
            );

            float screenHeight = 2f * distance *
                Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);

            float spriteHeight = spriteRenderer.sprite.bounds.size.y;

            if (spriteHeight <= 0f)
                return;

            float scale = screenHeight / spriteHeight;

            transform.localScale = new Vector3(
                originalScale.x * scale,
                originalScale.y * scale,
                originalScale.z * scale
            );
        }
    }
}