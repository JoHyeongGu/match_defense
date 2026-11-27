using UnityEngine;
using DG.Tweening;

namespace MatchDefense.Defense.Units
{
    public class Butter : Unit
    {
        [Header("<color=yellow>Butter</color>")]
        [SerializeField] private float atkSpeedBuff = 0.65f;
        [SerializeField] private float atkDamageBuff = 1.35f;
        [SerializeField] private float buffDuration = 5f;
        [SerializeField] private Transform butter;

        private const string buffKey = "atkSpeed";

        protected override void Awake()
        {
            base.Awake();
            cooldown += buffDuration;
        }

        protected override bool IsInRange(Character character)
        {
            return base.IsInRange(character) && !character.HasBuff(buffKey) && !(character is Butter);
        }

        protected override void Attack(Character target)
        {
            if (target is Unit unitTarget)
            {
                unitTarget.AddBuff(
                    buffKey,
                    action: () => InsertBuff(unitTarget),
                    duration: buffDuration,
                    release: () => RemoveBuff(unitTarget)
                );
            }

            if (butter != null && !DOTween.IsTweening(butter))
            {
                butter.DOKill();
                Sequence buffSeq = DOTween.Sequence();
                buffSeq.Append(butter.DOScale(1f, 1f));
                buffSeq.AppendInterval(0.3f);
                buffSeq.Append(butter.DOScale(Vector3.zero, 2f).SetEase(Ease.InQuad));
            }
        }

        private void InsertBuff(Unit target)
        {
            if (target == null) return;
            target.cooldown *= Mathf.Clamp(atkSpeedBuff, 0.25f, 1f);
            target.damage *= Mathf.Max(1f, atkDamageBuff);
        }

        private void RemoveBuff(Unit target)
        {
            if (target == null) return;
            target.cooldown /= Mathf.Clamp(atkSpeedBuff, 0.25f, 1f);
            target.damage /= Mathf.Max(1f, atkDamageBuff);
        }
    }
}
