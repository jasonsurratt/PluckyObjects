using Mirror;
using Plucky.Common;
using Plucky.UnityExtensions;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace knockback
{
    public class HeathChangeEvent : UnityEvent<float>
    {
    }

    /// Health represents the health of a character or object. All changes take place exclusively
    /// on the server, but stay in sync on all clients.
    public class Health : NetworkBehaviour
    {
        public delegate void DamageTaken(Health health, float damage, uint source);
        public delegate void DeltaSummary(Health health, float diff);

        static readonly ILogger debug = null; // GameManager.debug;

        [SyncVar(hook = "OnHealthChange")]
        public float health;

        // used in conjunction with deltaSummary
        private float lastDeltaHealth = -1;
        private float lastDeltaTime;

        static readonly ILogger log = Debug.unityLogger;

        [SyncVar]
        public float maxHealth;

        [Tooltip("Invoked whenever the health changes with the difference in health.")]
        public HeathChangeEvent changeEvent;

        [Tooltip("Invoked on all clients when the object takes damage.")]
        public UnityEvent damageEvent;

        [Tooltip("Invoked on all clients when the object dies.")]
        public UnityEvent deathEvent;

        public static event DamageTaken damageTaken;
        /// deathEmitted is true if the death events have been emitted. If the health goes above
        /// zero this is reset to false.
        bool deathEmitted = false;
        public static event Action<Health, uint> deathEventStatic;
        public const float deltaFrequency = 0.25f;
        public static DeltaSummary deltaSummary;

        [Tooltip("Invoked on the server when the object takes damage.")]
        public UnityEvent serverDamageEvent;

        public GameObject lastAttacker
        {
            get
            {
                if (NetworkIdentity.spawned.TryGetValue(lastAttackerNid, out NetworkIdentity nid) &&
                    nid)
                {
                    return nid.gameObject;
                }
                return null;
            }
        }
        public uint lastAttackerNid = 0;

        public float tookDamageLast;

        IEntityStats _stats;

        public void ApplyDamage(float pain)
        {
            ApplyDamage(pain, 0);
        }

        /// ApplyDamage applies the specified damage to the health. This is only effective on the
        /// server.
        public void ApplyDamage(float pain, uint source)
        {
            debug?.Log($"ApplyDamage {pain} {source}");
            lastAttackerNid = source;
            if (isServer)
            {
                // when small/large pain scales with size.
                if (_stats != null) pain = pain / _stats.GetFloat(EntityStatType.Scale);

                SetHealth(health - pain);
                if (pain > 0)
                {
                    debug?.Log($"Calling RpcDamageTaken {health} {pain} {source}");
                    RpcDamageTaken(pain, source);
                }
            }
        }

        /// ApplyHealing does the inverse of ApplyDamage.
        public void ApplyHealing(float health)
        {
            ApplyDamage(-health);
        }

        public void Awake()
        {
            _stats = GetComponent<IEntityStats>();
        }

        [Command]
        public void CmdKill() => Kill();

        /// Implement IDamageable for Behaviour Trees.
        public void Damage(float pain) { ApplyDamage(pain); }

        public void DestroyGameObject(GameObject obj)
        {
            KnockbackNetworkManager.SafeDestroy(obj);
        }

        void EmitDeathIfNeeded()
        {
            if (!deathEmitted && health <= 0)
            {
                deathEvent?.Invoke();
                deathEventStatic?.Invoke(this, lastAttackerNid);

                deathEmitted = true;
            }
        }

        /// Emit a summary of the health changes periodically. This avoids spamming health values
        /// when taking damage from DoT effects. E.g. multiple fireballs.
        void EmitDeltaSummary()
        {
            if (deltaSummary == null) return;

            // just initialize, nothing else to do.
            if (lastDeltaHealth == -1)
            {
                lastDeltaHealth = health;
                return;
            }

            float delta = health - lastDeltaHealth;
            if (Math.Abs(delta) > 0.5f && (Time.time - lastDeltaTime) >= deltaFrequency)
            {
                deltaSummary(this, delta);
                lastDeltaHealth = health;
                lastDeltaTime = Time.time;
            }
        }

        /// Increase the health over time and respond dynamically if the entity's stats change
        private IEnumerator _IncreaseHealth()
        {
            float lastCheckTime = Time.time;

            // it is OK if there aren't any stats, just assume no regen
            if (_stats == null) yield break;

            while (true)
            {
                yield return new WaitForSeconds(0.25f);
                if (enabled && IsAlive())
                {
                    float maxHealthTmp = _stats.GetFloat(EntityStatType.HealthMax);
                    if (maxHealth != maxHealthTmp)
                    {
                        maxHealth = maxHealthTmp;
                    }

                    float delta = (Time.time - lastCheckTime) * _stats.GetFloat(EntityStatType.HealthRegen);
                    health = Mathf.Min(health + delta, maxHealth);
                    lastCheckTime = Time.time;
                }
            }
        }

        /// <summary>
        /// IsHealable returns true if this object can be healed.
        /// </summary>
        /// <seealso cref="MultiTag.Tag.Healable"/>
        public bool IsHealable()
        {
            if (MultiTag.HasTag(gameObject, MultiTag.Tag.Healable)) return true;
            if (MultiTag.HasTag(gameObject, MultiTag.Tag.Repairable)) return false;

            return true;
        }

        public void OnDisable()
        {
            EmitDeltaSummary();
            EmitDeathIfNeeded();
            StopAllCoroutines();
        }

        public void OnEnable()
        {
            if (isServer)
            {
                StartCoroutine(_IncreaseHealth());
                if (_stats != null)
                {
                    this.WaitUntil(() => _stats != null, delegate ()
                    {
                        maxHealth = _stats.GetFloat(EntityStatType.HealthMax);
                        health = maxHealth;
                        tookDamageLast = health;

                        if (maxHealth <= 0)
                        {
                            log.LogError("Health", $"Max health is {maxHealth} ({gameObject})", this);
                        }
                    });
                }
            }

            tookDamageLast = health;

            StartCoroutine(_SummarizeHealth());
        }

        /// _SummarizeHealth emits the health changes that occur during a fixed time period.
        private IEnumerator _SummarizeHealth()
        {
            yield return new WaitForSeconds(Util.RandRange(0f, 1f));

            while (true)
            {
                if (enabled && IsAlive())
                {
                    EmitDeltaSummary();
                }
                float t = deltaFrequency;
                if (Time.time - lastDeltaTime > 0)
                {
                    t = deltaFrequency - (Time.time - lastDeltaTime);
                }
                yield return new WaitForSeconds(t);
            }
        }

        /// IsAlive returns true if the object is "alive"
        public bool IsAlive()
        {
            return health > 0;
        }

        /// IsHurt returns true if the object has taken damage that brings it below max health.
        public bool IsHurt()
        {
            return health < maxHealth;
        }

        // IsLess is silly but helps with Behavior designer
        public bool IsLess(float h) => health < h;

        /// Kill immediately kills the object. This only take effect on the server.
        public void Kill()
        {
            if (!isServer) CmdKill();
            if (!IsAlive()) return;

            float pain = health + 1;
            ApplyDamage(pain);
        }

        /// OnHealthChange is called on all clients when the objects health changes. This will cause
        /// changeEvent and deathEvents to be invoked, if appropriate.
        public void OnHealthChange(float oldHealth, float newHealth)
        {
            debug?.Log($"OnHealthChange {oldHealth} {newHealth}");
            if (oldHealth == newHealth) return;

            float diff = newHealth - oldHealth;
            // Periodically report the change in health
            EmitDeltaSummary();
            changeEvent?.Invoke(diff);

            if (newHealth > 0) deathEmitted = false;

            EmitDeathIfNeeded();
        }

        [Command]
        void CmdReset()
        {
            if (_stats != null)
            {
                maxHealth = _stats.GetFloat(EntityStatType.HealthMax);
            }
            health = maxHealth;
            lastDeltaHealth = health;
        }

        /// Reset resets the objects health to maxHealth. Only effective on the server.
        public void Reset()
        {
            if (hasAuthority) CmdReset();
        }

        /// RpcDamageTaken is called on all clients whenever damage is taken. This is not called on
        /// healing.
        [ClientRpc]
        public void RpcDamageTaken(float pain, uint source)
        {
            debug?.Log($"RpcDamageTaken {this} {pain} {health} {source}");
            lastAttackerNid = source;
            damageTaken?.Invoke(this, pain, source);
            damageEvent?.Invoke();
            EmitDeathIfNeeded();
        }

        [Server]
        public void SetHealth(float newHealth)
        {
            if (newHealth == health) return;
            health = Mathf.Min(newHealth, maxHealth);
        }

        /// Has this entity taken damage since the last time this function was called. Only really
        /// useful in behaviour trees
        public bool TookDamage()
        {
            bool result = false;
            if (health < tookDamageLast)
            {
                result = true;
            }
            tookDamageLast = health;
            return result;
        }
    }
}
