using UnityEngine;
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
        [SerializeField] private float transitionDuration = 0.1f;
        [SerializeField] private MeshRenderer meshRenderer;

        public float CurrentSpeed => pathTween != null ? speed * pathTween.timeScale : speed;
        public float TraveledDistance { get; private set; }

        private Tween pathTween;
        private Tween transitionTween;
        private MaterialPropertyBlock propBlock;
        private float pathLength;
        private float animOffset = 0f;
        private Vector3 initialMeshScale;

        private static readonly int[] VatPosTexIds = new int[]
        {
            Shader.PropertyToID("_VatPosTex0"),
            Shader.PropertyToID("_VatPosTex1"),
            Shader.PropertyToID("_VatPosTex2")
        };

        private static readonly int PrevAnimIndexId = Shader.PropertyToID("_PrevAnimIndex");
        private static readonly int CurrentAnimIndexId = Shader.PropertyToID("_CurrentAnimIndex");
        private static readonly int TransitionProgressId = Shader.PropertyToID("_TransitionProgress");
        private static readonly int AnimOffsetId = Shader.PropertyToID("_AnimOffset");
        private static readonly int VatAnimFpsId = Shader.PropertyToID("_VatAnimFps");

        protected override void Awake()
        {
            base.Awake();

            propBlock = new MaterialPropertyBlock();
            animOffset = UnityEngine.Random.Range(0f, 5f);

            if (meshRenderer != null)
                initialMeshScale = meshRenderer.transform.localScale;
        }

        private void OnDestroy()
        {
            pathTween?.Kill();
            transitionTween?.Kill();
            if (EnemySpawner.Instance != null) EnemySpawner.Instance.RemoveEnemy(this);
        }

        #region State
        public void Init(Vector3[] waypoints, float length)
        {
            if (waypoints == null || waypoints.Length == 0) return;

            pathLength = length;
            UpdateShaderProperties(animIndex, animIndex, 1.0f);

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
            if (meshRenderer != null)
            {
                meshRenderer.transform.DOKill(true);
                meshRenderer.transform.localScale = initialMeshScale * Mathf.Lerp(0.4f, 1.0f, hpRatio);
                meshRenderer.transform.DOPunchScale(new Vector3(0.3f, -0.2f, 0.3f), 0.15f, 2, 0.5f);
            }

            base.Attacked(damage);
        }

        protected override void Die()
        {
            pathTween?.Pause();
            _ = SetAnimation(State.Die);
            transform.DOScale(0f, 0.3f).SetEase(Ease.InBack).OnComplete(() =>
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
            if (animIndex == targetAnim) return;

            int prevAnim = animIndex;
            animIndex = targetAnim;
            animOffset = -Time.time;

            transitionTween?.Kill();
            UpdateShaderProperties(prevAnim, animIndex, 0f);

            transitionTween = DOVirtual.Float(0f, 1f, transitionDuration, progress =>
            {
                UpdateShaderProperties(prevAnim, animIndex, progress);
            })
            .SetEase(Ease.Linear)
            .OnComplete(() => transitionTween = null);

            float duration = GetAnimDuration(targetAnim);

            if (duration > 0f) await Task.Delay((int)(duration * 1000));
        }

        private float GetAnimDuration(int index)
        {
            if (meshRenderer == null || meshRenderer.sharedMaterial == null) return 0f;
            if (index < 0 || index >= VatPosTexIds.Length) return 0f;

            Material mat = meshRenderer.sharedMaterial;
            float fps = mat.HasProperty(VatAnimFpsId) ? mat.GetFloat(VatAnimFpsId) : 15.0f;
            if (fps <= 0.001f) fps = 15.0f;

            Texture vatTex = mat.GetTexture(VatPosTexIds[index]);
            if (vatTex == null) return 0f;

            return vatTex.height / fps;
        }

        private void UpdateShaderProperties(int prev, int curr, float progress)
        {
            if (meshRenderer == null) return;

            meshRenderer.GetPropertyBlock(propBlock);
            propBlock.SetFloat(PrevAnimIndexId, prev);
            propBlock.SetFloat(CurrentAnimIndexId, curr);
            propBlock.SetFloat(TransitionProgressId, progress);
            propBlock.SetFloat(AnimOffsetId, animOffset);
            meshRenderer.SetPropertyBlock(propBlock);
        }
        #endregion
    }
}