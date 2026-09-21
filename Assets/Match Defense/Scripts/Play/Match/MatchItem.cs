using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using DG.Tweening;

namespace MatchDefense.Match
{
    [Serializable]
    public struct ItemVisual
    {
        [Tooltip("평상시 보여질 기본 스프라이트")]
        public Sprite defaultSprite;
        [Tooltip("매치될 때 재생될 스톱모션 스프라이트 배열")]
        public Sprite[] mashSprites;
    }

    [RequireComponent(typeof(RectTransform), typeof(Image))]
    public class MatchItem : MonoBehaviour
    {
        public int ItemType { get; private set; }
        public Vector2 BoardPos { get; set; }
        public RectTransform RectTr => rectTransform;

        [Header("<color=yellow>Visuals</color>")]
        [SerializeField] private ItemVisual[] visualTypes;

        [Header("<color=yellow>Animation</color>")]
        [SerializeField] private float mashDuration = 0.4f;
        [SerializeField] private float stopMotionFps = 12f;

        private RectTransform rectTransform;
        private Image itemImage;
        private Coroutine mashCoroutine;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            itemImage = GetComponent<Image>();
        }

        public void ChangeType(int typeIndex)
        {
            if (typeIndex < 0 || typeIndex >= visualTypes.Length) return;
            ItemType = typeIndex;
            itemImage.sprite = visualTypes[typeIndex].defaultSprite;
        }

        public void SetSize(float size)
        {
            rectTransform.sizeDelta = new Vector2(size, size);
        }

        #region Interaction
        public void Hover(bool isHover)
        {
            rectTransform.DOKill();
            if (isHover)
            {
                transform.SetAsLastSibling();
                rectTransform.DOAnchorPos(BoardPos + Vector2.up * (rectTransform.rect.height * 0.15f), 0.1f);
                rectTransform.DOScale(Vector3.one * 1.15f, 0.1f);
            }
            else
            {
                rectTransform.DOAnchorPos(BoardPos, 0.1f);
                rectTransform.DOScale(Vector3.one, 0.1f);
            }
        }

        public void SwapTo(Vector2 targetPos, float duration, Action onComplete = null)
        {
            BoardPos = targetPos;
            rectTransform.DOAnchorPos(targetPos, duration).SetEase(Ease.OutQuad).OnComplete(() => onComplete?.Invoke());
        }
        #endregion

        #region Effects
        public void MoveAndMerge(Vector2 targetPos, float duration, Action onComplete)
        {
            rectTransform.DOAnchorPos(targetPos, duration).SetEase(Ease.InQuad).OnComplete(() => onComplete?.Invoke());
        }

        public void PlayMatchEffect(Action onComplete)
        {
            rectTransform.DOKill();
            if (mashCoroutine != null) StopCoroutine(mashCoroutine);

            transform.SetAsLastSibling();
            Vector3 originalScale = transform.localScale;

            Sequence seq = DOTween.Sequence();

            // 스톱모션 애니메이션 시작
            mashCoroutine = StartCoroutine(PlayStopMotion());

            // 지정된 시간동안 스톱모션 유지 후 축소되며 사라짐
            seq.AppendInterval(mashDuration);
            seq.Append(rectTransform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack));

            seq.OnComplete(() =>
            {
                if (mashCoroutine != null) StopCoroutine(mashCoroutine);
                transform.localScale = originalScale;
                itemImage.sprite = visualTypes[ItemType].defaultSprite;
                onComplete?.Invoke();
            });
        }

        private IEnumerator PlayStopMotion()
        {
            Sprite[] frames = visualTypes[ItemType].mashSprites;
            if (frames == null || frames.Length == 0) yield break;

            float delay = 1f / stopMotionFps;
            int frameIndex = 0;

            while (true)
            {
                itemImage.sprite = frames[frameIndex];
                frameIndex = (frameIndex + 1) % frames.Length;
                yield return new WaitForSeconds(delay);
            }
        }
        #endregion
    }
}