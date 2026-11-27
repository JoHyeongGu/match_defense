using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using MatchDefense.UI;
using DG.Tweening;
using MatchDefense.Match;

namespace MatchDefense.Defense
{
    public enum EnemyArchetype
    {
        Normal,
        Runner,
        Tanker,
        Splitter,
        Assassin,
        SlimeTrail,
        Boss
    }

    public class Enemy : Character
    {
        private enum State { Walk, Die }

        [Header("<color=yellow>Enemy Identity</color>")]
        [SerializeField] private EnemyArchetype archetype = EnemyArchetype.Normal;
        [SerializeField] private string enemyId = "enemy_normal";
        [SerializeField] private string nameLocalizationKey = "enemy_normal_name";
        [SerializeField] private string descLocalizationKey = "enemy_normal_desc";

        [Header("<color=yellow>Move</color>")]
        [SerializeField] private float speed = 1f;
        [SerializeField] private int leakDamage = 1;

        [Header("<color=yellow>Animation & Visual</color>")]
        [SerializeField] private int animIndex = 0;
        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private bool applyTintColor = false;
        [SerializeField] private Color tintColor = Color.white;

        [Header("<color=yellow>Splitter & Boss Spawn</color>")]
        [SerializeField] private GameObject splitSpawnPrefab;
        [SerializeField] private int splitSpawnCount = 3;
        [SerializeField] private float bossSummonInterval = 4.5f;

        [Header("<color=yellow>Assassin Unit Hunter</color>")]
        [SerializeField] private float assassinTriggerProgress = 0.22f;
        [SerializeField] private float assassinWarningDuration = 0.4f;
        [SerializeField] private float assassinLungeDuration = 0.35f;

        [Header("<color=yellow>Slime Trail</color>")]
        [SerializeField] private GameObject slimeZonePrefab;
        [SerializeField] private float slimeDropInterval = 0.8f;
        [SerializeField] private float slimeZoneRadius = 1.25f;
        [SerializeField] private float slimeZoneDuration = 5.5f;
        [SerializeField] private float slimeHasteMultiplier = 1.6f;

        [Header("<color=yellow>Screen Space HP</color>")]
        [SerializeField] private RectTransform hpUIPrefab;
        [SerializeField] private Vector3 hpBarOffset = new Vector3(0, 1.5f, 0);

        public EnemyArchetype Archetype => archetype;
        public string EnemyId => enemyId;
        public string NameLocalizationKey => nameLocalizationKey;
        public string DescLocalizationKey => descLocalizationKey;
        public float CurrentSpeed => pathTween != null ? runtimeSpeed * pathTween.timeScale : runtimeSpeed;
        public float TraveledDistance { get; private set; }
        public float PathProgress => pathLength > 0.001f ? Mathf.Clamp01(TraveledDistance / pathLength) : 0f;

        public static event Action<Enemy> OnAnyEnemyDied;
        public event Action<Enemy> OnDied;

        private Tween pathTween;
        private MaterialPropertyBlock propBlock;
        private float pathLength;
        private float startProgressOffset;
        private float runtimeSpeed;
        private Vector3 initialMeshScale;

        private float slowMultiplier = 1f;
        private readonly Dictionary<object, float> activeHasteSources = new Dictionary<object, float>();

        private float slimeTimer;
        private float bossSummonTimer;
        private bool isAssassinLunging;

        private Camera mainCam;
        private VisualElement hpBarRoot;
        private VisualElement hpBarFill;
        private Tween hpFillTween;
        private float hpBarWidth = 74f;
        private float hpBarHeight = 14f;
        private float currentHpFillRatio = 1f;
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
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

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

            if (applyTintColor)
                ApplyTintToRenderers(tintColor);

            InitHPBar();
        }

        private void Update()
        {
            if (isDead || hasReachedGoal || isAssassinLunging) return;

            if (archetype == EnemyArchetype.SlimeTrail)
            {
                slimeTimer -= Time.deltaTime;
                if (slimeTimer <= 0f)
                {
                    slimeTimer = Mathf.Max(0.25f, slimeDropInterval);
                    SpawnSlimeZone();
                }
            }
            else if (archetype == EnemyArchetype.Assassin)
            {
                if (PathProgress >= assassinTriggerProgress)
                {
                    Unit targetUnit = PickRandomAliveUnit();
                    if (targetUnit != null)
                    {
                        StartCoroutine(AssassinLungeRoutine(targetUnit));
                    }
                }
            }
            else if (archetype == EnemyArchetype.Boss)
            {
                bossSummonTimer -= Time.deltaTime;
                if (bossSummonTimer <= 0f)
                {
                    bossSummonTimer = Mathf.Max(1.5f, bossSummonInterval);
                    SpawnMinionsAtCurrentPosition(Mathf.Max(1, splitSpawnCount));
                }
            }
        }

        private void LateUpdate()
        {
            if (!isHpBarActive || hpBarRoot == null) return;
            UpdateHpBarScreenPosition();
        }

        private void UpdateHpBarScreenPosition()
        {
            if (hpBarRoot == null || hpBarRoot.panel == null) return;
            if (mainCam == null) mainCam = Camera.main;
            if (mainCam == null) return;

            Vector3 worldPos = transform.position + hpBarOffset;
            Vector2 panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(hpBarRoot.panel, worldPos, mainCam);
            hpBarRoot.style.left = panelPos.x - hpBarWidth * 0.5f;
            hpBarRoot.style.top = panelPos.y - hpBarHeight * 0.5f;
        }

        private void OnDestroy()
        {
            pathTween?.Kill();
            transform.DOKill();

            hpFillTween?.Kill();
            if (hpBarRoot != null)
            {
                hpBarRoot.RemoveFromHierarchy();
                hpBarRoot = null;
                hpBarFill = null;
            }

            if (EnemySpawner.Instance != null) EnemySpawner.Instance.RemoveEnemy(this);
        }
        #endregion

        #region Visual & UI
        private void ApplyTintToRenderers(Color color)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                var block = new MaterialPropertyBlock();
                renderers[i].GetPropertyBlock(block);
                block.SetColor(BaseColorId, color);
                block.SetColor(ColorId, color);
                renderers[i].SetPropertyBlock(block);
            }
        }

        private void InitHPBar()
        {
            if (hpBarRoot != null) return;
            VisualElement layer = PauseController.EnemyHpLayer;
            if (layer == null) return;

            bool isBoss = archetype == EnemyArchetype.Boss;
            hpBarWidth = isBoss ? 128f : 74f;
            hpBarHeight = isBoss ? 18f : 13f;

            hpBarRoot = new VisualElement { pickingMode = PickingMode.Ignore };
            hpBarRoot.style.position = Position.Absolute;
            hpBarRoot.style.width = hpBarWidth;
            hpBarRoot.style.height = hpBarHeight;
            hpBarRoot.style.backgroundColor = new Color(0.32f, 0.10f, 0.08f, 0.92f);
            hpBarRoot.style.borderTopWidth = hpBarRoot.style.borderBottomWidth = hpBarRoot.style.borderLeftWidth = hpBarRoot.style.borderRightWidth = 2.5f;
            Color borderCol = new Color(0.20f, 0.06f, 0.04f, 1f);
            hpBarRoot.style.borderTopColor = hpBarRoot.style.borderBottomColor = hpBarRoot.style.borderLeftColor = hpBarRoot.style.borderRightColor = borderCol;
            hpBarRoot.style.borderTopLeftRadius = hpBarRoot.style.borderTopRightRadius = hpBarRoot.style.borderBottomLeftRadius = hpBarRoot.style.borderBottomRightRadius = hpBarHeight * 0.5f;
            hpBarRoot.style.overflow = Overflow.Hidden;
            hpBarRoot.style.display = DisplayStyle.None;

            hpBarFill = new VisualElement { pickingMode = PickingMode.Ignore };
            hpBarFill.style.position = Position.Absolute;
            hpBarFill.style.left = 0;
            hpBarFill.style.top = 0;
            hpBarFill.style.bottom = 0;
            hpBarFill.style.width = Length.Percent(100f);
            hpBarFill.style.backgroundColor = isBoss
                ? new Color(0.62f, 0.18f, 0.85f, 1f)
                : new Color(0.92f, 0.25f, 0.22f, 1f);
            hpBarRoot.Add(hpBarFill);

            layer.Add(hpBarRoot);
            currentHpFillRatio = 1f;
            isHpBarActive = false;
        }
        #endregion

        #region Movement & Speed Modifiers
        public void Init(Vector3[] waypoints, float length, float hpMultiplier = 1f, float speedMultiplier = 1f, float initialProgress = 0f)
        {
            if (waypoints == null || waypoints.Length == 0) return;

            pathLength = Mathf.Max(0.01f, length);
            startProgressOffset = Mathf.Clamp01(initialProgress);
            TraveledDistance = startProgressOffset * pathLength;

            maxHP = Mathf.Max(0.1f, maxHP * Mathf.Max(0.1f, hpMultiplier));
            currentHP = maxHP;
            runtimeSpeed = Mathf.Max(0.1f, speed * Mathf.Max(0.1f, speedMultiplier));

            slimeTimer = 0.2f;
            bossSummonTimer = bossSummonInterval;

            if (bodyRenderer != null)
            {
                bodyRenderer.GetPropertyBlock(propBlock);
                propBlock.SetFloat(CurrentAnimIndexId, animIndex);
                propBlock.SetFloat(TransitionStartTimeId, -10f);
                bodyRenderer.SetPropertyBlock(propBlock);
            }

            float remainingLength = pathLength * (1f - startProgressOffset);
            pathTween = transform.DOPath(waypoints, runtimeSpeed, PathType.Linear)
                .SetSpeedBased(true)
                .SetEase(Ease.Linear)
                .SetLookAt(0.003f)
                .OnUpdate(() =>
                {
                    if (pathTween != null)
                        TraveledDistance = startProgressOffset * pathLength + pathTween.ElapsedPercentage() * remainingLength;
                })
                .OnComplete(() =>
                {
                    pathTween = null;
                    OnReachedGoal();
                });

            RecalculateSpeedTimeScale();
        }

        public void SetMoveSpeed(float slowMulti = 1.0f)
        {
            slowMultiplier = Mathf.Clamp(slowMulti, 0.1f, 3f);
            RecalculateSpeedTimeScale();
        }

        public void AddSlimeHaste(object source, float hasteMulti)
        {
            if (source == null) return;
            activeHasteSources[source] = Mathf.Max(1f, hasteMulti);
            RecalculateSpeedTimeScale();
        }

        public void RemoveSlimeHaste(object source)
        {
            if (source == null) return;
            if (activeHasteSources.Remove(source))
                RecalculateSpeedTimeScale();
        }

        private void RecalculateSpeedTimeScale()
        {
            if (!IsAlive || pathTween == null) return;

            float maxHaste = 1f;
            foreach (float val in activeHasteSources.Values)
            {
                if (val > maxHaste) maxHaste = val;
            }

            pathTween.timeScale = Mathf.Clamp(slowMultiplier * maxHaste, 0.1f, 4f);
        }

        private void OnReachedGoal()
        {
            if (isDead || hasReachedGoal || isAssassinLunging) return;
            hasReachedGoal = true;

            GameManager.Instance?.TakeDamage(leakDamage);
            Destroy(gameObject);
        }
        #endregion

        #region Archetype Abilities
        private void SpawnMinionsAtCurrentPosition(int count)
        {
            if (EnemySpawner.Instance == null || count <= 0) return;
            GameObject prefab = splitSpawnPrefab != null ? splitSpawnPrefab : EnemySpawner.Instance.GetFallbackEnemyPrefab();
            if (prefab == null) return;

            float baseProgress = PathProgress;
            for (int i = 0; i < count; i++)
            {
                float jitter = UnityEngine.Random.Range(-0.025f, 0.025f);
                float spawnProg = Mathf.Clamp(baseProgress + jitter, 0f, 0.96f);
                Enemy child = EnemySpawner.Instance.SpawnEnemyAtProgress(prefab, spawnProg, 0.85f, 1.05f);
                if (child != null)
                {
                    Vector3 targetScale = child.transform.localScale;
                    child.transform.localScale = Vector3.zero;
                    child.transform.DOScale(targetScale, 0.22f).SetEase(Ease.OutBack);
                }
            }
        }

        private Unit PickRandomAliveUnit()
        {
            if (MatchManager.Instance == null) return null;
            var units = MatchManager.Instance.SpawnedUnits;
            if (units == null || units.Count == 0) return null;

            List<Unit> candidates = new List<Unit>(units.Count);
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] != null && units[i].IsAlive)
                    candidates.Add(units[i]);
            }

            if (candidates.Count == 0) return null;
            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        private IEnumerator AssassinLungeRoutine(Unit targetUnit)
        {
            isAssassinLunging = true;
            pathTween?.Kill();
            pathTween = null;

            if (targetUnit != null)
            {
                Vector3 lookPos = targetUnit.transform.position;
                lookPos.y = transform.position.y;
                transform.DOLookAt(lookPos, 0.15f);
            }

            // Brief telegraph pulse so player & Pretzel sniper can react
            ApplyTintToRenderers(new Color(1f, 0.18f, 0.25f, 1f));
            transform.DOPunchScale(Vector3.one * 0.35f, assassinWarningDuration, 4, 0.5f);
            yield return new WaitForSeconds(assassinWarningDuration);

            if (isDead || !IsAlive) yield break;

            if (targetUnit != null && targetUnit.IsAlive)
            {
                Vector3 strikePos = targetUnit.transform.position;
                yield return transform.DOMove(strikePos, assassinLungeDuration)
                    .SetEase(Ease.InBack)
                    .WaitForCompletion();

                if (isDead || !IsAlive) yield break;

                if (targetUnit != null && targetUnit.IsAlive)
                    targetUnit.Attacked(9999f);
            }

            // Self-destruct after eliminating the unit
            isDead = true;
            if (hpBarRoot != null) hpBarRoot.style.display = DisplayStyle.None;
            isHpBarActive = false;
            transform.DOScale(0f, 0.2f).SetEase(Ease.InBack).OnComplete(() => Destroy(gameObject));
        }

        private void SpawnSlimeZone()
        {
            Vector3 dropPos = transform.position;
            dropPos.y += 0.03f;

            GameObject zoneObj;
            if (slimeZonePrefab != null)
            {
                zoneObj = Instantiate(slimeZonePrefab, dropPos, Quaternion.identity);
            }
            else
            {
                zoneObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                zoneObj.name = "SlimeZone";
                zoneObj.transform.position = dropPos;
                Collider col = zoneObj.GetComponent<Collider>();
                if (col != null) Destroy(col);
                zoneObj.AddComponent<SlimeZone>();
            }

            SlimeZone zone = zoneObj.GetComponent<SlimeZone>();
            zone?.Configure(slimeZoneRadius, slimeZoneDuration, slimeHasteMultiplier);
        }
        #endregion

        #region Damage & Death
        public override void Attacked(float damage)
        {
            if (!IsAlive) return;

            float hpRatio = Mathf.Clamp01((currentHP - damage) / maxHP);

            if (hpBarRoot == null)
                InitHPBar();

            if (!isHpBarActive && hpBarRoot != null)
            {
                isHpBarActive = true;
                hpBarRoot.style.display = DisplayStyle.Flex;
                UpdateHpBarScreenPosition();
            }

            if (hpBarFill != null)
            {
                hpFillTween?.Kill();
                hpFillTween = DOTween.To(
                    () => currentHpFillRatio,
                    v =>
                    {
                        currentHpFillRatio = v;
                        if (hpBarFill != null)
                            hpBarFill.style.width = Length.Percent(v * 100f);
                    },
                    hpRatio,
                    0.18f
                ).SetEase(Ease.OutQuad);
            }

            if (bodyRenderer != null)
            {
                bodyRenderer.transform.DOKill(true);
                bodyRenderer.transform.localScale = initialMeshScale * Mathf.Lerp(0.55f, 1.0f, hpRatio);
                bodyRenderer.transform.DOPunchScale(new Vector3(0.25f, -0.18f, 0.25f), 0.15f, 2, 0.5f);
            }

            base.Attacked(damage);
        }

        protected override void Die()
        {
            if (isDead) return;
            isDead = true;

            MatchManager.Instance?.AddTurn(1);
            SoundManager.Instance?.PlaySFX(SfxType.EnemyDie);

            pathTween?.Kill();
            pathTween = null;

            if (archetype == EnemyArchetype.Splitter)
                SpawnMinionsAtCurrentPosition(Mathf.Max(1, splitSpawnCount));

            if (hpBarRoot != null) hpBarRoot.style.display = DisplayStyle.None;
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

            transform.DOScale(0f, 0.45f).SetEase(Ease.InBack).OnComplete(() =>
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
        #endregion

        #region VAT Animation
        private float SetAnimation(State state)
        {
            int targetAnim = (int)state;
            if (animIndex == targetAnim || bodyRenderer == null) return 0f;

            animIndex = targetAnim;

            Material mat = bodyRenderer.sharedMaterial;
            if (mat == null || !mat.HasProperty(CurrentAnimIndexId))
                return 0f;

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
            int texID = index == 0 ? VatPosTex0Id : VatPosTex1Id;
            if (!mat.HasProperty(texID)) return 0f;

            float fps = mat.HasProperty(VatAnimFpsId) ? mat.GetFloat(VatAnimFpsId) : 15.0f;
            if (fps <= 0.001f) fps = 15.0f;

            Texture vatTex = mat.GetTexture(texID);
            return vatTex != null ? vatTex.height / fps : 0f;
        }
        #endregion
    }
}
