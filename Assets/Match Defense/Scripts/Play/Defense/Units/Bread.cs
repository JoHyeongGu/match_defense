using UnityEngine;
using DG.Tweening;

namespace MatchDefense.Defense.Units
{
    public class Bread : Unit
    {
        [Header("<color=yellow>Bread</color>")]
        [SerializeField] private Transform weapon;

        protected override void Attack()
        {
            weapon.DOKill();
            weapon.localRotation = Quaternion.identity;

            var swing = DOTween.Sequence();
            swing.Append(weapon.DOLocalRotate(new Vector3(0f, 0f, -30f), 0.08f, RotateMode.Fast)
                .SetEase(Ease.OutCubic));
            swing.Append(weapon.DOLocalRotate(new Vector3(0f, 0f, 30f), 0.12f, RotateMode.Fast)
                .SetEase(Ease.InOutSine));
            swing.Append(weapon.DOLocalRotate(Vector3.zero, 0.06f, RotateMode.Fast)
                .SetEase(Ease.OutCubic));

            base.Attack();
        }
    }
}