using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

namespace MatchDefense.Match
{
    public class ProgramManager : MonoBehaviour
    {
        public static ProgramManager Instance { get; private set; }
        
        public bool IsLoading { get; private set; } = true; 

        private PanelRenderer panelRenderer;
        private VisualElement loadingScreen;
        private int currentUiVersion = -1;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                
                panelRenderer = GetComponent<PanelRenderer>();
                if (panelRenderer != null)
                {
                    panelRenderer.RegisterUIReloadCallback(OnUIReload);
                }
                else
                {
                    Debug.LogWarning("PanelRenderer 컴포넌트가 없습니다! Inspector에서 추가해주세요.");
                }
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
        }

        private void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
        {
            if (currentUiVersion == version) return;
            currentUiVersion = version;

            loadingScreen = new VisualElement();
            loadingScreen.style.position = Position.Absolute;
            loadingScreen.style.top = 0;
            loadingScreen.style.bottom = 0;
            loadingScreen.style.left = 0;
            loadingScreen.style.right = 0;
            loadingScreen.style.backgroundColor = Color.black;
            loadingScreen.style.justifyContent = Justify.Center;
            loadingScreen.style.alignItems = Align.Center;

            Label loadingText = new Label("Loading");
            loadingText.style.color = Color.white;
            loadingText.style.fontSize = 40;

            loadingScreen.Add(loadingText);
            root.Add(loadingScreen);
        }

        public void FinishLoading()
        {
            StartCoroutine(WaitAndFadeOutRoutine());
        }

        private IEnumerator WaitAndFadeOutRoutine()
        {
            while (loadingScreen == null)
            {
                yield return null;
            }

            float duration = 0.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                loadingScreen.style.opacity = 1f - (elapsed / duration);
                yield return null;
            }

            loadingScreen.style.display = DisplayStyle.None;
            IsLoading = false;
        }
    }
}