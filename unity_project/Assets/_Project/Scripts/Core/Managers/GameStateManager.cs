using System;
using UnityEngine;

public class GameStateManager : MonoBehaviour
{
    private bool areManagersInitialized = false;
    public bool AreManagersInitialized => areManagersInitialized;
    public static GameStateManager Instance { get; private set; }

    public enum GameState
    {
        Preparing,
        WaveInProgress,
        BetweenWaves,
        Victory,
        Defeat
    }

    [SerializeField] private GameState currentState = GameState.Preparing;

    [Header("Manager References")]
    public WaveManager waveManager;
    public PlayerManager playerManager;
    public EconomyManager economyManager;
    public TowerManager towerManager;
    public EnemyManager enemyManager;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            CheckForDuplicateManagers();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        GameEvents.ClearAllSubscriptions();
        InitializeManagers();
        ResetGame();
    }

    public void ChangeState(GameState newState)
    {
        if (currentState == GameState.Defeat || currentState == GameState.Victory)
        {
            return;
        }

        GameState previousState = currentState;
        currentState = newState;

        UIManager uiManager = FindObjectOfType<UIManager>();
        if (uiManager != null)
        {
            uiManager.OnGameStateChanged(newState);
        }
    }

    public bool IsGameActive()
    {
        return currentState == GameState.WaveInProgress ||
               currentState == GameState.BetweenWaves ||
               currentState == GameState.Preparing;
    }

    public void StartWave()
    {
        if (currentState == GameState.Preparing || currentState == GameState.BetweenWaves)
        {
            if (waveManager != null)
            {
                ChangeState(GameState.WaveInProgress);
                waveManager.StartNextWave();
            }
        }
    }

    public void OnWaveCompleted()
    {
        if (currentState == GameState.Defeat)
        {
            return;
        }

        if (waveManager.IsFinalWave())
        {
            ChangeState(GameState.Victory);
        }
        else
        {
            ChangeState(GameState.BetweenWaves);

            if (waveManager != null && !waveManager.IsInPreparation())
            {
                waveManager.StartPreparationPhase();
            }
        }
    }

    public void OnPlayerDefeated() => ChangeState(GameState.Defeat);
    public GameState GetCurrentState() => currentState;

    private void InitializeManagers()
    {
        if (areManagersInitialized) return;

        MLTrainingManager mlManager = FindObjectOfType<MLTrainingManager>();

        if (mlManager != null && !mlManager.IsInitialized())
        {
            Invoke("InitializeManagers", 0.5f);
            return;
        }

        economyManager?.InitializeEconomy();
        playerManager?.Initialize();
        towerManager?.Initialize();
        enemyManager?.Initialize();
        waveManager?.Initialize();

        areManagersInitialized = true;
    }

    public void ResetGame()
    {
        EntitySummoner.ClearPools();

        currentState = GameState.Preparing;
        areManagersInitialized = false;

        economyManager?.ResetEconomy();
        playerManager?.ResetPlayer();
        waveManager?.ResetWaves();
        towerManager?.ResetTowers();
        enemyManager?.ResetEnemies();

        // ✅ NUEVO: Resetear multiplicador de vida de IA
        MLWaveGeneratorAgent mlAgent = FindObjectOfType<MLWaveGeneratorAgent>();
        if (mlAgent != null)
        {
            mlAgent.ResetHealthMultiplier();
        }

        EntitySummoner.Init();
        InitializeManagers();

        if (waveManager != null) waveManager.enabled = true;
        if (enemyManager != null) enemyManager.enabled = true;
    }

    private void CheckForDuplicateManagers()
    {
        PlayerManager[] playerManagers = FindObjectsOfType<PlayerManager>();
        if (playerManagers.Length > 1)
        {
            for (int i = 1; i < playerManagers.Length; i++)
            {
                Destroy(playerManagers[i].gameObject);
            }
        }
    }
}