using UnityEngine;
using OneButtonRunner.Core;

namespace OneButtonRunner.Enemies
{
    /// <summary>
    /// Unexpected Enemy - High damage, IMMUNE to light attacks, only dies to charged.
    /// Attach this script directly to UnexpectedEnemy prefab.
    /// </summary>
    public class UnexpectedEnemy : EnemyBase
    {
        [Header("Unexpected Behavior")]
        [SerializeField] private float minSpeed = 2f;
        [SerializeField] private float maxSpeed = 6f;
        [SerializeField] private float speedChangeInterval = 1f;

        private float currentSpeed;
        private float nextSpeedChangeTime;

        protected override void Awake()
        {
            base.Awake();
            Type = EnemyType.Unexpected;

            // Stats
            maxHealth = 100;    // Tanky - requires charged attack
            damage = 40;        // HIGH damage - punishes mistakes
            moveSpeed = 3f;     // Base speed (randomized at runtime)
            immuneToLightAttacks = true; // KEY: Light attacks don't work!
        }

        protected override void Start()
        {
            base.Start();
            currentSpeed = Random.Range(minSpeed, maxSpeed);
            nextSpeedChangeTime = Time.time + speedChangeInterval;

            // Red color - menacing!
            if (spriteRenderer != null)
            {
                normalColor = new Color(0.8f, 0.2f, 0.2f);
                spriteRenderer.color = normalColor;
            }
        }

        protected override void Move()
        {
            // Unpredictable speed changes
            if (Time.time >= nextSpeedChangeTime)
            {
                currentSpeed = Random.Range(minSpeed, maxSpeed);
                nextSpeedChangeTime = Time.time + speedChangeInterval;
            }

            rb.linearVelocity = new Vector2(-currentSpeed, rb.linearVelocity.y);
        }

        public override void TakeDamage(int damageAmount, bool isChargedAttack)
        {
            if (!isChargedAttack)
            {
                // Light attacks don't work - taunt the player
                Debug.Log("[Unexpected] Light attack has no effect! Use charged attack!");
                FlashColor(Color.gray);
                
                // Speed up briefly as punishment
                currentSpeed = maxSpeed;
                return;
            }

            // Charged attacks work normally
            base.TakeDamage(damageAmount, isChargedAttack);
        }

        protected override void Die()
        {
            Debug.Log("[Unexpected] The mighty one falls!");
            base.Die();
        }
    }
}
