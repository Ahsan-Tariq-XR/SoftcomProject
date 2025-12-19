using UnityEngine;
using OneButtonRunner.Core;

namespace OneButtonRunner.Enemies
{
    /// <summary>
    /// Medium Enemy - Fast, medium health, dies to 1 charged or 2 light attacks.
    /// Attach this script directly to MediumEnemy prefab.
    /// </summary>
    public class MediumEnemy : EnemyBase
    {
        protected override void Awake()
        {
            base.Awake();
            Type = EnemyType.Medium;

            // Stats
            maxHealth = 50;     // Dies to 1 charged (100) or 2 light (25+25)
            damage = 15;        // Medium damage
            moveSpeed = 4f;     // Fast
            immuneToLightAttacks = false;
        }

        protected override void Start()
        {
            base.Start();

            // Orange color
            if (spriteRenderer != null)
            {
                normalColor = new Color(0.8f, 0.6f, 0.2f);
                spriteRenderer.color = normalColor;
            }
        }

        protected override void Move()
        {
            // Slight speed variation for unpredictability
            float variation = Mathf.Sin(Time.time * 2f) * 0.5f;
            rb.linearVelocity = new Vector2(-(moveSpeed + variation), rb.linearVelocity.y);
        }
    }
}
