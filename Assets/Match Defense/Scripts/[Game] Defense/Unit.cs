using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace MatchDefense.Defense
{
    public class Unit : MonoBehaviour
    {
        public enum AttackType
        {
            SingleTarget,
            MultiTarget,
            AreaOfEffect
        }

        [Header("<color=yellow>Stats</color>")]
        [SerializeField] protected int damage = 1;
        [SerializeField] protected float attackInterval = 1f;
        [SerializeField] protected float attackRange = 1f;

        [Header("<color=yellow>Attack</color>")]
        [SerializeField] protected AttackType attackType = AttackType.SingleTarget;
        [SerializeField] protected int maxTargets = 3;

        public int Damage => damage;

        protected readonly List<PathInterval> _attackableIntervals = new List<PathInterval>();
        protected readonly List<Enemy> _targetsInRange = new List<Enemy>();
        protected float _attackCooldown;

        protected virtual void Start()
        {
            CalculateAttackRanges();
        }

        protected virtual void Update()
        {
            _attackCooldown -= Time.deltaTime;
            if (_attackCooldown <= 0f && TryAttack())
            {
                _attackCooldown = attackInterval;
            }
        }


        #region Init

        protected void CalculateAttackRanges()
        {
            _attackableIntervals.Clear();
            if (EnemySpawner.Instance?.PathWaypoints == null) return;

            Vector3[] waypoints = EnemySpawner.Instance.PathWaypoints;
            float currentPathDist = 0f;
            bool isInside = false;
            float startDist = 0f;

            for (int i = 0; i < waypoints.Length - 1; i++)
            {
                Vector3 start = waypoints[i];
                Vector3 end = waypoints[i + 1];
                float segmentLen = Vector3.Distance(start, end);
                int steps = Mathf.Max(1, Mathf.CeilToInt(segmentLen / 0.2f));

                for (int s = 0; s <= steps; s++)
                {
                    float t = (float)s / steps;
                    Vector3 point = Vector3.Lerp(start, end, t);
                    float pointPathDist = currentPathDist + (segmentLen * t);

                    if (Vector3.Distance(transform.position, point) <= attackRange)
                    {
                        if (!isInside)
                        {
                            isInside = true;
                            startDist = pointPathDist;
                        }
                    }
                    else if (isInside)
                    {
                        isInside = false;
                        _attackableIntervals.Add(new PathInterval(startDist, pointPathDist));
                    }
                }
                currentPathDist += segmentLen;
            }

            if (isInside)
            {
                _attackableIntervals.Add(new PathInterval(startDist, currentPathDist));
            }
        }

        #endregion


        #region Attack

        protected bool IsInRange(float dist)
        {
            foreach (var interval in _attackableIntervals)
            {
                if (dist >= interval.Min && dist <= interval.Max) return true;
            }
            return false;
        }

        protected virtual bool TryAttack()
        {
            if (EnemySpawner.Instance == null) return false;

            _targetsInRange.Clear();

            foreach (var enemy in EnemySpawner.Instance.ActiveEnemies)
            {
                if (enemy != null && enemy.IsAlive && IsInRange(enemy.TraveledDistance))
                {
                    _targetsInRange.Add(enemy);
                }
            }

            if (_targetsInRange.Count == 0) return false;

            _targetsInRange.Sort((a, b) => b.TraveledDistance.CompareTo(a.TraveledDistance));

            RotateTowardsTarget(_targetsInRange[0]);
            ExecuteAttack();

            return true;
        }

        protected virtual void RotateTowardsTarget(Enemy target)
        {
            if (target == null) return;

            Vector3 targetPos = target.transform.position;
            targetPos.y = transform.position.y;

            transform.DOLookAt(targetPos, 0.3f).SetEase(Ease.OutQuad);
        }

        protected virtual void ExecuteAttack()
        {
            switch (attackType)
            {
                case AttackType.SingleTarget:
                    _ = _targetsInRange[0].Attacked(damage);
                    break;

                case AttackType.MultiTarget:
                    int count = Mathf.Min(_targetsInRange.Count, maxTargets);
                    for (int i = 0; i < count; i++)
                    {
                        _ = _targetsInRange[i].Attacked(damage);
                    }
                    break;

                case AttackType.AreaOfEffect:
                    foreach (var target in _targetsInRange)
                    {
                        _ = target.Attacked(damage);
                    }
                    break;
            }
        }

        #endregion

        #region Gizmos

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
#endif

        #endregion
    }

    #region Structs

    public readonly struct PathInterval
    {
        public readonly float Min;
        public readonly float Max;

        public PathInterval(float min, float max)
        {
            Min = min;
            Max = max;
        }
    }

    #endregion
}