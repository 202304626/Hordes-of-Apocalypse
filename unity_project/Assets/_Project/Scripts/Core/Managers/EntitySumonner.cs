using System;
using System.Collections.Generic;
using UnityEngine;

public class EntitySummoner : MonoBehaviour
{
    public static List<Enemy> EnemiesInGame;
    public static List<Transform> EnemiesInGameTransform;
    public static Dictionary<int, GameObject> EnemyPrefabs;
    public static Dictionary<int, Queue<Enemy>> EnemyObjectPools;
    public static Dictionary<Transform, Enemy> EnemyTransformPairs;
    public static bool IsInitialized;

    [Header("Debug Settings")]
    public bool showSummonDebug = true;

    private float lastCleanupTime = 0f;
    private const float CLEANUP_INTERVAL = 3f;

    void Update()
    {
        if (Time.time - lastCleanupTime > CLEANUP_INTERVAL)
        {
            CleanOrphanedEnemies();
            lastCleanupTime = Time.time;
        }
    }

    public static void Init()
    {
        if (!IsInitialized)
        {
            ClearPools();

            EnemyPrefabs = new Dictionary<int, GameObject>();
            EnemyObjectPools = new Dictionary<int, Queue<Enemy>>();
            EnemiesInGame = new List<Enemy>();
            EnemiesInGameTransform = new List<Transform>();
            EnemyTransformPairs = new Dictionary<Transform, Enemy>();

            EnemySummonData[] Enemies = Resources.LoadAll<EnemySummonData>("Enemies");

            foreach (EnemySummonData enemy in Enemies)
            {
                EnemyPrefabs.Add(enemy.EnemyId, enemy.EnemyPrefab);
                EnemyObjectPools.Add(enemy.EnemyId, new Queue<Enemy>());
            }
            IsInitialized = true;

            CreateEnemyHierarchySystem();
        }
    }

    private static void CreateEnemyHierarchySystem()
    {
        GameObject enemySystem = GameObject.Find("EnemySystem");
        if (enemySystem == null)
        {
            enemySystem = new GameObject("EnemySystem");
            enemySystem.transform.position = Vector3.zero;
        }

        Transform enemiesContainer = enemySystem.transform.Find("Enemies");
        if (enemiesContainer == null)
        {
            GameObject enemiesObj = new GameObject("Enemies");
            enemiesObj.transform.SetParent(enemySystem.transform);
            enemiesObj.transform.localPosition = Vector3.zero;
        }
    }

    public static Transform GetEnemiesContainer()
    {
        GameObject enemySystem = GameObject.Find("EnemySystem");
        if (enemySystem == null) return null;
        return enemySystem.transform.Find("Enemies");
    }

    public static Enemy SummonEnemy(int EnemyID)
    {
        Enemy SummonedEnemy = null;

        if (EnemyPrefabs.ContainsKey(EnemyID))
        {
            Queue<Enemy> ReferencedQueue = EnemyObjectPools[EnemyID];

            if (ReferencedQueue.Count > 0)
            {
                SummonedEnemy = ReferencedQueue.Dequeue();
                SummonedEnemy.gameObject.SetActive(true);
                ApplyConsistentHealthScaling(SummonedEnemy, EnemyID);
                SummonedEnemy.Init();
            }
            else
            {
                Transform enemiesContainer = GetEnemiesContainer();
                GameObject NewEnemy;

                if (enemiesContainer != null)
                {
                    NewEnemy = Instantiate(EnemyPrefabs[EnemyID], GameLoopManager.NodePositions[0],
                                         Quaternion.identity, enemiesContainer);
                }
                else
                {
                    NewEnemy = Instantiate(EnemyPrefabs[EnemyID], GameLoopManager.NodePositions[0],
                                         Quaternion.identity);
                }

                SummonedEnemy = NewEnemy.GetComponent<Enemy>();
                ApplyConsistentHealthScaling(SummonedEnemy, EnemyID);
                SummonedEnemy.Init();
            }

            RegisterEnemySpawnForAI(SummonedEnemy, EnemyID);
            RegisterEnemySpeedForAI(SummonedEnemy, EnemyID);
        }
        else
        {
            Debug.LogError($"ENTITY SUMMONER: ENEMY WITH ID {EnemyID} DOES NOT EXIST!");
            return null;
        }

        InitializeNewEnemy(SummonedEnemy);
        if (!EnemiesInGame.Contains(SummonedEnemy))
            EnemiesInGame.Add(SummonedEnemy);
        if (!EnemiesInGameTransform.Contains(SummonedEnemy.transform))
            EnemiesInGameTransform.Add(SummonedEnemy.transform);
        if (!EnemyTransformPairs.ContainsKey(SummonedEnemy.transform))
            EnemyTransformPairs.Add(SummonedEnemy.transform, SummonedEnemy);

        SummonedEnemy.ID = EnemyID;
        return SummonedEnemy;
    }

    private static void InitializeNewEnemy(Enemy enemy)
    {
        if (enemy == null) return;

        WaveManager waveManager = FindObjectOfType<WaveManager>();
        PlayerManager playerManager = FindObjectOfType<PlayerManager>();

        if (waveManager != null && playerManager != null)
        {
            int currentWave = waveManager.GetCurrentWaveIndex();
            float playerHealthPercent = waveManager.GetCachedPlayerHealthForWave();

            enemy.InitializeForWave(currentWave, playerHealthPercent);
            enemy.MarkAsInitializedForWave();
        }
    }

    private static void RegisterEnemySpawnForAI(Enemy enemy, int enemyID)
    {
        WaveManager waveManager = FindObjectOfType<WaveManager>();
        if (waveManager != null && EnemyPerformanceTracker.Instance != null)
        {
            EnemyPerformanceTracker.Instance.RecordEnemySpawn(
                enemyID,
                waveManager.GetCurrentWaveIndex()
            );
        }
    }

    private static void RegisterEnemySpeedForAI(Enemy enemy, int enemyID)
    {
        if (EnemyPerformanceTracker.Instance != null)
        {
            EnemyPerformanceTracker.Instance.RecordEnemySpeed(enemyID, enemy.Speed);
        }
    }

    public static void RemoveEnemy(Enemy EnemyToRemove)
    {
        if (EnemyToRemove == null) return;

        if (EnemyObjectPools.ContainsKey(EnemyToRemove.ID))
        {
            if (!EnemyObjectPools[EnemyToRemove.ID].Contains(EnemyToRemove))
            {
                EnemyObjectPools[EnemyToRemove.ID].Enqueue(EnemyToRemove);
            }

            EnemyToRemove.gameObject.SetActive(false);
            EnemiesInGame.Remove(EnemyToRemove);
            EnemiesInGameTransform.Remove(EnemyToRemove.transform);
            EnemyTransformPairs.Remove(EnemyToRemove.transform);
        }
        else
        {
            Debug.LogError($"No se encontró pool para Enemy ID: {EnemyToRemove.ID}");
            Destroy(EnemyToRemove.gameObject);
        }
    }

    public static void ClearPools()
    {
        if (EnemyPrefabs != null) EnemyPrefabs.Clear();
        if (EnemyObjectPools != null)
        {
            foreach (var pool in EnemyObjectPools.Values)
            {
                while (pool.Count > 0)
                {
                    Enemy enemy = pool.Dequeue();
                    if (enemy != null && enemy.gameObject != null)
                    {
                        DestroyImmediate(enemy.gameObject);
                    }
                }
            }
            EnemyObjectPools.Clear();
        }
        if (EnemiesInGame != null) EnemiesInGame.Clear();
        if (EnemiesInGameTransform != null) EnemiesInGameTransform.Clear();
        if (EnemyTransformPairs != null) EnemyTransformPairs.Clear();

        IsInitialized = false;
    }

    public static void CleanOrphanedEnemies()
    {
        int orphanedCount = 0;
        List<Enemy> toRemove = new List<Enemy>();

        foreach (Enemy enemy in EnemiesInGame)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy || !enemy.IsAliveAndActive())
            {
                toRemove.Add(enemy);
                orphanedCount++;
            }
            else if (enemy.transform.position.y < -5f)
            {
                toRemove.Add(enemy);
                orphanedCount++;
            }
        }

        foreach (Enemy enemy in toRemove)
        {
            RemoveEnemy(enemy);
        }
    }
    private static void ApplyConsistentHealthScaling(Enemy enemy, int enemyID)
    {
        EnemyHealthManager healthManager = FindObjectOfType<EnemyHealthManager>();
        WaveManager waveManager = FindObjectOfType<WaveManager>();

        if (healthManager != null && waveManager != null)
        {
            int currentWaveIndex = waveManager.GetCurrentWaveIndex();
            GameObject originalPrefab = EnemyPrefabs[enemyID];
            Enemy originalEnemy = originalPrefab.GetComponent<Enemy>();
            float baseHealth = originalEnemy.MaxHealth;

            float playerHealthPercent = waveManager.GetCachedPlayerHealthForWave();

            float healthMultiplier = healthManager.GetHealthMultiplierForWave(currentWaveIndex, playerHealthPercent);

            enemy.MaxHealth = baseHealth * healthMultiplier;
            enemy.Health = enemy.MaxHealth;

        }
    }
}