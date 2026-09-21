using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace MatchDefense.Defense.Units
{
    public class StrawberryJam : Unit
    {
        [Header("<color=yellow>Strawberry Jam</color>")]
        [SerializeField] private float buffDuration = 2f;
        [SerializeField] private Transform jam;

        private Sequence _atkSeq;
        private const string BUFF_ID = "StrawberryJam_Slow";

        private void Awake()
        {
            attackInterval += buffDuration;
        }

        protected override bool TryAttack()
        {
            Attack();
            return true;
        }

        protected override void Attack()
        {
            _atkSeq?.Kill();
            _atkSeq = DOTween.Sequence();
            _atkSeq.Append(jam.DOScale(1f, 1f));
            _atkSeq.InsertCallback(0.5f, () =>
            {
                if (UnitBoard.Instance == null || EnemySpawner.Instance == null) return;

                Vector2Int myPos = UnitBoard.Instance.WorldToCell(transform.position);

                foreach (Enemy enemy in EnemySpawner.Instance.ActiveEnemies)
                {
                    if (enemy == null || !enemy.IsAlive) continue;

                    Vector2Int enemyPos = UnitBoard.Instance.WorldToCell(enemy.transform.position);
                    if (Vector2.Distance(myPos, enemyPos) < (attackRange + 0.5f))
                    {
                        if (!enemy.activeBuffs.Contains(BUFF_ID))
                        {
                            enemy.activeBuffs.Add(BUFF_ID);
                        }
                    }
                }
            });
            _atkSeq.InsertCallback(buffDuration + 0.5f, () =>
            {
                if (EnemySpawner.Instance == null) return;

                foreach (Enemy enemy in EnemySpawner.Instance.ActiveEnemies)
                {
                    if (enemy != null)
                    {
                        enemy.activeBuffs.Remove(BUFF_ID);
                    }
                }
            });
            _atkSeq.Insert(buffDuration, jam.DOScale(0f, 1f));
        }
    }
}