using UnityEngine;
using DG.Tweening;

namespace MatchDefense.Defense.Units
{
    public class Butter : Unit
    {
        [Header("<color=yellow>Butter</color>")]
        [SerializeField] private float atkSpeedBuff = 0.5f;
        [SerializeField] private float atkDamageBuff = 0.5f;
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
                    action: () => InsertBuff(target as Unit),
                    duration: buffDuration,
                    release: () => RemoveBuff(target as Unit)
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
            target.cooldown *= atkSpeedBuff;
            target.damage *= atkDamageBuff;
        }

        private void RemoveBuff(Unit target)
        {
            target.cooldown /= atkSpeedBuff;
            target.damage /= atkDamageBuff;
        }
    }
}