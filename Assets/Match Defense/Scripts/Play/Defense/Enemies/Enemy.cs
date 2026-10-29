using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Threading.Tasks;

namespace MatchDefense.Defense
{
    public class Enemy : Character
    {
        private enum State { Walk, Die }

        [Header("<color=yellow>Move</color>")]
        [SerializeField] private float speed = 1f;

        [Header("<color=yellow>Animation</color>")]
        [SerializeField] private int animIndex = 0;
        [SerializeField] private Renderer bodyRenderer;

        [Header("<color=yellow>UI (Screen Space HP)</color>")]
        [SerializeField] private RectTransform hpUIPrefab;
        [SerializeField] private Vector3 hpBarOffset = new Vector3(0, 1.5f, 0);

        public float CurrentSpeed => pathTween != null ? speed * pathTween.timeScale : speed;
        public float TraveledDistance { get; private set; }

        private Tween pathTween;
        private MaterialPropertyBlock propBlock;
        private float pathLength;
        private Vector3 initialMeshScale;

        // UI 캐싱
        private Camera mainCam;
        private RectTransform hpBarInstance;
        private Image hpFillImage;
        private bool isHpBarActive = false;

        private static readonly int CurrentAnimIndexId = Shader.PropertyToID("_CurrentAnimIndex");
        private static readonly int TransitionStartTimeId = Shader.PropertyToID("_TransitionStartTime");
        private static readonly int StartTime0Id = Shader.PropertyToID("_StartTime0");
        private static readonly int StartTime1Id = Shader.PropertyToID("_StartTime1");
        private static readonly int VatAnimFpsId = Shader.PropertyToID("_VatAnimFps");
        private static readonly int VatPosTex0Id = Shader.PropertyToID("_VatPosTex0");
        private static readonly int VatPosTex1Id = Shader.PropertyToID("_VatPosTex1");

        protected override void Awake()
        {
            base.Awake();

            mainCam = Camera.main;
            propBlock = new MaterialPropertyBlock();

            if (bodyRenderer != null)
            {
                initialMeshScale = bodyRenderer.transform.localScale;
                bodyRenderer.GetPropertyBlock(propBlock);
                propBlock.SetFloat(StartTime0Id, -UnityEngine.Random.Range(0f, 5f));
                bodyRenderer.SetPropertyBlock(propBlock);
            }

            InitHPBar();
        }

        private void InitHPBar()
        {
            if (hpUIPrefab == null) return;

            Canvas mainCanvas = FindAnyObjectByType<Canvas>();
            if (mainCanvas != null)
            {
                hpBarInstance = Instantiate(hpUIPrefab, mainCanvas.transform);
                hpFillImage = hpBarInstance.GetComponentsInChildren<Image>()[1];
                hpBarInstance.gameObject.SetActive(false);
                isHpBarActive = false;
            }
        }

        private void LateUpdate()
        {
            // HP 바가 활성화되었을 때만 월드 좌표를 스크린 픽셀 좌표로 변환하여 UI 이동 (최적화 포인트)
            if (isHpBarActive && hpBarInstance != null)
            {
                hpBarInstance.position = mainCam.WorldToScreenPoint(transform.position + hpBarOffset);
            }
        }

        private void OnDestroy()
        {
            pathTween?.Kill();

            // 적이 파괴될 때 Screen Space에 띄워둔 UI도 함께 파괴
            if (hpBarInstance != null)
            {
                hpFillImage?.DOKill();
                Destroy(hpBarInstance.gameObject);
            }

            if (EnemySpawner.Instance != null) EnemySpawner.Instance.RemoveEnemy(this);
        }

        #region State
        public void Init(Vector3[] waypoints, float length)
        {
            if (waypoints == null || waypoints.Length == 0) return;

            pathLength = length;

            if (bodyRenderer != null)
            {
                bodyRenderer.GetPropertyBlock(propBlock);
                propBlock.SetFloat(CurrentAnimIndexId, animIndex);
                propBlock.SetFloat(TransitionStartTimeId, -10f);
                bodyRenderer.SetPropertyBlock(propBlock);
            }

            pathTween = transform.DOPath(waypoints, speed, PathType.Linear)
            .SetSpeedBased(true)
            .SetEase(Ease.Linear)
            .SetLookAt(0.003f)
            .OnUpdate(() => TraveledDistance = pathTween.ElapsedPercentage() * pathLength)
            .OnComplete(() =>
            {
                pathTween = null;
                Destroy(gameObject);
            });
        }

        public override void Attacked(float damage)
        {
            if (!IsAlive) return;

            float hpRatio = Mathf.Clamp01((currentHP - damage) / maxHP);

            // 데미지를 받는 순간 HP 바 표시
            if (!isHpBarActive && hpBarInstance != null)
            {
                isHpBarActive = true;
                hpBarInstance.gameObject.SetActive(true);
                // 켜지자마자 현재 위치로 즉시 갱신시켜 튀는 현상 방지
                hpBarInstance.position = mainCam.WorldToScreenPoint(transform.position + hpBarOffset);
            }

            // DOTween으로 부드럽게 깎이는 연출
            if (hpFillImage != null)
            {
                hpFillImage.DOKill();
                hpFillImage.DOFillAmount(hpRatio, 0.2f).SetEase(Ease.OutQuad);
            }

            if (bodyRenderer != null)
            {
                bodyRenderer.transform.DOKill(true);
                bodyRenderer.transform.localScale = initialMeshScale * Mathf.Lerp(0.4f, 1.0f, hpRatio);
                bodyRenderer.transform.DOPunchScale(new Vector3(0.3f, -0.2f, 0.3f), 0.15f, 2, 0.5f);
            }

            base.Attacked(damage);
        }

        protected override async void Die()
        {
            pathTween?.Pause();

            // 죽는 애니메이션이 나올 때 UI 숨김
            if (hpBarInstance != null) hpBarInstance.gameObject.SetActive(false);
            isHpBarActive = false;

            await SetAnimation(State.Die);
            transform.DOScale(0f, 0.5f).SetEase(Ease.InBack).OnComplete(() =>
            {
                transform.DOKill();
                Destroy(gameObject);
            });
        }

        public void SetMoveSpeed(float _multi = 1.0f)
        {
            if (!IsAlive || pathTween == null) return;
            pathTween.timeScale = _multi;
        }
        #endregion

        #region Animation
        private async Task SetAnimation(State state)
        {
            int targetAnim = (int)state;
            if (animIndex == targetAnim || bodyRenderer == null) return;

            animIndex = targetAnim;

            bodyRenderer.GetPropertyBlock(propBlock);
            propBlock.SetFloat(CurrentAnimIndexId, targetAnim);
            propBlock.SetFloat(TransitionStartTimeId, Time.time);

            if (targetAnim == 0)
                propBlock.SetFloat(StartTime0Id, Time.time);
            else
                propBlock.SetFloat(StartTime1Id, Time.time);

            bodyRenderer.SetPropertyBlock(propBlock);

            float duration = GetAnimDuration(targetAnim);
            if (duration > 0f) await Task.Delay((int)(duration * 1000));
        }

        private float GetAnimDuration(int index)
        {
            if (bodyRenderer == null || bodyRenderer.sharedMaterial == null) return 0f;

            Material mat = bodyRenderer.sharedMaterial;
            float fps = mat.HasProperty(VatAnimFpsId) ? mat.GetFloat(VatAnimFpsId) : 15.0f;
            if (fps <= 0.001f) fps = 15.0f;

            int texID = index == 0 ? VatPosTex0Id : VatPosTex1Id;
            Texture vatTex = mat.GetTexture(texID);

            return vatTex != null ? vatTex.height / fps : 0f;
        }
        #endregion
    }
}