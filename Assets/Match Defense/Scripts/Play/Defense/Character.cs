using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace MatchDefense.Defense
{
    public abstract class Character : MonoBehaviour
    {
        [Header("<color=yellow>Stats</color>")]
        [SerializeField] protected float maxHP = 3f;
        protected float currentHP;
        public bool IsAlive => currentHP > 0;

        protected virtual void Awake()
        {
            currentHP = maxHP;
        }

        public virtual void Attacked(float damage)
        {
            if (!IsAlive) return;
            currentHP -= damage;
            if (currentHP <= 0) Die();
        }

        protected abstract void Die();

        #region Buff
        private List<string> activeBuffs = new();

        public bool HasBuff(string key) => activeBuffs.Contains(key);

        public void AddBuff(string key, Action action, float duration = 0f, Action release = null)
        {
            if (!IsAlive || HasBuff(key)) return;
            StartCoroutine(BuffRoutine(key, action, duration, release));
        }

        private IEnumerator BuffRoutine(string key, Action action, float duration, Action release)
        {
            activeBuffs.Add(key);
            action?.Invoke();

            yield return new WaitForSeconds(duration);

            if (this == null) yield break;

            release?.Invoke();
            activeBuffs.Remove(key);
        }
        #endregion
    }
}