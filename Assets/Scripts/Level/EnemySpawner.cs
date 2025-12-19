using System.Collections.Generic;
using UnityEngine;
using OneButtonRunner.Core;
using OneButtonRunner.Enemies;
using OneButtonRunner.Player;

namespace OneButtonRunner.Level
{
    /// <summary>
    /// Spawns enemies at intervals ahead of the player.
    /// Manages enemy pools and spawn patterns.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Enemy Prefabs")]
        [SerializeField] private GameObject basicEnemyPrefab;
        [SerializeField] private GameObject mediumEnemyPrefab;
        [SerializeField] private GameObject unexpectedEnemyPrefab;

        [Header("Spawn Settings")]
        [SerializeField] private float initialSpawnDelay = 3f;
        [SerializeField] private float minSpawnInterval = 1f;
        [SerializeField] private float maxSpawnInterval = 3f;
        [SerializeField] private float spawnDistanceAhead = 15f;
        [SerializeField] private float groundSpawnY = -2f;
        [SerializeField] private float ceilingSpawnY = 2f;

        [Header("Spawn Weights (Total should = 100)")]
        [SerializeField] private int basicWeight = 60;
        [SerializeField] private int mediumWeight = 30;
        [SerializeField] private int unexpectedWeight = 10;

        [Header("Difficulty Scaling")]
        [SerializeField] private float difficultyScaleTime = 30f; // Time to reach max difficulty
        [SerializeField] private float minIntervalAtMaxDiff = 0.5f;

        // Pooling
        private Dictionary<EnemyType, Queue<GameObject>> enemyPools = new Dictionary<EnemyType, Queue<GameObject>>();
        private List<GameObject> activeEnemies = new List<GameObject>();

        private float nextSpawnTime;
        private float gameStartTime;
        private Transform playerTransform;

        private void Start()
        {
            Debug.Log("[Spawner] ✓ EnemySpawner Start");
            InitializePools();

            if (PlayerController.Instance != null)
            {
                playerTransform = PlayerController.Instance.transform;
                Debug.Log("[Spawner] ✓ Player reference found");
            }
            else
            {
                Debug.LogError("[Spawner] ✗ PlayerController.Instance is NULL! Spawner won't work!");
            }

            // Check prefabs
            Debug.Log($"[Spawner] Prefabs - Basic: {basicEnemyPrefab != null}, Medium: {mediumEnemyPrefab != null}, Unexpected: {unexpectedEnemyPrefab != null}");

            gameStartTime = Time.time;
            nextSpawnTime = Time.time + initialSpawnDelay;
            Debug.Log($"[Spawner] First spawn in {initialSpawnDelay}s");
        }

        private void OnEnable()
        {
            // Subscribe to game state changes
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged += OnGameStateChanged;
            }
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= OnGameStateChanged;
            }
        }

        private void OnGameStateChanged(GameState newState)
        {
            if (newState == GameState.Playing)
            {
                gameStartTime = Time.time;
                nextSpawnTime = Time.time + initialSpawnDelay;
            }
        }

        private void Update()
        {
            if (GameManager.Instance?.CurrentState != GameState.Playing) return;
            if (playerTransform == null) return;

            if (Time.time >= nextSpawnTime)
            {
                SpawnEnemy();
                ScheduleNextSpawn();
            }

            CleanupDeadEnemies();
        }

        private void InitializePools()
        {
            enemyPools[EnemyType.Basic] = new Queue<GameObject>();
            enemyPools[EnemyType.Medium] = new Queue<GameObject>();
            enemyPools[EnemyType.Unexpected] = new Queue<GameObject>();

            // Pre-warm pools
            CreatePooledEnemies(EnemyType.Basic, basicEnemyPrefab, 5);
            CreatePooledEnemies(EnemyType.Medium, mediumEnemyPrefab, 3);
            CreatePooledEnemies(EnemyType.Unexpected, unexpectedEnemyPrefab, 2);
        }

        private void CreatePooledEnemies(EnemyType type, GameObject prefab, int count)
        {
            if (prefab == null) return;

            for (int i = 0; i < count; i++)
            {
                GameObject enemy = Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);
                enemy.SetActive(false);
                enemyPools[type].Enqueue(enemy);
            }
        }

        private GameObject GetPooledEnemy(EnemyType type)
        {
            if (enemyPools[type].Count > 0)
            {
                return enemyPools[type].Dequeue();
            }

            // Create new if pool exhausted
            GameObject prefab = GetPrefabForType(type);
            if (prefab != null)
            {
                return Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);
            }

            return null;
        }

        private GameObject GetPrefabForType(EnemyType type)
        {
            switch (type)
            {
                case EnemyType.Basic: return basicEnemyPrefab;
                case EnemyType.Medium: return mediumEnemyPrefab;
                case EnemyType.Unexpected: return unexpectedEnemyPrefab;
                default: return null;
            }
        }

        private void ReturnToPool(GameObject enemy, EnemyType type)
        {
            enemy.SetActive(false);
            enemyPools[type].Enqueue(enemy);
        }

        private void SpawnEnemy()
        {
            // Choose enemy type based on weights
            EnemyType type = ChooseEnemyType();
            GameObject enemy = GetPooledEnemy(type);

            if (enemy == null) return;

            // Random spawn position (ground or ceiling)
            bool spawnOnCeiling = Random.value > 0.5f;
            float spawnX = playerTransform.position.x + spawnDistanceAhead;
            float spawnY = spawnOnCeiling ? ceilingSpawnY : groundSpawnY;

            Vector3 spawnPos = new Vector3(spawnX, spawnY, 0);

            // Initialize enemy
            var enemyComponent = enemy.GetComponent<EnemyBase>();
            if (enemyComponent != null)
            {
                enemyComponent.Initialize(spawnPos, spawnOnCeiling);
                enemyComponent.OnEnemyDied += OnEnemyDied;
            }
            else
            {
                enemy.transform.position = spawnPos;
            }

            enemy.SetActive(true);
            activeEnemies.Add(enemy);

            Debug.Log($"[Spawner] ★ Spawned {type} at {spawnPos} (Ceiling: {spawnOnCeiling}). Active enemies: {activeEnemies.Count}");
        }

        private EnemyType ChooseEnemyType()
        {
            int roll = Random.Range(0, basicWeight + mediumWeight + unexpectedWeight);

            if (roll < basicWeight)
                return EnemyType.Basic;
            else if (roll < basicWeight + mediumWeight)
                return EnemyType.Medium;
            else
                return EnemyType.Unexpected;
        }

        private void ScheduleNextSpawn()
        {
            // Difficulty scaling - spawn faster over time
            float elapsed = Time.time - gameStartTime;
            float difficultyT = Mathf.Clamp01(elapsed / difficultyScaleTime);

            float interval = Mathf.Lerp(maxSpawnInterval, minIntervalAtMaxDiff, difficultyT);
            interval = Mathf.Max(interval, minSpawnInterval);

            nextSpawnTime = Time.time + Random.Range(interval * 0.8f, interval * 1.2f);
        }

        private void OnEnemyDied(EnemyBase enemy)
        {
            // Could add score here
            enemy.OnEnemyDied -= OnEnemyDied;
        }

        private void CleanupDeadEnemies()
        {
            float despawnX = playerTransform.position.x - 20f; // Despawn behind player

            for (int i = activeEnemies.Count - 1; i >= 0; i--)
            {
                var enemy = activeEnemies[i];
                if (enemy == null || !enemy.activeInHierarchy || enemy.transform.position.x < despawnX)
                {
                    if (enemy != null)
                    {
                        var enemyBase = enemy.GetComponent<EnemyBase>();
                        if (enemyBase != null)
                        {
                            ReturnToPool(enemy, enemyBase.Type);
                        }
                    }
                    activeEnemies.RemoveAt(i);
                }
            }
        }

        public void ResetSpawner()
        {
            // Return all active enemies to pool
            foreach (var enemy in activeEnemies)
            {
                if (enemy != null)
                {
                    var enemyBase = enemy.GetComponent<EnemyBase>();
                    if (enemyBase != null)
                    {
                        ReturnToPool(enemy, enemyBase.Type);
                    }
                }
            }
            activeEnemies.Clear();

            gameStartTime = Time.time;
            nextSpawnTime = Time.time + initialSpawnDelay;
        }
    }
}
