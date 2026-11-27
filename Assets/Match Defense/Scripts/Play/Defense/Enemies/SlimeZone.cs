using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace MatchDefense.Defense
{
    public class SlimeZone : MonoBehaviour
    {
        [Header("<color=yellow>Slime Zone</color>")]
        [SerializeField] private float radius = 1.25f;
        [SerializeField] private float duration = 5.5f;
        [SerializeField] private float hasteMultiplier = 1.6f;
        [SerializeField] private float checkInterval = 0.15f;
        [SerializeField] private Renderer puddleRenderer;
        [SerializeField] private Color slimeColor = new Color(0.95f, 0.86f, 0.16f, 0.85f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly HashSet<Enemy> _boostedEnemies = new HashSet<Enemy>();
        private readonly List<Enemy> _removeBuffer = new List<Enemy>();
        private float _checkTimer;
        private float _lifeTimer;
        private bool _isFadingOut;
        private Vector3 _targetScale;

        public void Configure(float customRadius, float customDuration, float customHaste)
        {
            if (customRadius > 0.1f) radius = customRadius;
            if (customDuration > 0.2f) duration = customDuration;
            if (customHaste > 1f) hasteMultiplier = customHaste;
        }

        private void Start()
        {
            _targetScale = new Vector3(radius * 1.8f, 0.06f, radius * 1.8f);
            transform.localScale = Vector3.zero;
            transform.DOScale(_targetScale, 0.25f).SetEase(Ease.OutBack);

            ApplySlimeColor();
        }

        private void ApplySlimeColor()
        {
            Renderer r = puddleRenderer != null ? puddleRenderer : GetComponentInChildren<Renderer>();
            if (r == null) return;
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, slimeColor);
            block.SetColor(ColorId, slimeColor);
            r.SetPropertyBlock(block);
        }

        private void Update()
        {
            _lifeTimer += Time.deltaTime;
            if (!_isFadingOut && _lifeTimer >= duration)
            {
                _isFadingOut = true;
                ClearAllBoosts();
                transform.DOScale(Vector3.zero, 0.35f)
                    .SetEase(Ease.InBack)
                    .OnComplete(() => Destroy(gameObject));
                return;
            }

            if (_isFadingOut) return;

            _checkTimer -= Time.deltaTime;
            if (_checkTimer <= 0f)
            {
                _checkTimer = checkInterval;
                RefreshEnemiesInZone();
            }
        }

        private void RefreshEnemiesInZone()
        {
            if (EnemySpawner.Instance == null) return;

            float sqrRadius = radius * radius;
            Vector3 center = transform.position;

            var activeEnemies = EnemySpawner.Instance.ActiveEnemies;
            for (int i = 0; i < activeEnemies.Count; i++)
            {
                Enemy enemy = activeEnemies[i];
                if (enemy == null || !enemy.IsAlive) continue;

                Vector3 diff = enemy.transform.position - center;
                diff.y = 0f;
                bool inside = diff.sqrMagnitude <= sqrRadius;

                if (inside)
                {
                    if (_boostedEnemies.Add(enemy))
                        enemy.AddSlimeHaste(this, hasteMultiplier);
                }
                else if (_boostedEnemies.Remove(enemy))
                {
                    enemy.RemoveSlimeHaste(this);
                }
            }

            _removeBuffer.Clear();
            foreach (Enemy e in _boostedEnemies)
            {
                if (e == null || !e.IsAlive)
                    _removeBuffer.Add(e);
            }
            for (int i = 0; i < _removeBuffer.Count; i++)
                _boostedEnemies.Remove(_removeBuffer[i]);
        }

        private void ClearAllBoosts()
        {
            foreach (Enemy enemy in _boostedEnemies)
            {
                if (enemy != null)
                    enemy.RemoveSlimeHaste(this);
            }
            _boostedEnemies.Clear();
        }

        private void OnDestroy()
        {
            transform.DOKill();
            ClearAllBoosts();
        }
    }
}
