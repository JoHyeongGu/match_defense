using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace MatchDefense.Defense.Units
{
    public class Butter : Unit
    {
        [Header("<color=yellow>Butter</color>")]
        [SerializeField] private float buffMultiplier = 0.3f;
        [SerializeField] private float buffDuration = 5f;
        [SerializeField] private Transform butter;

        private List<Unit> _targets = new();
        private Sequence _atkSeq;
        private const string BUFF_ID = "Butter_AtkSpeed";

        private void Awake()
        {
            attackInterval += buffDuration;
        }

        protected override bool TryAttack()
        {
            if (UnitBoard.Instance == null) return false;

            _targets.Clear();
            Vector2Int myPos = UnitBoard.Instance.WorldToCell(transform.position);

            foreach (Unit u in FindObjectsByType<Unit>())
            {
                if (u is StrawberryJam || u.activeBuffs.Contains(BUFF_ID)) continue;

                if (Vector2.Distance(myPos, UnitBoard.Instance.WorldToCell(u.transform.position)) < attackRange + 0.5f)
                    _targets.Add(u);
            }
            Attack();
            return true;
        }

        protected override void Attack()
        {
            _atkSeq?.Kill();
            _atkSeq = DOTween.Sequence();
            _atkSeq.Append(butter.DOScale(1f, 1f));
            _atkSeq.InsertCallback(0.5f, () =>
            {
                foreach (Unit t in _targets)
                {
                    if (t != null)
                    {
                        t.attackInterval *= buffMultiplier;
                        t.activeBuffs.Add(BUFF_ID);
                    }
                }
            });
            _atkSeq.InsertCallback(buffDuration + 0.5f, () =>
            {
                foreach (Unit t in _targets)
                {
                    if (t != null)
                    {
                        t.attackInterval /= buffMultiplier;
                        t.activeBuffs.Remove(BUFF_ID);
                    }
                }
            });
            _atkSeq.Insert(buffDuration, butter.DOScale(0f, 1f));
        }
    }
}