using UnityEngine;
using DG.Tweening;

namespace MatchDefense.Defense.Units
{
    public class Bread : Unit
    {
        [Header("<color=yellow>Bread</color>")]
        [SerializeField] private Animator animator;

        protected override void Attack(Character target)
        {
            if (animator != null)
            {
                animator.SetTrigger("Attack");
            }
            DOVirtual.DelayedCall(0.3f, () =>
            {
                if (this != null && target != null && target.IsAlive)
                {
                    base.Attack(target);
                }
            });
        }
    }
}