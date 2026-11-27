using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace MatchDefense.Defense
{
    public class Enemy : Character
    {
        private enum State { Walk, Die }

        [Header("<color=yellow>Enemy Identity</color>")]
        [SerializeField] private string enemyId = "enemy_normal";
        [SerializeField] private string nameLocalizationKey = "enemy_normal_name";
        [SerializeField] private string descLocalizationKey = "enemy_normal_desc";

        [Header("<color=yellow>Move</color>")]
        [SerializeField] private float speed = 1f;
        [SerializeField] private int leakDamage = 1;

        [Header("<color=yellow>Animation</color>")]
        [SerializeField] private int animIndex = 0;
        [SerializeField] private Renderer bodyRenderer;

        [Header("<color=yellow>UI (Screen Space HP)</color>")]
        [SerializeField] private RectTransform hpUIPrefab;
        [SerializeField] private Vector3 hpBarOffset = new Vector3(0, 1.5f, 0);

        public string EnemyId => enemyId;
        public string NameLocalizationKey => nameLocalizationKey;
        public string DescLocalizationKey => descLocalizationKey;
        public float CurrentSpeed => pathTween != null ? runtimeSpeed * pathTween.timeScale : runtimeSpeed;
        public float TraveledDistance { get; private set; }

        public static event Action<Enemy> OnAnyEnemyDied;
        public event Action<Enemy> OnDied;

        private Tween pathTween;
        private MaterialPropertyBlock propBlock;
        private float pathLength;
        private float runtimeSpeed;
        private Vector3 initialMeshScale;

        private Camera mainCam;
        private RectTransform hpBarInstance;
        private Image hpFillImage;
        private bool isHpBarActive = false;
        private bool isDead;
        private bool hasReachedGoal;

        private static readonly int CurrentAnimIndexId = Shader.PropertyToID("_CurrentAnimIndex");
        private static readonly int TransitionStartTimeId = Shader.PropertyToID("_TransitionStartTime");
        private static readonly int StartTime0Id = Shader.PropertyToID("_StartTime0");
        private static readonly int StartTime1Id = Shader.PropertyToID("_StartTime1");
        private static readonly int VatAnimFpsId = Shader.PropertyToID("_VatAnimFps");
        private static readonly int VatPosTex0Id = Shader.PropertyToID("_VatPosTex0");
        private static readonly int VatPosTex1Id = Shader.PropertyToID("_VatPosTex1");

        #region Unity Lifecycle
        protected override void Awake()
        {
            base.Awake();
            runtimeSpeed = speed;

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

        private void LateUpdate()
        {
            if (isHpBarActive && hpBarInstance != null && mainCam != null)
            {
                hpBarInstance.position = mainCam.WorldToScreenPoint(transform.position + hpBarOffset);
            }
        }

        private void OnDestroy()
        {
            pathTween?.Kill();

            if (hpBarInstance != null)
            {
                hpFillImage?.DOKill();
                Destroy(hpBarInstance.gameObject);
            }

            if (EnemySpawner.Instance != null) EnemySpawner.Instance.RemoveEnemy(this);
        }
        #endregion

        #region UI
        private void InitHPBar()
        {
            if (hpUIPrefab == null) return;

            Canvas mainCanvas = FindAnyObjectByType<Canvas>();
            if (mainCanvas != null)
            {
                hpBarInstance = Instantiate(hpUIPrefab, mainCanvas.transform);
                Image[] images = hpBarInstance.GetComponentsInChildren<Image>();
                if (images.Length > 1) hpFillImage = images[1];
                else if (images.Length == 1) hpFillImage = images[0];
                hpBarInstance.gameObject.SetActive(false);
                isHpBarActive = false;
            }
        }
        #endregion

        #region State
        public void Init(Vector3[] waypoints, float length, float hpMultiplier = 1f, float speedMultiplier = 1f)
        {
            if (waypoints == null || waypoints.Length == 0) return;

            pathLength = length;
            maxHP = Mathf.Max(0.1f, maxHP * Mathf.Max(0.1f, hpMultiplier));
            currentHP = maxHP;
            runtimeSpeed = Mathf.Max(0.1f, speed * Mathf.Max(0.1f, speedMultiplier));

            if (bodyRenderer != null)
            {
                bodyRenderer.GetPropertyBlock(propBlock);
                propBlock.SetFloat(CurrentAnimIndexId, animIndex);
                propBlock.SetFloat(TransitionStartTimeId, -10f);
                bodyRenderer.SetPropertyBlock(propBlock);
            }

            pathTween = transform.DOPath(waypoints, runtimeSpeed, PathType.Linear)
                .SetSpeedBased(true)
                .SetEase(Ease.Linear)
                .SetLookAt(0.003f)
                .OnUpdate(() => TraveledDistance = pathTween.ElapsedPercentage() * pathLength)
                .OnComplete(() =>
                {
                    pathTween = null;
                    OnReachedGoal();
                });
        }

        private void OnReachedGoal()
        {
            if (isDead || hasReachedGoal) return;
            hasReachedGoal = true;

            GameManager.Instance?.TakeDamage(leakDamage);
            Destroy(gameObject);
        }

        public override void Attacked(float damage)
        {
            if (!IsAlive) return;

            float hpRatio = Mathf.Clamp01((currentHP - damage) / maxHP);

            if (!isHpBarActive && hpBarInstance != null)
            {
                isHpBarActive = true;
                hpBarInstance.gameObject.SetActive(true);
                if (mainCam != null)
                    hpBarInstance.position = mainCam.WorldToScreenPoint(transform.position + hpBarOffset);
            }

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

        protected override void Die()
        {
            if (isDead) return;
            isDead = true;

            Match.MatchManager.Instance?.AddTurn(1);
            SoundManager.Instance?.PlaySFX(SfxType.EnemyDie);

            pathTween?.Pause();

            if (hpBarInstance != null) hpBarInstance.gameObject.SetActive(false);
            isHpBarActive = false;

            OnDied?.Invoke(this);
            ProgramManager.Instance?.AddQuestProgress(QuestType.KillEnemy, 1);
            OnAnyEnemyDied?.Invoke(this);

            StartCoroutine(DieRoutine());
        }

        private IEnumerator DieRoutine()
        {
            float delay = SetAnimation(State.Die);
            if (delay > 0f) yield return new WaitForSeconds(delay);

            transform.DOScale(0f, 0.5f).SetEase(Ease.InBack).OnComplete(() =>
            {
                transform.DOKill();
                Destroy(gameObject);
            });
        }

        public void SetTutorialHP(float hp = 0.1f)
        {
            maxHP = Mathf.Max(0.01f, hp);
            currentHP = maxHP;
        }

        public void SetMoveSpeed(float _multi = 1.0f)
        {
            if (!IsAlive || pathTween == null) return;
            pathTween.timeScale = _multi;
        }
        #endregion

        #region Animation
        private float SetAnimation(State state)
        {
            int targetAnim = (int)state;
            if (animIndex == targetAnim || bodyRenderer == null) return 0f;

            animIndex = targetAnim;

            bodyRenderer.GetPropertyBlock(propBlock);
            propBlock.SetFloat(CurrentAnimIndexId, targetAnim);
            propBlock.SetFloat(TransitionStartTimeId, Time.time);

            if (targetAnim == 0)
                propBlock.SetFloat(StartTime0Id, Time.time);
            else
                propBlock.SetFloat(StartTime1Id, Time.time);

            bodyRenderer.SetPropertyBlock(propBlock);

            return GetAnimDuration(targetAnim);
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
