// Unit.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;
using UnityEngine.EventSystems;

namespace MatchDefense.Defense
{
    public class Unit : MonoBehaviour
    {
        public enum AttackType
        {
            SingleTarget,
            MultiTarget,
            AreaOfEffect
        }

        [Header("Stats")]
        [SerializeField] protected int damage = 1;
        [SerializeField] protected float attackInterval = 1f;
        [SerializeField] protected float attackRange = 1f;

        [Header("Attack")]
        [SerializeField] protected AttackType attackType = AttackType.SingleTarget;
        [SerializeField] protected int maxTargets = 3;

        public float AttackRange => attackRange;
        public int Damage => damage;

        protected readonly List<Enemy> targets = new List<Enemy>();
        protected float cooldown;
        private Vector2Int gridPos;

        protected virtual void Start()
        {
            if (UnitBoard.Instance != null)
            {
                gridPos = UnitBoard.Instance.WorldToCell(transform.position);
            }
        }

        protected virtual void Update()
        {
            cooldown -= Time.deltaTime;
            if (cooldown <= 0f && TryAttack())
            {
                cooldown = attackInterval;
            }

            HandleSelection();
        }

        #region Selection
        private void HandleSelection()
        {
            if (Pointer.current == null || !Pointer.current.press.wasPressedThisFrame) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (Camera.main == null) return;

            Ray ray = Camera.main.ScreenPointToRay(Pointer.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                {
                    if (UnitBoard.Instance != null)
                    {
                        UnitBoard.Instance.ShowUnitRange(transform.position, attackRange);
                    }
                }
                else
                {
                    Unit clickedUnit = hit.transform.GetComponentInParent<Unit>();
                    if (clickedUnit == null && UnitBoard.Instance != null && !UnitSpawnManager.IsDraggingUnit)
                    {
                        UnitBoard.Instance.ClearHover();
                        UnitBoard.Instance.ToggleGridVisibility(false);
                    }
                }
            }
            else if (UnitBoard.Instance != null && !UnitSpawnManager.IsDraggingUnit)
            {
                UnitBoard.Instance.ClearHover();
                UnitBoard.Instance.ToggleGridVisibility(false);
            }
        }
        #endregion

        #region Combat
        protected bool IsInRange(Enemy enemy)
        {
            if (enemy == null || UnitBoard.Instance == null) return false;

            Vector2Int enemyPos = UnitBoard.Instance.WorldToCell(enemy.transform.position);
            float tileDistance = Vector2.Distance(gridPos, enemyPos);

            return tileDistance < (attackRange + 0.5f);
        }

        protected virtual bool TryAttack()
        {
            if (EnemySpawner.Instance == null) return false;

            targets.Clear();

            foreach (var enemy in EnemySpawner.Instance.ActiveEnemies)
            {
                if (enemy != null && enemy.IsAlive && IsInRange(enemy))
                {
                    targets.Add(enemy);
                }
            }

            if (targets.Count == 0) return false;

            targets.Sort((a, b) => b.TraveledDistance.CompareTo(a.TraveledDistance));

            RotateTowardsTarget(targets[0]);
            ExecuteAttack();

            return true;
        }

        protected virtual void RotateTowardsTarget(Enemy target)
        {
            if (target == null) return;

            Vector3 targetPos = target.transform.position;
            targetPos.y = transform.position.y;

            transform.DOLookAt(targetPos, 0.3f).SetEase(Ease.OutQuad);
        }

        protected virtual void ExecuteAttack()
        {
            switch (attackType)
            {
                case AttackType.SingleTarget:
                    targets[0].Attacked(damage);
                    break;

                case AttackType.MultiTarget:
                    int count = Mathf.Min(targets.Count, maxTargets);
                    for (int i = 0; i < count; i++)
                    {
                        targets[i].Attacked(damage);
                    }
                    break;

                case AttackType.AreaOfEffect:
                    foreach (var target in targets)
                    {
                        target.Attacked(damage);
                    }
                    break;
            }
        }
        #endregion
    }
}