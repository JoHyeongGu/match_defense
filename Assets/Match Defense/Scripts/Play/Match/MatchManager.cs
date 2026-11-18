using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

namespace MatchDefense.Match
{
    public class MatchManager : MonoBehaviour
    {
        [Header("UI Toolkit 설정")]
        [SerializeField] private PanelRenderer panelRenderer;

        [Header("게임 리소스 설정")]
        [SerializeField] private ItemVisual[] visualTypes;
        [SerializeField] private float[] itemTypeWeights = new float[] { 1f, 1f, 1f, 1f, 1f };
        [SerializeField] private AudioClip[] matchClips;

        public static MatchManager Instance { get; private set; }

        private MatchBoard boardWidget;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void OnEnable()
        {
            panelRenderer.RegisterUIReloadCallback(OnUIReload);
        }

        private void OnDisable()
        {
            panelRenderer.UnregisterUIReloadCallback(OnUIReload);
        }

        private void OnUIReload(PanelRenderer panel, VisualElement rootElement)
        {
            boardWidget = rootElement.Q<MatchBoard>();

            if (boardWidget != null)
            {
                // 위젯을 찾았다면, 리소스와 코루틴 러너(this)를 넘겨서 초기화
                boardWidget.Initialize(this, visualTypes, itemTypeWeights, TriggerHapticAndSound);
            }
            else
            {
                Debug.LogWarning("루트 UI 요소에서 MatchBoard을 찾을 수 없습니다. UI Builder에서 추가했는지 확인해주세요.");
            }
        }

        // 웨이브 매니저 등에서 접근하기 위한 퍼블릭 API
        public void SetMatchActive(bool isActive)
        {
            if (boardWidget != null)
            {
                boardWidget.SetMatchActive(isActive);
            }
        }

        #region Sound & Haptics
        // 위젯 내부에서 매치가 발생할 때 호출되는 콜백 함수
        private void TriggerHapticAndSound(float delayTime, int matchCount)
        {
            StartCoroutine(DelayedHapticRoutine(delayTime, matchCount));
        }

        private IEnumerator DelayedHapticRoutine(float delayTime, int matchCount)
        {
            if (SoundManager.Instance != null && matchClips != null && matchClips.Length > 0)
            {
                int safeMatchCount = Mathf.Max(1, matchCount);

                for (int i = 0; i < safeMatchCount; i++)
                {
                    float pitch = 1f + (i * 0.08f);
                    SoundManager.Instance.PlayRandomSFX(matchClips, 1f, pitch);
                    if (i < safeMatchCount - 1)
                        yield return new WaitForSeconds(0.05f);
                }
            }

            yield return new WaitForSeconds(delayTime);

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    if (vibrator != null)
                    {
                        using (AndroidJavaClass vibrationEffect = new AndroidJavaClass("android.os.VibrationEffect"))
                        {
                            AndroidJavaObject effect = vibrationEffect.CallStatic<AndroidJavaObject>("createOneShot", 300L, 5);
                            vibrator.Call("vibrate", effect);
                        }
                    }
                }
            }
            catch
            {
                try
                {
                    using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (AndroidJavaObject vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                    {
                        vibrator.Call("vibrate", 150L);
                    }
                }
                catch { }
            }
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }
        #endregion
    }
}