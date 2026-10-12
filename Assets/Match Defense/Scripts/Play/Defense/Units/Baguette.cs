using UnityEngine;
using DG.Tweening;

namespace MatchDefense.Defense.Units
{
    public class Baguette : Unit
    {
        [Header("<color=yellow>Baguette</color>")]
        [SerializeField] private Transform body;

        protected override void Attack(Character target)
        {
            if (body != null)
            {
                body.DOKill();

                var originalScale = body.localScale;
                var originalRotation = body.localRotation;
                var extendedScale = new Vector3(originalScale.x, originalScale.y * 3f, originalScale.z);

                body.DOScale(extendedScale, 0.3f)
                    .SetEase(Ease.OutCubic);

                body.DOLocalRotate(new Vector3(-25f, 0f, 0f), 0.3f, RotateMode.LocalAxisAdd)
                    .SetEase(Ease.OutCubic)
                    .OnComplete(() =>
                    {
                        body.DOLocalRotate(new Vector3(125f, 0f, 0f), 0.3f, RotateMode.LocalAxisAdd)
                            .SetEase(Ease.InBack)
                            .OnComplete(() =>
                            {
                                base.Attack(target);

                                body.DOScale(originalScale, 0.25f)
                                    .SetEase(Ease.InOutCubic);

                                body.DOLocalRotateQuaternion(originalRotation, 0.25f)
                                    .SetEase(Ease.InOutCubic);
                            });
                    });
            }
            else
            {
                base.Attack(target);
            }
        }
    }
}