using System;
using UnityEngine;
using UnityEngine.UIElements;
using DG.Tweening;

namespace MatchDefense.UI
{
    public static class CoinFlyEffect
    {
        public static void ApplyCoinSprite(VisualElement badgeElement, Label fallbackText = null)
        {
            if (badgeElement == null) return;
            Sprite sprite = ProgramManager.Instance != null ? ProgramManager.Instance.CoinIconSprite : null;

            if (sprite != null)
            {
                badgeElement.style.backgroundImage = new StyleBackground(sprite);
                badgeElement.style.backgroundColor = Color.clear;
                badgeElement.style.borderTopWidth = 0;
                badgeElement.style.borderBottomWidth = 0;
                badgeElement.style.borderLeftWidth = 0;
                badgeElement.style.borderRightWidth = 0;
                badgeElement.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                if (fallbackText != null) fallbackText.style.display = DisplayStyle.None;
            }
            else if (fallbackText != null)
            {
                fallbackText.style.display = DisplayStyle.Flex;
            }
        }

        public static void Play(
            VisualElement root,
            Vector2 startPos,
            VisualElement targetCoinPill,
            Label coinLabel,
            int fromCoins,
            int toCoins,
            int visualCoinCount = 10,
            Action onComplete = null)
        {
            if (root == null || targetCoinPill == null)
            {
                if (coinLabel != null) coinLabel.text = $"{toCoins:N0}";
                onComplete?.Invoke();
                return;
            }

            Vector2 endPos = targetCoinPill.worldBound.center;
            int count = Mathf.Clamp(visualCoinCount, 4, 18);
            int arrivedCount = 0;
            float pillBounceScale = 1f;
            Tween pillTween = null;

            for (int i = 0; i < count; i++)
            {
                int idx = i;
                VisualElement coin = CreateCoinElement(startPos);
                root.Add(coin);

                Vector2 scatter = startPos + new Vector2(
                    UnityEngine.Random.Range(-110f, 110f),
                    UnityEngine.Random.Range(-70f, 70f));

                float delay = idx * 0.05f;
                float scatterDuration = 0.15f;
                float flyDuration = 0.42f;

                Sequence seq = DOTween.Sequence().SetUpdate(true);
                seq.AppendInterval(delay);

                seq.Append(DOVirtual.Vector2(startPos, scatter, scatterDuration, pos => SetCenterPos(coin, pos)).SetEase(Ease.OutQuad));
                seq.Join(DOVirtual.Float(0.4f, 1.15f, scatterDuration, s => coin.style.scale = new Scale(new Vector3(s, s, 1f))).SetEase(Ease.OutBack));

                seq.Append(DOVirtual.Float(0f, 1f, flyDuration, t =>
                {
                    Vector2 mid = (scatter + endPos) * 0.5f + new Vector2(UnityEngine.Random.Range(-10f, 10f), -120f);
                    Vector2 p = EvaluateQuadraticBezier(scatter, mid, endPos, t);
                    SetCenterPos(coin, p);
                    float s = Mathf.Lerp(1.1f, 0.7f, t);
                    coin.style.scale = new Scale(new Vector3(s, s, 1f));
                }).SetEase(Ease.InCubic));

                seq.OnComplete(() =>
                {
                    coin.RemoveFromHierarchy();
                    arrivedCount++;

                    TriggerLightHaptic();
                    SoundManager.Instance?.PlaySFX(SfxType.CoinTick, pitch: 1f + idx * 0.045f, volumeMultiplier: 0.85f);

                    if (coinLabel != null)
                    {
                        float progress = (float)arrivedCount / count;
                        int displayVal = Mathf.RoundToInt(Mathf.Lerp(fromCoins, toCoins, progress));
                        coinLabel.text = $"{displayVal:N0}";
                    }

                    pillTween?.Kill();
                    pillBounceScale = 1.18f;
                    targetCoinPill.style.scale = new Scale(new Vector3(pillBounceScale, pillBounceScale, 1f));
                    pillTween = DOTween.To(
                        () => pillBounceScale,
                        v =>
                        {
                            pillBounceScale = v;
                            targetCoinPill.style.scale = new Scale(new Vector3(v, v, 1f));
                        },
                        1f,
                        0.18f
                    ).SetEase(Ease.OutBack).SetUpdate(true);

                    if (arrivedCount >= count)
                    {
                        if (coinLabel != null) coinLabel.text = $"{toCoins:N0}";
                        onComplete?.Invoke();
                    }
                });
            }
        }

        private static VisualElement CreateCoinElement(Vector2 centerPos)
        {
            const float size = 56f;
            var coin = new VisualElement { pickingMode = PickingMode.Ignore };
            coin.style.position = Position.Absolute;
            coin.style.width = size;
            coin.style.height = size;
            coin.style.justifyContent = Justify.Center;
            coin.style.alignItems = Align.Center;
            coin.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));

            Sprite customSprite = ProgramManager.Instance != null ? ProgramManager.Instance.CoinIconSprite : null;
            if (customSprite != null)
            {
                coin.style.backgroundImage = new StyleBackground(customSprite);
                coin.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            }
            else
            {
                coin.style.backgroundColor = new Color(1f, 0.78f, 0.16f, 1f);
                coin.style.borderTopWidth = coin.style.borderBottomWidth = coin.style.borderLeftWidth = coin.style.borderRightWidth = 5f;
                Color borderCol = new Color(0.84f, 0.52f, 0.08f, 1f);
                coin.style.borderTopColor = coin.style.borderBottomColor = coin.style.borderLeftColor = coin.style.borderRightColor = borderCol;
                coin.style.borderTopLeftRadius = coin.style.borderTopRightRadius = coin.style.borderBottomLeftRadius = coin.style.borderBottomRightRadius = size * 0.5f;

                var label = new Label("C") { pickingMode = PickingMode.Ignore };
                label.style.fontSize = 26f;
                label.style.color = new Color(0.56f, 0.28f, 0.04f, 1f);
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                coin.Add(label);
            }

            SetCenterPos(coin, centerPos);
            return coin;
        }

        private static void SetCenterPos(VisualElement el, Vector2 center)
        {
            el.style.left = center.x - 28f;
            el.style.top = center.y - 28f;
        }

        private static Vector2 EvaluateQuadraticBezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        public static void TriggerLightHaptic()
        {
            if (ProgramManager.Instance != null && !ProgramManager.Instance.IsVibrationEnabled)
                return;

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
                            AndroidJavaObject effect = vibrationEffect.CallStatic<AndroidJavaObject>("createOneShot", 18L, 40);
                            vibrator.Call("vibrate", effect);
                        }
                    }
                }
            }
            catch { }
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }
    }
}
