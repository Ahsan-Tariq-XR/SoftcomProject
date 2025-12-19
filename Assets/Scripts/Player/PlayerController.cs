using UnityEngine;
using OneButtonRunner.Core;

namespace OneButtonRunner.Player
{
    public enum PlayerState
    {
        Running,
        Charging,
        Attacking,
        Stunned
    }

    /// <summary>
    /// Main player controller - handles movement, gravity, attacks, and health.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float acceleration = 15f; // How fast player accelerates to target speed
        [SerializeField] private float deceleration = 25f; // How fast player slows down (for knockback recovery)

        [Header("Gravity Flip")]
        [SerializeField] private float flipDuration = 0.3f;

        [Header("Combat")]
        [SerializeField] private Transform attackPoint;
        [SerializeField] private float lightAttackRange = 1.5f;
        [SerializeField] private float chargedAttackRange = 2.5f;
        [SerializeField] private LayerMask enemyLayer;

        [Header("Health")]
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private float knockbackForce = 8f;  // Knockback on damage

        [Header("Visual Feedback")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color chargingColor = Color.yellow;
        [SerializeField] private Color hurtColor = Color.red;

        [Header("Testing")]
        [SerializeField] private bool godMode = false; // Unlimited health for testing

        // Component references
        private Rigidbody2D rb;
        private BoxCollider2D boxCollider;

        // State
        public PlayerState CurrentState { get; private set; } = PlayerState.Running;
        public int CurrentHealth { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsGravityFlipped { get; private set; }

        // Internal tracking
        private bool isDead;
        private bool isInKnockback; // Damage cooldown during knockback
        private float chargeStartTime;

        // Events
        public System.Action<int, int> OnHealthChanged; // current, max
        public System.Action OnPlayerDied;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Player] Duplicate instance destroyed!");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Debug.Log("[Player] ✓ PlayerController Awake");

            rb = GetComponent<Rigidbody2D>();
            boxCollider = GetComponent<BoxCollider2D>();

            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            
            Debug.Log($"[Player] Components: RB={rb != null}, Collider={boxCollider != null}, Sprite={spriteRenderer != null}");
        }

        private void Start()
        {
            CurrentHealth = maxHealth;
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
            Debug.Log($"[Player] ✓ Health initialized: {CurrentHealth}/{maxHealth}");

            // Subscribe to input events
            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnLightAttack += PerformLightAttack;
                InputManager.Instance.OnChargeStart += StartCharging;
                InputManager.Instance.OnChargedAttackRelease += PerformChargedAttack;
                InputManager.Instance.OnGravityFlip += FlipGravity;
                Debug.Log("[Player] ✓ Subscribed to InputManager events");
            }
            else
            {
                Debug.LogError("[Player] ✗ InputManager.Instance is NULL! Input won't work!");
            }
            
            Debug.Log($"[Player] Attack Point assigned: {attackPoint != null}, Enemy Layer: {enemyLayer.value}");
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnLightAttack -= PerformLightAttack;
                InputManager.Instance.OnChargeStart -= StartCharging;
                InputManager.Instance.OnChargedAttackRelease -= PerformChargedAttack;
                InputManager.Instance.OnGravityFlip -= FlipGravity;
            }
        }

        private void Update()
        {
            // Visual feedback for charging
            if (CurrentState == PlayerState.Charging)
            {
                float chargeProgress = (Time.time - chargeStartTime) / 1f; // 1 second for full charge visual
                spriteRenderer.color = Color.Lerp(normalColor, chargingColor, Mathf.Clamp01(chargeProgress));
            }
        }

        private void FixedUpdate()
        {
            if (GameManager.Instance?.CurrentState != GameState.Playing) return;
            if (CurrentState == PlayerState.Stunned) return;

            // Auto-run forward
            MoveForward();

            // Check grounded
            CheckGrounded();
        }

        #region Movement

        private void MoveForward()
        {
            float targetSpeed = GameManager.Instance?.CurrentSpeed ?? moveSpeed;
            float currentSpeedX = rb.linearVelocity.x;
            
            // Accelerate towards target speed
            float newSpeedX;
            if (currentSpeedX < targetSpeed)
            {
                // Accelerating forward
                newSpeedX = Mathf.MoveTowards(currentSpeedX, targetSpeed, acceleration * Time.fixedDeltaTime);
            }
            else
            {
                // Already at or above target (shouldn't normally happen but handle it)
                newSpeedX = currentSpeedX;
            }
            
            rb.linearVelocity = new Vector2(newSpeedX, rb.linearVelocity.y);
        }

        private void CheckGrounded()
        {
            // Cast down (or up if flipped) to check for ground
            Vector2 direction = IsGravityFlipped ? Vector2.up : Vector2.down;
            float distance = boxCollider.bounds.extents.y + 0.1f;

            RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, distance, LayerMask.GetMask("Ground"));
            IsGrounded = hit.collider != null;
        }

        #endregion

        #region Gravity Flip

        private void FlipGravity()
        {
            if (CurrentState == PlayerState.Stunned) return;

            IsGravityFlipped = !IsGravityFlipped;

            // Flip gravity
            rb.gravityScale = IsGravityFlipped ? -1f : 1f;

            // Flip sprite
            Vector3 scale = transform.localScale;
            scale.y = IsGravityFlipped ? -Mathf.Abs(scale.y) : Mathf.Abs(scale.y);
            transform.localScale = scale;

            Debug.Log($"[Player] Gravity flipped! Now: {(IsGravityFlipped ? "Inverted" : "Normal")}");
        }

        #endregion

        #region Combat

        private void StartCharging()
        {
            if (CurrentState == PlayerState.Stunned) return;

            CurrentState = PlayerState.Charging;
            chargeStartTime = Time.time;
            Debug.Log("[Player] Started charging...");
        }

        private void PerformLightAttack()
        {
            // No stun check - player can always attack

            CurrentState = PlayerState.Attacking;
            Vector3 attackPos = attackPoint != null ? attackPoint.position : transform.position;
            Debug.Log($"[Player] ★ LIGHT ATTACK at position {attackPos}, range: {lightAttackRange}");

            // Detect enemies in range
            Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(
                attackPos,
                lightAttackRange,
                enemyLayer
            );
            
            Debug.Log($"[Player] Found {hitEnemies.Length} enemies in range");

            foreach (Collider2D enemy in hitEnemies)
            {
                var enemyBase = enemy.GetComponent<Enemies.EnemyBase>();
                if (enemyBase != null)
                {
                    Debug.Log($"[Player] → Hitting enemy: {enemy.gameObject.name}");
                    enemyBase.TakeDamage(GameConstants.LIGHT_ATTACK_DAMAGE, false);
                }
            }

            // Return to running
            CurrentState = PlayerState.Running;
            SetSpriteColor(normalColor);
        }

        private void PerformChargedAttack()
        {
            if (CurrentState != PlayerState.Charging)
            {
                Debug.Log($"[Player] Charged attack blocked - state is {CurrentState}, not Charging");
                return;
            }

            CurrentState = PlayerState.Attacking;
            Vector3 attackPos = attackPoint != null ? attackPoint.position : transform.position;
            Debug.Log($"[Player] ★★ CHARGED ATTACK at position {attackPos}, range: {chargedAttackRange}");

            // Detect enemies in larger range
            Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(
                attackPos,
                chargedAttackRange,
                enemyLayer
            );
            
            Debug.Log($"[Player] Found {hitEnemies.Length} enemies in charged range");

            foreach (Collider2D enemy in hitEnemies)
            {
                var enemyBase = enemy.GetComponent<Enemies.EnemyBase>();
                if (enemyBase != null)
                {
                    Debug.Log($"[Player] → Hitting enemy with charged attack: {enemy.gameObject.name}");
                    enemyBase.TakeDamage(GameConstants.CHARGED_ATTACK_DAMAGE, true);
                }
            }

            // Return to running
            CurrentState = PlayerState.Running;
            SetSpriteColor(normalColor);
        }

        #endregion

        #region Health & Damage

        public void TakeDamage(int damage, Vector2 knockbackDirection)
        {
            if (isDead) return; // No damage after death
            if (isInKnockback) return; // One hit per knockback cycle

            // SET COOLDOWN IMMEDIATELY to prevent race condition
            isInKnockback = true;
            Invoke(nameof(EndKnockback), 0.8f); // 0.8s damage cooldown

            // God mode - still show feedback but don't take damage
            if (godMode)
            {
                Debug.Log($"[Player] GOD MODE - Would have taken {damage} damage");
                ApplyKnockback(knockbackDirection);
                SetSpriteColor(hurtColor);
                Invoke(nameof(ResetColor), 0.2f);
                return;
            }

            CurrentHealth -= damage;
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

            Debug.Log($"[Player] Took {damage} damage! Health: {CurrentHealth}/{maxHealth}");

            if (CurrentHealth <= 0)
            {
                Die();
                return;
            }

            // Knockback physics
            ApplyKnockback(knockbackDirection);
            SetSpriteColor(hurtColor);
            
            // Flash back to normal after short delay
            Invoke(nameof(ResetColor), 0.2f);
        }

        private void ResetColor()
        {
            if (!isDead)
                SetSpriteColor(normalColor);
        }

        private void ApplyKnockback(Vector2 direction)
        {
            // Smooth horizontal knockback - no arc, just slide back
            StartCoroutine(SmoothKnockbackRoutine());
        }

        private System.Collections.IEnumerator SmoothKnockbackRoutine()
        {
            float knockbackDistance = knockbackForce * 0.5f; // Convert force to distance
            float knockbackDuration = 0.25f;
            float elapsed = 0f;
            
            Vector3 startPos = transform.position;
            Vector3 endPos = startPos + new Vector3(-knockbackDistance, 0, 0); // Straight back
            
            // Disable physics movement during knockback
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            
            while (elapsed < knockbackDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / knockbackDuration;
                // Smooth ease-out
                float smoothT = 1f - Mathf.Pow(1f - t, 3f);
                
                transform.position = Vector3.Lerp(startPos, endPos, smoothT);
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y); // Keep vertical, zero horizontal
                
                yield return null;
            }
            
            transform.position = endPos;
        }

        private void EndKnockback()
        {
            isInKnockback = false;
        }

        private void Die()
        {
            isDead = true;
            CurrentState = PlayerState.Stunned;
            Debug.Log("[Player] DIED!");
            OnPlayerDied?.Invoke();
            GameManager.Instance?.GameOver();
        }

        #endregion

        #region Helpers

        private void SetSpriteColor(Color color)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = color;
            }
        }

        private void OnDrawGizmosSelected()
        {
            DrawAttackGizmos(false);
        }

        private void OnDrawGizmos()
        {
            // Always show gizmos during play mode
            if (Application.isPlaying)
            {
                DrawAttackGizmos(true);
            }
        }

        private void DrawAttackGizmos(bool isPlayMode)
        {
            Vector3 point = attackPoint != null ? attackPoint.position : transform.position;

            // Light attack range - yellow (bright when attacking)
            if (isPlayMode && CurrentState == PlayerState.Attacking)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.8f); // Bright yellow
                Gizmos.DrawSphere(point, lightAttackRange); // Solid sphere when attacking
            }
            else
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.3f); // Faded yellow
                Gizmos.DrawWireSphere(point, lightAttackRange);
            }

            // Charged attack range - red (bright when charging)
            if (isPlayMode && CurrentState == PlayerState.Charging)
            {
                Gizmos.color = new Color(1f, 0.3f, 0f, 0.5f); // Bright orange-red
                Gizmos.DrawSphere(point, chargedAttackRange); // Solid sphere when charging
            }
            else
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.2f); // Faded red
                Gizmos.DrawWireSphere(point, chargedAttackRange);
            }
        }

        #endregion
    }
}
