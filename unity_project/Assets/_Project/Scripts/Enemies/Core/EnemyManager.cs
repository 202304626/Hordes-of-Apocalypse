using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    [Header("Configuración")]
    private float rotationSpeed = 10f;
    public static EnemyManager Instance { get; private set; }
    private bool isSubscribed = false;

    public void Initialize()
    {
        EntitySummoner.Init();
        SubscribeToEvents();
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        Initialize();
    }

    private void SubscribeToEvents()
    {
        if (isSubscribed) return;

        UnsubscribeFromEvents();
        GameEvents.OnEnemyDied += OnEnemyDied;
        GameEvents.OnRestartGame += OnRestartGame;
        isSubscribed = true;
    }

    private void OnRestartGame()
    {
        ResetEnemies();
    }

    private void UnsubscribeFromEvents()
    {
        GameEvents.OnEnemyDied -= OnEnemyDied;
        GameEvents.OnRestartGame -= OnRestartGame;
        isSubscribed = false;
    }

    public void Tick()
    {
        MoveEnemiesSimple();
    }

    private void MoveEnemiesSimple()
    {
        foreach (Enemy enemy in EntitySummoner.EnemiesInGame)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
            MoveSingleEnemy(enemy);
        }
    }

    private void MoveSingleEnemy(Enemy enemy)
    {
        if (enemy == null || !enemy.gameObject.activeInHierarchy || enemy.transform == null)
        {
            if (enemy != null && EntitySummoner.EnemiesInGame.Contains(enemy))
            {
                EntitySummoner.EnemiesInGame.Remove(enemy);
            }
            return;
        }

        if (!enemy.IsInitializedForCurrentWave())
        {
            InitializeEnemyForCurrentWave(enemy);
        }

        if (enemy.NodeIndex >= GameLoopManager.NodePositions.Length)
        {
            HandleEnemyReachedEnd(enemy);
            return;
        }

        Vector3 targetPosition = GameLoopManager.NodePositions[enemy.NodeIndex];
        Vector3 oldPosition = enemy.transform.position;

        enemy.transform.position = Vector3.MoveTowards(
            enemy.transform.position,
            targetPosition,
            enemy.Speed * Time.deltaTime
        );

        RotateEnemyTowardsMovement(enemy, oldPosition);

        float distanceToNode = Vector3.Distance(enemy.transform.position, targetPosition);
        if (distanceToNode < 0.1f)
        {
            int nextNode = enemy.NodeIndex + 1;

            if (nextNode >= GameLoopManager.NodePositions.Length)
            {
                HandleEnemyReachedEnd(enemy);
            }
            else
            {
                enemy.NodeIndex = nextNode;
                UpdateEnemyRotationForNextNode(enemy);
            }
        }
    }

    private void UpdateEnemyRotationForNextNode(Enemy enemy)
    {
        if (enemy.NodeIndex < GameLoopManager.NodePositions.Length - 1)
        {
            Vector3 nextNodePosition = GameLoopManager.NodePositions[enemy.NodeIndex + 1];
            Vector3 directionToNextNode = (nextNodePosition - enemy.transform.position).normalized;

            if (directionToNextNode != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToNextNode);
                enemy.transform.rotation = Quaternion.Slerp(
                    enemy.transform.rotation,
                    targetRotation,
                    Time.deltaTime * rotationSpeed
                );
            }
        }
    }

    private void RotateEnemyTowardsMovement(Enemy enemy, Vector3 oldPosition)
    {
        Vector3 movementDirection = (enemy.transform.position - oldPosition).normalized;
        if (movementDirection != Vector3.zero && movementDirection.magnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(movementDirection);
            enemy.transform.rotation = Quaternion.Slerp(
                enemy.transform.rotation,
                targetRotation,
                Time.deltaTime * rotationSpeed
            );
        }
    }

    private void HandleEnemyReachedEnd(Enemy enemy)
    {
        if (enemy == null) return;

        enemy.OnReachedEnd();
    }

    private void OnEnemyDied(Enemy enemy)
    {
        if (enemy == null) return;

        if (GameStateManager.Instance == null || !GameStateManager.Instance.IsGameActive())
        {
            return;
        }

        if (EconomyManager.Instance == null)
        {
            return;
        }

        try
        {
            int reward = EconomyManager.Instance.CalculateEnemyReward(enemy);

            bool success = EconomyManager.Instance.ProcessTransaction(
                MoneyTransactionType.EnemyKill,
                $"Enemy_{enemy.ID}_Kill",
                reward
            );

            if (success)
            {
                WaveManager waveManager = FindObjectOfType<WaveManager>();
                if (waveManager != null && EnemyPerformanceTracker.Instance != null)
                {
                    EnemyPerformanceTracker.Instance.RecordEnemyDefeated(
                        enemy.ID,
                        waveManager.GetCurrentWaveIndex(),
                        enemy.NodeIndex
                    );
                }
            }
            else
            {
                Debug.LogError($"❌ Transacción fallida para Enemy {enemy.ID}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"💥 ERROR en OnEnemyDied: {e.Message}");
        }
        finally
        {
            if (enemy != null)
            {
                if (EntitySummoner.EnemiesInGame.Contains(enemy))
                {
                    EntitySummoner.RemoveEnemy(enemy);
                }
            }
        }
    }

    public int GetActiveEnemyCount() => EntitySummoner.EnemiesInGame.Count;

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    public void ResetEnemies()
    {
        Transform enemiesContainer = EntitySummoner.GetEnemiesContainer();
        if (enemiesContainer != null)
        {
            foreach (Transform child in enemiesContainer)
            {
                if (child != null)
                {
                    Destroy(child.gameObject);
                }
            }
        }
        else
        {
            foreach (Enemy enemy in EntitySummoner.EnemiesInGame.ToList())
            {
                if (enemy != null)
                {
                    enemy.gameObject.SetActive(false);
                    EntitySummoner.RemoveEnemy(enemy);
                }
            }
        }

        EntitySummoner.EnemiesInGame.Clear();
        EntitySummoner.EnemiesInGameTransform.Clear();
        EntitySummoner.EnemyTransformPairs.Clear();
    }

    private void InitializeEnemyForCurrentWave(Enemy enemy)
    {
        if (enemy == null) return;

        WaveManager waveManager = FindObjectOfType<WaveManager>();
        PlayerManager playerManager = FindObjectOfType<PlayerManager>();

        if (waveManager != null && playerManager != null)
        {
            int currentWave = waveManager.GetCurrentWaveIndex();
            float playerHealthPercent = (float)playerManager.GetCurrentLives() / playerManager.GetMaxLives();
            float playerSuccessRate = playerHealthPercent;

            enemy.InitializeForWave(currentWave, playerSuccessRate);
        }
    }

    public void VerifyEnemyConsistency()
    {
        var enemiesByType = EntitySummoner.EnemiesInGame
            .Where(e => e != null && e.gameObject.activeInHierarchy)
            .GroupBy(e => e.ID);

        foreach (var group in enemiesByType)
        {
            int enemyID = group.Key;
            var uniqueHealthValues = group.Select(e => e.MaxHealth).Distinct().Count();

            if (uniqueHealthValues > 1)
            {
                Debug.LogError($"❌ INCONSISTENCIA: Enemigos tipo {enemyID} tienen {uniqueHealthValues} valores de vida diferentes!");

                foreach (var enemy in group)
                {
                    enemy.ResetEnemy();
                    InitializeEnemyForCurrentWave(enemy);
                }
            }
        }
    }
}