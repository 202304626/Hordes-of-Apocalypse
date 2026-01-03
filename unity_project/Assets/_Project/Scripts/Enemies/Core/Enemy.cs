using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Enemy : MonoBehaviour
{
    [SerializeField] private int _nodeIndex;
    public Transform RootPart;
    public float DamageResistance = 1f;
    public float MaxHealth;
    public float Health;
    public float Speed;
    public int ID;
    public int goldValue = 10;
    public int baseGoldValue = 10;

    private int maxNodeReached = 0;
    private bool hasReportedDeath = false;
    private bool hasReportedSuccess = false;
    private bool isInitialized = false;
    private bool hasReachedEnd = false;
    private int initializedForWave = -1;

    public int NodeIndex
    {
        get => _nodeIndex;
        set
        {
            if (value != _nodeIndex && isInitialized)
            {
                _nodeIndex = value;
                UpdateMaxNodeReached();
                CheckForPathCompletion();
            }
        }
    }

    public void Init()
    {
        _nodeIndex = 0;
        maxNodeReached = 0;
        Health = MaxHealth;
        hasReportedDeath = false;
        hasReportedSuccess = false;
        isInitialized = true;
        hasReachedEnd = false;

        if (GameLoopManager.NodePositions != null && GameLoopManager.TotalNodes > 0)
        {
            transform.position = GameLoopManager.NodePositions[0];

            if (GameLoopManager.TotalNodes > 1)
            {
                Vector3 initialDirection = (GameLoopManager.NodePositions[1] - transform.position).normalized;
                if (initialDirection != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(initialDirection);
                }
            }
        }
        else
        {
            Debug.LogError($"❌ Enemy {ID}: No hay NodePositions disponibles!");
        }

        UpdateMaxNodeReached();
    }

    private void CheckForPathCompletion()
    {
        if (hasReachedEnd) return;

        if (_nodeIndex >= GameLoopManager.TotalNodes - 1)
        {
            if (HasReachedFinalNodeByPosition())
            {
                hasReachedEnd = true;
                OnReachedEnd();
            }
        }
    }

    private bool HasReachedFinalNodeByPosition()
    {
        if (GameLoopManager.NodePositions == null || GameLoopManager.TotalNodes == 0)
            return false;

        Vector3 finalNodePosition = GameLoopManager.NodePositions[GameLoopManager.LastNodeIndex];
        float distanceToFinalNode = Vector3.Distance(transform.position, finalNodePosition);

        return distanceToFinalNode < 0.1f;
    }

    private void UpdateMaxNodeReached()
    {
        if (_nodeIndex > maxNodeReached && _nodeIndex < GameLoopManager.NodePositions.Length)
        {
            maxNodeReached = _nodeIndex;
        }
    }

    public void OnDeath()
    {
        if (hasReportedDeath || !isInitialized || Health > 0)
        {
            return;
        }

        hasReportedDeath = true;

        ForceFinalNodeUpdate();

        if (EnemyPerformanceTracker.Instance != null)
        {
            EnemyPerformanceTracker.Instance.RecordEnemyProgress(ID, maxNodeReached);
        }

        GameEvents.TriggerEnemyDied(this);
        gameObject.SetActive(false);
        GameLoopManager.EnqueEnemyToRemove(this);
    }

    private void ForceFinalNodeUpdate()
    {
        int calculatedNode = CalculateCurrentNodeFromPosition();
        if (calculatedNode > _nodeIndex && calculatedNode < GameLoopManager.NodePositions.Length)
        {
            _nodeIndex = calculatedNode;
            UpdateMaxNodeReached();
        }
    }

    private int CalculateCurrentNodeFromPosition()
    {
        if (GameLoopManager.NodePositions == null || GameLoopManager.NodePositions.Length == 0)
            return 0;

        Vector3 currentPos = transform.position;
        int closestNode = 0;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < GameLoopManager.NodePositions.Length; i++)
        {
            float distance = Vector3.Distance(currentPos, GameLoopManager.NodePositions[i]);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestNode = i;
            }
        }

        return closestNode;
    }

    public void OnReachedEnd()
    {
        if (hasReportedSuccess || !isInitialized)
        {
            return;
        }

        hasReportedSuccess = true;

        maxNodeReached = GameLoopManager.LastNodeIndex;
        _nodeIndex = maxNodeReached;

        if (EnemyPerformanceTracker.Instance != null)
        {
            EnemyPerformanceTracker.Instance.RecordEnemyProgress(ID, maxNodeReached);
        }

        WaveManager waveManager = FindObjectOfType<WaveManager>();
        if (waveManager != null)
        {
            waveManager.OnEnemyReachedEnd(this);
        }

        if (waveManager != null && EnemyPerformanceTracker.Instance != null)
        {
            EnemyPerformanceTracker.Instance.RecordEnemyReachedEnd(ID, waveManager.GetCurrentWaveIndex());
        }

        PlayerManager playerManager = FindObjectOfType<PlayerManager>();
        if (playerManager != null && waveManager != null)
        {
            bool isBossWave = (waveManager.GetCurrentWaveIndex() == 14);
            bool isMostEffectiveEnemy = IsMostEffectiveEnemy();

            if (isBossWave && isMostEffectiveEnemy)
            {
                playerManager.TakeDamage(1);
            }
            else
            {
                playerManager.TakeDamage(1);
            }
        }

        GameEvents.TriggerEnemyReachedEnd(this);
        gameObject.SetActive(false);
    }

    private bool IsMostEffectiveEnemy()
    {
        if (EnemyPerformanceTracker.Instance == null) return false;

        var mostEffective = EnemyPerformanceTracker.Instance.GetMostEffectiveEnemies(1);
        return mostEffective.Count > 0 && mostEffective[0] == this.ID;
    }

    public void TakeDamage(float damage)
    {
        if (hasReportedDeath || !isInitialized || Health <= 0)
        {
            return;
        }

        float actualDamage = damage * DamageResistance;
        Health -= actualDamage;

        if (Health <= 0)
        {
            Health = 0;
            OnDeath();
        }
    }
    private void Update()
    {
        if (!isInitialized || hasReachedEnd || hasReportedDeath) return;

        if (!hasReachedEnd && _nodeIndex >= GameLoopManager.TotalNodes - 1)
        {
            if (HasReachedFinalNodeByPosition())
            {
                hasReachedEnd = true;
                OnReachedEnd();
            }
        }
    }

    public bool IsAliveAndActive()
    {
        return isInitialized && !hasReportedDeath && !hasReportedSuccess && gameObject.activeInHierarchy && Health > 0;
    }

    private void OnDisable()
    {
        isInitialized = false;
    }

    public void InitializeForWave(int waveIndex, float playerSuccessRate = 0.5f)
    {
        UpdateGoldValueForWave(waveIndex);
        MarkAsInitializedForWave();
    }

    private void UpdateGoldValueForWave(int waveIndex)
    {
        float healthIncrease = (float)MaxHealth / Health;
        float waveBonus = waveIndex * 0.02f;

        int newGoldValue = Mathf.RoundToInt(baseGoldValue * healthIncrease * (1f + waveBonus));
        goldValue = Mathf.Clamp(newGoldValue, baseGoldValue, baseGoldValue * 3);
    }

    public bool IsInitializedForCurrentWave()
    {
        WaveManager waveManager = FindObjectOfType<WaveManager>();
        if (waveManager != null)
        {
            return initializedForWave == waveManager.GetCurrentWaveIndex();
        }
        return false;
    }

    public void MarkAsInitializedForWave()
    {
        WaveManager waveManager = FindObjectOfType<WaveManager>();
        if (waveManager != null)
        {
            initializedForWave = waveManager.GetCurrentWaveIndex();
        }
    }

    public void ResetEnemy()
    {
        Health = MaxHealth;
        goldValue = baseGoldValue;
        initializedForWave = -1;
        NodeIndex = 0;

        if (TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        transform.rotation = Quaternion.identity;
    }
}