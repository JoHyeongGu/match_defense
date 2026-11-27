using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using MatchDefense.Match;

namespace MatchDefense.Defense
{
    public class Unit : Character
    {
        public enum TargetType { Enemy, Ally, Both }

        [Header("<color=yellow>Attack</color>")]
        public float damage = 1f;
        public float cooldown = 1f;
        public float range = 1f;
        [SerializeField] protected TargetType targetType = TargetType.Enemy;
        [SerializeField] protected int atkCount = 1;
        [SerializeField] protected float atkInterval = 0f;
        [Tooltip("Atk Target Count < 0 : Targeting All Characters")]
        [SerializeField] protected int atkTargetCount = 1;

        protected readonly List<Character> targets = new();
        protected float _timer;
        public Vector2Int GridPos { get; private set; }
        public int UnitLevel { get; private set; } = 1;

        protected override void Awake()
        {
            base.Awake();
        }

        private void Start()
        {
            if (UnitBoard.Instance != null)
                GridPos = UnitBoard.Instance.WorldToCell(transform.position);

            if (MatchManager.Instance != null)
                MatchManager.Instance.AddSpawnedUnit(this);
        }

        protected virtual void OnDestroy()
        {
            if (MatchManager.Instance != null)
                MatchManager.Instance.RemoveSpawnedUnit(this);
        }

        public virtual void ApplyUpgradeStats(int level, float bonusDamage, float cooldownMultiplier)
        {
            UnitLevel = Mathf.Max(1, level);
            damage = Mathf.Max(0.1f, damage + Mathf.Max(0f, bonusDamage));
            cooldown = Mathf.Max(0.15f, cooldown * Mathf.Clamp(cooldownMultiplier, 0.3f, 1f));
        }

        private void Update()
        {
            HandleSelection();

            _timer -= Time.deltaTime;

            if (_timer <= 0f && FindTargets())
            {
                _timer = cooldown;
                ExecuteAttack();
            }
        }

        protected override void Die()
        {
            transform.DOKill();
            Destroy(gameObject);
        }

        #region Utils
        protected virtual bool IsInRange(Character character)
        {
            if (character == null || UnitBoard.Instance == null) return false;
            return Vector2.Distance(GridPos, UnitBoard.Instance.WorldToCell(character.transform.position)) < (range + 0.5f);
        }

        private void LookAt(Character target)
        {
            if (target == null) return;
            Vector3 pos = target.transform.position;
            pos.y = transform.position.y;
            transform.DOLookAt(pos, 0.3f).SetEase(Ease.OutQuad);
        }

        private void HandleSelection()
        {
            if (Pointer.current == null || !Pointer.current.press.wasPressedThisFrame) return;
            if (EventSystem.current?.IsPointerOverGameObject() == true || Camera.main == null) return;

            Ray ray = Camera.main.ScreenPointToRay(Pointer.current.position.ReadValue());
            bool isHit = Physics.Raycast(ray, out RaycastHit hit);
            bool isSelf = isHit && (hit.transform == transform || hit.transform.IsChildOf(transform));

            if (isSelf)
            {
                UnitBoard.Instance?.ShowUnitRange(transform.position, range);
            }
            else if (!MatchManager.IsDraggingUnit)
            {
                Unit clickedUnit = isHit ? hit.transform.GetComponentInParent<Unit>() : null;
                if (clickedUnit == null)
                {
                    UnitBoard.Instance?.ClearHover();
                    UnitBoard.Instance?.ToggleGridVisibility(false);
                }
            }
        }
        #endregion

        #region Attack
        protected virtual void Attack(Character target) => target.Attacked(damage);

        private void ExecuteAttack()
        {
            StartCoroutine(AttackRoutine());
        }

        private IEnumerator AttackRoutine()
        {
            for (int i = 0; i < atkCount; i++)
            {
                if (this == null) yield break;

                if (i > 0 && atkInterval > 0f)
                {
                    if (!FindTargets()) break;
                }

                foreach (var target in targets)
                {
                    if (target == null || !target.IsAlive) continue;
                    Attack(target);
                }

                if (atkInterval > 0f && i < atkCount - 1)
                    yield return new WaitForSeconds(atkInterval);
            }
        }

        protected virtual bool FindTargets()
        {
            targets.Clear();

            if (targetType == TargetType.Enemy || targetType == TargetType.Both)
            {
                if (EnemySpawner.Instance != null)
                {
                    foreach (var enemy in EnemySpawner.Instance.ActiveEnemies)
                    {
                        if (enemy != null && enemy.IsAlive && IsInRange(enemy))
                            targets.Add(enemy);
                    }
                }
            }

            if (targetType == TargetType.Ally || targetType == TargetType.Both)
            {
                if (MatchManager.Instance != null)
                {
                    foreach (var unit in MatchManager.Instance.SpawnedUnits)
                    {
                        if (unit != null && unit != this && unit.IsAlive && IsInRange(unit))
                            targets.Add(unit);
                    }
                }
            }

            if (targets.Count == 0) return false;

            targets.Sort((a, b) =>
            {
                if (a is Enemy enemyA && b is Enemy enemyB)
                    return enemyB.TraveledDistance.CompareTo(enemyA.TraveledDistance);

                return Vector3.Distance(transform.position, a.transform.position)
                    .CompareTo(Vector3.Distance(transform.position, b.transform.position));
            });

            if (atkTargetCount >= 0 && targets.Count > atkTargetCount)
                targets.RemoveRange(atkTargetCount, targets.Count - atkTargetCount);

            return true;
        }
        #endregion
    }
}
