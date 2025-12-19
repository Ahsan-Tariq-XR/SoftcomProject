using System;
using UnityEngine;

namespace OneButtonRunner.Core
{
    public enum GameState
    {
        Menu,
        Playing,
        Paused,
        GameOver
    }

    /// <summary>
    /// Central game manager - handles game state, score, and restart logic.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Game Settings")]
        [SerializeField] private float baseScrollSpeed = 5f;
        [SerializeField] private float speedIncreaseRate = 0.1f; // Speed increase per second

        [Header("Debug")]
        [SerializeField] private bool autoStartGame = true; // Toggle in Inspector for testing!

        public event Action<GameState> OnGameStateChanged;
        public event Action<float> OnScoreChanged;

        // Properties
        public GameState CurrentState { get; private set; } = GameState.Menu;
        public float Score { get; private set; }
        public float CurrentSpeed => baseScrollSpeed + (Score * speedIncreaseRate);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GameManager] Duplicate instance destroyed!");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log($"[GameManager] ✓ Initialized! Base speed: {baseScrollSpeed}, Speed increase rate: {speedIncreaseRate}");
        }

        private void Start()
        {
            if (autoStartGame)
            {
                Debug.Log("[GameManager] Auto-starting game for testing...");
                StartGame();
            }
        }

        private void Update()
        {
            if (CurrentState == GameState.Playing)
            {
                // Score increases with time/distance
                Score += Time.deltaTime;
                OnScoreChanged?.Invoke(Score);
            }
        }

        public void StartGame()
        {
            Score = 0f;
            SetGameState(GameState.Playing);
            Debug.Log("[GameManager] Game Started!");
        }

        public void PauseGame()
        {
            if (CurrentState == GameState.Playing)
            {
                SetGameState(GameState.Paused);
                Time.timeScale = 0f;
            }
        }

        public void ResumeGame()
        {
            if (CurrentState == GameState.Paused)
            {
                Time.timeScale = 1f;
                SetGameState(GameState.Playing);
            }
        }

        public void GameOver()
        {
            SetGameState(GameState.GameOver);
            Debug.Log($"[GameManager] Game Over! Final Score: {Score:F1}");
        }

        public void RestartGame()
        {
            Time.timeScale = 1f;
            StartGame();
        }

        public void ReturnToMenu()
        {
            Time.timeScale = 1f;
            Score = 0f;
            SetGameState(GameState.Menu);
        }

        private void SetGameState(GameState newState)
        {
            GameState oldState = CurrentState;
            CurrentState = newState;
            Debug.Log($"[GameManager] State changed: {oldState} → {newState}");
            OnGameStateChanged?.Invoke(newState);
        }
    }
}
