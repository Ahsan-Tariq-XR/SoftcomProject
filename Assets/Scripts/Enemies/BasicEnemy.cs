using UnityEngine;
using OneButtonRunner.Core;

namespace OneButtonRunner.Enemies
{
    /// <summary>
    /// Basic Enemy - Slow, low health, dies to any attack.
    /// Attach this script directly to BasicEnemy prefab.
    /// </summary>
    public class BasicEnemy : EnemyBase
    {
        protected override void Awake()
        {
            base.Awake();
            Type = EnemyType.Basic;

            // Stats
            maxHealth = 1;      // Dies in 1 hit
            damage = 10;        // Low damage
            moveSpeed = 2f;     // Slow
            immuneToLightAttacks = false;
        }

        protected override void Start()
        {
            base.Start();

            // Green color
            if (spriteRenderer != null)
            {
                normalColor = new Color(0.4f, 0.8f, 0.4f);
                spriteRenderer.color = normalColor;
            }
        }
    }
}
