using UnityEngine;
using DG.Tweening;

namespace MatchDefense.Defense.Units
{
    public class Pretzel : Unit
    {
        [Header("<color=yellow>Pretzel</color>")]
        [SerializeField] private GameObject bulletPrefab;

        protected override void Attack(Character target)
        {
            if (target is Enemy enemyTarget)
            {
                ShootBullet(enemyTarget);
            }
        }

        private void ShootBullet(Enemy target)
        {
            float flightTime = 0.3f;

            Vector3 predictedPos = target.transform.position + (target.transform.forward * target.CurrentSpeed * flightTime);
            GameObject bullet = Instantiate(bulletPrefab, transform.position, Quaternion.identity);

            bullet.transform.localScale = Vector3.one * 0.3f;

            bullet.transform.DOMove(predictedPos, flightTime)
            .SetEase(Ease.Linear)
            .SetLink(bullet)
            .OnComplete(() =>
            {
                if (target != null && target.IsAlive)
                    base.Attack(target);

                Destroy(bullet);
            });
        }
    }
}