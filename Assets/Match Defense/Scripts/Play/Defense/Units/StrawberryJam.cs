using UnityEngine;
using DG.Tweening;

namespace MatchDefense.Defense.Units
{
    public class StrawberryJam : Unit
    {
        [Header("<color=yellow>Strawberry Jam</color>")]
        [SerializeField] private float buffValue = 0.3f;
        [SerializeField] private float buffDuration = 2f;
        [SerializeField] private GameObject bulletPrefab;
        private const string buffKey = "slow";

        protected override bool IsInRange(Character character)
        {
            return base.IsInRange(character) && !character.HasBuff(buffKey);
        }

        protected override void Attack(Character target)
        {
            if (target is Enemy enemyTarget)
            {
                ShootBullet(enemyTarget);
            }
        }

        private void ShootBullet(Enemy target)
        {
            float arcHeight = 1.0f;
            float flightTime = 0.5f;

            Vector3 predictedPos = target.transform.position + (target.transform.forward * target.CurrentSpeed * flightTime);
            GameObject bullet = Instantiate(bulletPrefab, transform.position, Quaternion.identity);

            Destroy(bullet, 2f);
            bullet.transform.localScale = Vector3.zero;

            Sequence scaleSeq = DOTween.Sequence().SetLink(bullet);
            scaleSeq.Append(bullet.transform.DOScale(new Vector3(0.6f, 1.2f, 0.6f), flightTime * 0.5f).SetEase(Ease.OutQuad));
            scaleSeq.Append(bullet.transform.DOScale(new Vector3(0.7f, 0.7f, 0.7f), flightTime * 0.5f).SetEase(Ease.InQuad));

            bullet.transform.DOJump(predictedPos, arcHeight, 1, flightTime)
                .SetEase(Ease.OutQuart)
                .SetLink(bullet)
                .OnComplete(() =>
                {
                    scaleSeq.Kill();
                    Sequence hitSeq = DOTween.Sequence().SetLink(bullet);
                    hitSeq.Append(bullet.transform.DOScale(new Vector3(0.8f, 0.1f, 0.8f), 0.1f).SetEase(Ease.OutBack));
                    hitSeq.Append(bullet.transform.DOScale(Vector3.zero, 1f).SetEase(Ease.InQuad));
                    hitSeq.OnComplete(() => Destroy(bullet));

                    if (target == null || !target.IsAlive) return;

                    target.AddBuff(
                        buffKey,
                        action: () => target.SetMoveSpeed(buffValue),
                        duration: buffDuration,
                        release: () => target.SetMoveSpeed()
                    );
                });
        }
    }
}