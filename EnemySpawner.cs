using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages the continuous spawning of enemies around the player or spawner position.
/// Uses object pooling for efficient enemy instantiation and manages spawn rates based on score.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [SerializeField]
    private GameObject basicEnemyPrefab;    // Standard enemy type
    [SerializeField]
    private GameObject runnerPrefab;        // Fast-moving enemy type
    [SerializeField]
    private GameObject brutusPrefab;        // Heavy, powerful enemy type

    public float baseSpawnInterval = 0.5f;  // Time between enemy spawns at score 0
    public float spawnRadius = 150f;        // Distance from spawn point to spawn enemies

    // Controls the probability of spawning each enemy type
    [SerializeField] private int basicEnemyWeight = 5;   // Most common enemy type
    [SerializeField] private int runnerEnemyWeight = 3;  // Medium frequency enemy type
    [SerializeField] private int brutusEnemyWeight = 1;  // Rare enemy type

    // --- NEW: spawn positioning options ---
    [Header("Spawn Positioning")]
    [Tooltip("If true, use a screen point to determine spawn world position.")]
    [SerializeField] private bool useScreenPoint = false;

    [Tooltip("If normalized, X/Y are 0..1 relative to screen. Otherwise use pixel coordinates.")]
    [SerializeField] private bool screenPointIsNormalized = true;

    [Tooltip("Screen point used when useScreenPoint is enabled. If normalized: (0..1,0..1). If not: pixels.")]
    [SerializeField] private Vector2 screenPoint = new Vector2(0.5f, 0.5f);

    [Tooltip("Optional camera to convert screen->world. If null, Camera.main is used.")]
    [SerializeField] private Camera spawnCamera;

    [Tooltip("World Z offset to place spawned enemies (useful for 2D setups).")]
    [SerializeField] private float spawnZOffset = 0f;

    [Tooltip("If true and a fixedTransform is assigned, spawn at that Transform instead of screen point.")]
    [SerializeField] private bool useFixedTransform = false;
    [SerializeField] private Transform fixedSpawnTransform = null;
    // --- END NEW ---

    private float nextSpawnTime;            // When the next enemy should spawn
    private Transform playerTransform;       // Reference to player for spawn positioning
    private List<GameObject> enemyPool;     // Pool of reusable enemy objects
    private int poolSize = 200;             // Maximum number of enemies in the pool
    private ScoreManager scoreManager;       // Reference to score system for spawn rate adjustment
    private float currentSpawnInterval;     // Current time between spawns after score adjustments

    void Start()
    {
        // Initialize player reference for spawn positioning
        playerTransform = GameObject.FindWithTag("Player")?.transform;

        // Initialize score manager for spawn rate adjustments
        scoreManager = FindObjectOfType<ScoreManager>();

        // Initialize spawn timing and object pool
        currentSpawnInterval = baseSpawnInterval;
        nextSpawnTime = Time.time + currentSpawnInterval;
        enemyPool = new List<GameObject>();
        PrewarmPool();

        // fallback camera
        if (spawnCamera == null)
            spawnCamera = Camera.main;
    }

    /// <summary>
    /// Creates initial pool of enemy objects at a hidden location
    /// </summary>
    void PrewarmPool()
    {
        Vector3 hiddenPosition = new Vector3(1000f, 1000f, 0f);
        transform.position = hiddenPosition;

        // Create initial pool of enemies
        for (int i = 0; i < poolSize/2; i++)
            CreateEnemy(basicEnemyPrefab);
        for (int i = 0; i < poolSize/3; i++)
            CreateEnemy(runnerPrefab);
        for (int i = 0; i < poolSize/6; i++)
            CreateEnemy(brutusPrefab);

        transform.position = Vector3.zero;
    }

    /// <summary>
    /// Creates a new enemy instance and adds it to the pool
    /// </summary>
    GameObject CreateEnemy(GameObject prefab)
    {
        if (prefab == null) return null;

        GameObject enemy = Instantiate(prefab, transform.position, Quaternion.identity);
        enemy.SetActive(false);
        enemyPool.Add(enemy);
        return enemy;
    }

    /// <summary>
    /// Retrieves an inactive enemy of the specified type from the pool
    /// Creates a new one if none are available and pool isn't full
    /// </summary>
    GameObject GetInactiveEnemy(GameObject prefabType)
    {
        foreach (GameObject enemy in enemyPool)
        {
            if (enemy == null) continue;
            if (!enemy.activeInHierarchy && enemy.name.Contains(prefabType.name))
            {
                return enemy;
            }
        }

        if (enemyPool.Count < poolSize)
        {
            return CreateEnemy(prefabType);
        }

        return null;
    }

    void Update()
    {
        // Update spawn timing based on current score
        UpdateSpawnInterval();

        // Spawn new enemy when it's time
        if (Time.time >= nextSpawnTime)
        {
            SpawnEnemy();
            nextSpawnTime = Time.time + currentSpawnInterval;
        }
    }

    /// <summary>
    /// Adjusts spawn interval based on player's score
    /// Spawns become faster as score increases
    /// </summary>
    void UpdateSpawnInterval()
    {
        if (scoreManager == null) return;

        int currentScore = scoreManager.GetCurrentScore();
        float intervalMultiplier = 1.0f;

        // Progressively decrease spawn interval as score increases
        if (currentScore >= 10)
            intervalMultiplier *= 0.8f;
        if (currentScore >= 25)
            intervalMultiplier *= 0.8f;
        if (currentScore >= 50)
            intervalMultiplier *= 0.8f;

        // Additional speed increase for every 50 points after 50
        if (currentScore > 50)
        {
            int additionalReductions = (currentScore - 50) / 50;
            for (int i = 0; i < additionalReductions; i++)
            {
                intervalMultiplier *= 0.8f;
            }
        }

        float newSpawnInterval = baseSpawnInterval * intervalMultiplier;

        if (newSpawnInterval != currentSpawnInterval)
        {
            currentSpawnInterval = newSpawnInterval;
        }
    }

    /// <summary>
    /// Spawns a single enemy. Position can be:
    /// - fixed Transform (useFixedTransform)
    /// - a screen point converted to world (useScreenPoint)
    /// - fallback: random circle around player/spawner (original behavior)
    /// </summary>
    void SpawnEnemy()
    {
        GameObject prefabToUse = ChooseEnemyType();
        if (prefabToUse == null) return;

        GameObject enemy = GetInactiveEnemy(prefabToUse);
        if (enemy == null) return;

        Vector3 spawnPosition;

        if (useFixedTransform && fixedSpawnTransform != null)
        {
            // spawn at explicit Transform
            spawnPosition = fixedSpawnTransform.position;
        }
        else if (useScreenPoint)
        {
            // convert configured screen point to world position
            Camera cam = spawnCamera != null ? spawnCamera : Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("No camera available for ScreenToWorldPoint. Falling back to random spawn.");
                spawnPosition = GetRandomSpawnPosition();
            }
            else
            {
                Vector2 screenPx;
                if (screenPointIsNormalized)
                    screenPx = new Vector2(screenPoint.x * Screen.width, screenPoint.y * Screen.height);
                else
                    screenPx = screenPoint;

                // compute distance from camera to the desired world Z
                float zDistanceFromCamera = Mathf.Abs(cam.transform.position.z) + spawnZOffset;
                Vector3 screenVec = new Vector3(screenPx.x, screenPx.y, zDistanceFromCamera);
                Vector3 worldPoint = cam.ScreenToWorldPoint(screenVec);
                spawnPosition = new Vector3(worldPoint.x, worldPoint.y, worldPoint.z);
            }
        }
        else
        {
            // original behaviour: random point around player or spawner
            spawnPosition = GetRandomSpawnPosition();
        }

        enemy.transform.position = spawnPosition;
        enemy.transform.rotation = Quaternion.identity;

        Enemy enemyComponent = enemy.GetComponent<Enemy>();
        if (enemyComponent != null)
        {
            enemy.SetActive(true);
        }
    }

    // helper to compute original random spawn location
    Vector3 GetRandomSpawnPosition()
    {
        Vector2 randomPoint = Random.insideUnitCircle.normalized * spawnRadius;
        Vector3 spawnPosition = playerTransform != null
            ? playerTransform.position + new Vector3(randomPoint.x, randomPoint.y, 0)
            : transform.position + new Vector3(randomPoint.x, randomPoint.y, 0);
        return spawnPosition;
    }

    /// <summary>
    /// Selects which type of enemy to spawn based on configured weights
    /// </summary>
    private GameObject ChooseEnemyType()
    {
        int totalWeight = basicEnemyWeight + runnerEnemyWeight + brutusEnemyWeight;
        int randomValue = Random.Range(0, totalWeight);

        if (randomValue < basicEnemyWeight)
            return basicEnemyPrefab;
        else if (randomValue < basicEnemyWeight + runnerEnemyWeight)
            return runnerPrefab;
        else
            return brutusPrefab;
    }

    /// <summary>
    /// Deactivates all active enemies and resets spawn timing
    /// </summary>
    public void ResetEnemies()
    {
        foreach (GameObject enemy in enemyPool)
        {
            if (enemy != null)
            {
                enemy.SetActive(false);
            }
        }

        nextSpawnTime = Time.time + currentSpawnInterval;
    }

    /// <summary>
    /// Visualizes the spawn radius and chosen spawn point in the Unity editor
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);

        // draw chosen spawn point when using fixed transform
        if (useFixedTransform && fixedSpawnTransform != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(fixedSpawnTransform.position, 0.5f);
        }

        // draw screen-derived spawn point
        if (useScreenPoint && (spawnCamera != null || Camera.main != null))
        {
            Camera cam = spawnCamera != null ? spawnCamera : Camera.main;
            if (cam != null)
            {
                Vector2 screenPx = screenPointIsNormalized ? new Vector2(screenPoint.x * Screen.width, screenPoint.y * Screen.height) : screenPoint;
                float zDistanceFromCamera = Mathf.Abs(cam.transform.position.z) + spawnZOffset;
                Vector3 worldPoint = cam.ScreenToWorldPoint(new Vector3(screenPx.x, screenPx.y, zDistanceFromCamera));
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(worldPoint, 0.5f);
            }
        }
    }

    /// <summary>
    /// Returns a list of all currently active enemies
    /// </summary>
    public List<GameObject> GetActiveEnemies()
    {
        List<GameObject> activeEnemies = new List<GameObject>();
        foreach (GameObject enemy in enemyPool)
        {
            if (enemy != null && enemy.activeInHierarchy)
            {
                activeEnemies.Add(enemy);
            }
        }
        return activeEnemies;
    }
}