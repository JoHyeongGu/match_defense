using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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

        public async void AddBuff(string key, Action action, float duration = 0f, Action release = null)
        {
            if (!IsAlive || HasBuff(key)) return;

            activeBuffs.Add(key);
            action?.Invoke();
            await Task.Delay((int)(duration * 1000));

            if (this == null) return;
            release?.Invoke();
            activeBuffs.Remove(key);
        }
        #endregion
    }
}