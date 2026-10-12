using UnityEngine;
using DG.Tweening;

namespace MatchDefense.Defense.Units
{
    public class Soboro : Unit
    {
        [Header("<color=yellow>Soboro</color>")]
        [SerializeField] private Transform body;

        protected override void Attack(Character target)
        {
            if (!DOTween.IsTweening(body))
            {
                Vector3 originPosition = body.localPosition;
                float duration = atkCount * atkInterval;
                Sequence seq = DOTween.Sequence();
                seq.Append(body.DORotate(new Vector3(0f, 360f, 0f), duration, RotateMode.LocalAxisAdd)
                    .SetRelative(true)
                    .SetEase(Ease.Linear));
                seq.Join(body.DOShakeRotation(duration, new Vector3(0f, 0f, 14f), 40, 90f, true));
                seq.OnUpdate(() => body.localPosition = originPosition);
                seq.OnComplete(() => body.localPosition = originPosition);
                seq.Play();
            }
            base.Attack(target);
        }
    }
}