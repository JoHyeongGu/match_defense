using UnityEngine;
using DG.Tweening;
using System.Linq;
using System.Threading.Tasks;

namespace MatchDefense.Defense
{
    public class Enemy : MonoBehaviour
    {
        private enum State
        {
            Walk,
            Die
        }

        [Header("<color=yellow>Stats</color>")]
        [SerializeField] private float speed = 1f;
        [SerializeField] private int HP = 3;

        [Header("<color=yellow>VAT Animation</color>")]
        [SerializeField] private int animationIndex = 0;
        [SerializeField] private float transitionDuration = 0.1f;
        [SerializeField] private MeshRenderer meshRenderer;

        public bool IsAlive => HP > 0;
        public float TraveledDistance { get; private set; }

        private Tween pathTween;
        private Tween transitionTween;
        private MaterialPropertyBlock propBlock;
        private float _totalPathLength;

        private int internalCurrAnimIndex = 0;
        private float currentAnimOffset = 0f;

        private static readonly int[] VatPosTexIds = new int[]
        {
            Shader.PropertyToID("_VatPosTex0"),
            Shader.PropertyToID("_VatPosTex1"),
            Shader.PropertyToID("_VatPosTex2")
        };

        void Awake()
        {
            propBlock = new MaterialPropertyBlock();
            currentAnimOffset = Random.Range(0f, 5f);
        }

        private void OnDestroy()
        {
            pathTween?.Kill();
            transitionTween?.Kill();

            if (EnemySpawner.Instance != null)
                EnemySpawner.Instance.RemoveEnemy(this);
        }

        public void Init(Transform[] path, float pathLength)
        {
            if (path == null || path.Length == 0) return;

            _totalPathLength = pathLength;
            internalCurrAnimIndex = animationIndex;
            UpdateShaderProperties(internalCurrAnimIndex, internalCurrAnimIndex, 1.0f);

            Vector3[] waypoints = path.Select(p => p.position).ToArray();
            pathTween = transform.DOPath(waypoints, speed, PathType.Linear)
            .SetSpeedBased(true)
            .SetEase(Ease.Linear)
            .SetLookAt(0.003f)
            .OnUpdate(() => TraveledDistance = pathTween.ElapsedPercentage() * _totalPathLength)
            .OnComplete(() => Destroy(gameObject));
        }


        #region Damage

        public async Task Attacked(int damage)
        {
            if (!IsAlive) return;
            HP -= damage;

            if (HP > 0) return;
            pathTween?.Pause();
            await SetAnimation(State.Die);
            if (this != null) Destroy(gameObject);
        }

        #endregion


        #region VAT Animation

        private static readonly int PrevAnimIndexId = Shader.PropertyToID("_PrevAnimIndex");
        private static readonly int CurrentAnimIndexId = Shader.PropertyToID("_CurrentAnimIndex");
        private static readonly int TransitionProgressId = Shader.PropertyToID("_TransitionProgress");
        private static readonly int AnimOffsetId = Shader.PropertyToID("_AnimOffset");
        private static readonly int VatAnimFpsId = Shader.PropertyToID("_VatAnimFps");

        private async Task SetAnimation(State _state)
        {
            int _animIndex = (int)_state;
            if (internalCurrAnimIndex == _animIndex) return;

            int prevAnim = internalCurrAnimIndex;
            internalCurrAnimIndex = _animIndex;
            animationIndex = _animIndex;

            currentAnimOffset = -Time.time;

            transitionTween?.Kill();
            UpdateShaderProperties(prevAnim, internalCurrAnimIndex, 0f);

            transitionTween = DOVirtual.Float(0f, 1f, transitionDuration, progress =>
            {
                UpdateShaderProperties(prevAnim, internalCurrAnimIndex, progress);
            })
            .SetEase(Ease.Linear);

            float animDuration = GetAnimDuration(_animIndex);

            if (animDuration > 0f)
                await Task.Delay((int)(animDuration * 1000));
        }

        private float GetAnimDuration(int animIndex)
        {
            if (meshRenderer == null || meshRenderer.sharedMaterial == null) return 0f;
            if (animIndex < 0 || animIndex >= VatPosTexIds.Length) return 0f;

            Material mat = meshRenderer.sharedMaterial;
            float fps = mat.HasProperty(VatAnimFpsId) ? mat.GetFloat(VatAnimFpsId) : 15.0f;
            if (fps <= 0.001f) fps = 15.0f;
            Texture vatTex = mat.GetTexture(VatPosTexIds[animIndex]);
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
            propBlock.SetFloat(AnimOffsetId, currentAnimOffset);
            meshRenderer.SetPropertyBlock(propBlock);
        }

        #endregion
    }
}