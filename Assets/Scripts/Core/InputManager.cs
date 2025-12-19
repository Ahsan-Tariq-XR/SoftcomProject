using System;
using UnityEngine;

namespace OneButtonRunner.Core
{
    /// <summary>
    /// Handles all input for the one-button game.
    /// Detects: Tap (Light Attack), Hold+Release (Charged Attack), Double-Tap (Gravity Flip)
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        [Header("Timing Settings")]
        [SerializeField] private float holdThreshold = 0.2f;      // Time to distinguish tap from hold
        [SerializeField] private float doubleTapWindow = 0.25f;   // Time window for double tap

        // Events for other scripts to subscribe to
        public event Action OnLightAttack;
        public event Action OnChargeStart;
        public event Action OnChargedAttackRelease;
        public event Action OnGravityFlip;

        // Internal state tracking
        private float pressStartTime;
        private float lastTapTime;
        private bool isHolding;
        private bool isCharging;
        private bool waitingForDoubleTap;

        private void Awake()
        {
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[InputManager] Duplicate instance destroyed!");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Debug.Log($"[InputManager] ✓ Initialized! Hold threshold: {holdThreshold}s, Double-tap window: {doubleTapWindow}s");
        }

        private void Update()
        {
            HandleInput();
        }

        private void HandleInput()
        {
            // Button pressed down
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
            {
                OnButtonDown();
            }

            // Button held
            if (Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0))
            {
                OnButtonHeld();
            }

            // Button released
            if (Input.GetKeyUp(KeyCode.Space) || Input.GetMouseButtonUp(0))
            {
                OnButtonUp();
            }

            // Check for double-tap timeout
            CheckDoubleTapTimeout();
        }

        private void OnButtonDown()
        {
            pressStartTime = Time.time;
            isHolding = true;
            isCharging = false;
            Debug.Log($"[InputManager] ▼ Button DOWN at {Time.time:F2}s");

            // Check for double tap
            if (waitingForDoubleTap && (Time.time - lastTapTime) <= doubleTapWindow)
            {
                // Double tap detected!
                float timeSinceLastTap = Time.time - lastTapTime;
                waitingForDoubleTap = false;
                Debug.Log($"[InputManager] ★★ DOUBLE TAP detected! Time between taps: {timeSinceLastTap:F3}s");
                OnGravityFlip?.Invoke();
                Debug.Log("[InputManager] → Event: OnGravityFlip invoked");
            }
        }

        private void OnButtonHeld()
        {
            if (!isHolding) return;

            float holdDuration = Time.time - pressStartTime;

            // If held long enough, start charging
            if (holdDuration >= holdThreshold && !isCharging)
            {
                isCharging = true;
                waitingForDoubleTap = false; // Cancel any pending tap
                OnChargeStart?.Invoke();
                Debug.Log("[InputManager] CHARGE START!");
            }
        }

        private void OnButtonUp()
        {
            if (!isHolding) return;

            float holdDuration = Time.time - pressStartTime;
            isHolding = false;
            Debug.Log($"[InputManager] ▲ Button UP - held for {holdDuration:F3}s");

            if (isCharging)
            {
                // Was charging - release charged attack
                isCharging = false;
                Debug.Log($"[InputManager] ★ CHARGED ATTACK released! Charge time: {holdDuration:F2}s");
                OnChargedAttackRelease?.Invoke();
                Debug.Log("[InputManager] → Event: OnChargedAttackRelease invoked");
            }
            else if (holdDuration < holdThreshold)
            {
                // Quick tap - FIRE FIRST (Optimistic)
                Debug.Log($"[InputManager] ★ LIGHT ATTACK (Instant fire)");
                OnLightAttack?.Invoke();
                
                // Check for double tap window
                if (!waitingForDoubleTap)
                {
                    waitingForDoubleTap = true;
                    lastTapTime = Time.time;
                    Debug.Log($"[InputManager] ? Waiting for possible double-tap...");
                }
            }
            else
            {
                Debug.Log($"[InputManager] ✗ Hold was {holdDuration:F3}s but charging didn't start (threshold: {holdThreshold}s)");
            }
        }

        private void CheckDoubleTapTimeout()
        {
            // Simply expire the double tap window if time passes
            if (waitingForDoubleTap && (Time.time - lastTapTime) > doubleTapWindow)
            {
                waitingForDoubleTap = false;
                // No delayed attack needed anymore - we already fired!
            }
        }

        /// <summary>
        /// Returns true if player is currently charging an attack
        /// </summary>
        public bool IsCharging => isCharging;

        /// <summary>
        /// Returns how long the button has been held (useful for charge meter UI)
        /// </summary>
        public float GetChargeTime()
        {
            if (!isCharging) return 0f;
            return Time.time - pressStartTime - holdThreshold;
        }
    }
}
