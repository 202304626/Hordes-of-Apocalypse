using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    [Header("Wave Configuration")]
    public List<WaveData> waves = new List<WaveData>();
    public float preparationTime = 10f;
    private float preparationTimer = 0f;
    private float lastPreparationUpdateTime = 0f;
    private bool preparationTimePaused = false;

    [Header("References")]
    public PlayerManager playerManager;
    public EconomyManager economyManager;

    [Header("Sistema de Dinero por Tiempo")]
    public float moneyPerSecondRate = 0.5f;
    public float minSecondsForMoney = 0f;
    private float waveStartTime;
    private Coroutine moneyPerSecondCoroutine;

    private int currentWaveIndex = -1;
    private bool isWaveActive = false;
    private bool isInPreparation = false;
    private Coroutine currentWaveCoroutine;
    private Coroutine preparationCoroutine;
    private List<Enemy> currentWaveEnemies = new List<Enemy>();

    public Action<int> OnWaveStarted;
    public Action<int> OnWaveCompleted;
    public Action<float> OnPreparationTimeUpdated;

    private int totalEnemiesInWave = 0;
    private int enemiesSpawned = 0;
    private int enemiesDefeated = 0;
    private int enemiesReachedEnd = 0;
    private bool allEnemiesSpawned = false;
    private bool waveCompletionChecked = false;

    [Header("ML-Agents Integration")]
    public MLTrainingManager mlTrainingManager;
    private bool useMLAgents = false;
    private bool mlAgentsInitialized = false;

    private int totalWaves = 15;

    private Dictionary<int, bool> iaWaveGenerated = new Dictionary<int, bool>();
    private Dictionary<int, bool> iaWaveRequested = new Dictionary<int, bool>();
    private Dictionary<int, WaveData> pendingMLWaves = new Dictionary<int, WaveData>();

    private float cachedPlayerHealthForWave = 0.5f;

    public void Initialize()
    {
        InitializeMLAgents();
        ResetWaves();
    }

    private void InitializeMLAgents()
    {
        if (mlTrainingManager == null)
        {
            mlTrainingManager = FindObjectOfType<MLTrainingManager>();
        }

        useMLAgents = (mlTrainingManager != null && mlTrainingManager.IsInitialized());
        mlAgentsInitialized = useMLAgents;

        if (useMLAgents)
        {
            for (int i = 0; i < totalWaves; i++)
            {
                iaWaveGenerated[i] = false;
                iaWaveRequested[i] = false;
            }
        }
    }

    private void UpdateMLAgentsStatus()
    {
        if (!mlAgentsInitialized && mlTrainingManager != null && mlTrainingManager.IsInitialized())
        {
            useMLAgents = true;
            mlAgentsInitialized = true;

            for (int i = 0; i < totalWaves; i++)
            {
                if (!iaWaveGenerated.ContainsKey(i)) iaWaveGenerated[i] = false;
                if (!iaWaveRequested.ContainsKey(i)) iaWaveRequested[i] = false;
            }
        }
    }

    private void Update()
    {
        if (GameStateManager.Instance?.GetCurrentState() == GameStateManager.GameState.Defeat ||
            GameStateManager.Instance?.GetCurrentState() == GameStateManager.GameState.Victory)
        {
            return;
        }

        if (!mlAgentsInitialized)
        {
            UpdateMLAgentsStatus();
        }

        CheckForWaveCompletion();
    }

    private void CheckForWaveCompletion()
    {
        if (!isWaveActive || waveCompletionChecked) return;

        CleanImmediateOrphans();

        bool canComplete = allEnemiesSpawned &&
                          currentWaveEnemies.Count == 0 &&
                          EntitySummoner.EnemiesInGame.Count == 0;

        if (canComplete)
        {
            waveCompletionChecked = true;
            CompleteCurrentWave();
        }
    }

    private void CleanImmediateOrphans()
    {
        if (!isWaveActive) return;

        currentWaveEnemies.RemoveAll(enemy =>
            enemy == null || !enemy.gameObject.activeInHierarchy || !enemy.IsAliveAndActive());
    }

    public void StartNextWave()
    {
        if (isWaveActive) return;

        if (playerManager != null)
        {
            cachedPlayerHealthForWave = (float)playerManager.GetCurrentLives() / playerManager.GetMaxLives();
        }

        EnemyManager enemyManager = FindObjectOfType<EnemyManager>();
        if (enemyManager != null)
        {
            enemyManager.VerifyEnemyConsistency();
        }

        if (currentWaveIndex >= totalWaves - 1)
        {
            GameEvents.TriggerVictory();
            return;
        }

        if (EntitySummoner.EnemiesInGame.Count > 0)
        {
            ForceCleanAllEnemies();
        }

        if (isInPreparation)
        {
            if (preparationCoroutine != null)
                StopCoroutine(preparationCoroutine);
            isInPreparation = false;
        }

        currentWaveIndex++;

        EnsureWaveExists(currentWaveIndex);
        ResetWaveTrackingVariables();
        isWaveActive = true;

        if (currentWaveCoroutine != null)
            StopCoroutine(currentWaveCoroutine);

        WaveData currentWave = GetCurrentWaveSafe();
        int totalEnemies = currentWave.enemyGroups.Sum(g => g.count);

        waveStartTime = Time.time;

        if (moneyPerSecondCoroutine != null)
            StopCoroutine(moneyPerSecondCoroutine);
        moneyPerSecondCoroutine = StartCoroutine(MoneyPerSecondDuringWave());

        OnWaveStarted?.Invoke(currentWaveIndex);
        GameEvents.TriggerWaveChanged(currentWaveIndex);

        currentWaveCoroutine = StartCoroutine(SpawnWaveCoroutine(currentWave));
    }

    private void EnsureWaveExists(int waveIndex)
    {
        while (waves.Count <= waveIndex)
        {
            waves.Add(null);
        }

        if (waves[waveIndex] == null)
        {
            if (waveIndex == 0)
            {
                waves[waveIndex] = CreateFixedWave1();
            }
            else if (waveIndex < 14)
            {
                if (useMLAgents && IsMLWaveGenerated(waveIndex))
                {
                    if (pendingMLWaves.ContainsKey(waveIndex))
                    {
                        waves[waveIndex] = pendingMLWaves[waveIndex];
                        pendingMLWaves.Remove(waveIndex);
               
                    }
                    else
                    {
                        waves[waveIndex] = CreateRobustFallbackWave(waveIndex);
                    }
                }
                else
                {
                    waves[waveIndex] = CreateRobustFallbackWave(waveIndex);
                }
            }
            else if (waveIndex == 14)
            {
                waves[waveIndex] = CreateEpicBossWave();
            }
            else
            {
                waves[waveIndex] = CreateRobustFallbackWave(waveIndex);
            }
        }
    }

    private WaveData GetCurrentWaveSafe()
    {
        if (currentWaveIndex < waves.Count && waves[currentWaveIndex] != null)
        {
            return waves[currentWaveIndex];
        }

        WaveData emergencyWave = CreateRobustFallbackWave(currentWaveIndex);

        while (waves.Count <= currentWaveIndex)
        {
            waves.Add(null);
        }
        waves[currentWaveIndex] = emergencyWave;

        return emergencyWave;
    }

    private void RequestMLWaveDuringPreparation(int waveIndex)
    {
        UpdateMLAgentsStatus();

        if (!useMLAgents || mlTrainingManager == null || waveIndex < 1 || waveIndex >= 14) return;

        if (!IsMLWaveRequested(waveIndex))
        {
            mlTrainingManager.RequestMLWaveGeneration(waveIndex);

            if (!iaWaveRequested.ContainsKey(waveIndex))
                iaWaveRequested.Add(waveIndex, true);
            else
                iaWaveRequested[waveIndex] = true;
        }
        else if (IsMLWaveGenerated(waveIndex) && pendingMLWaves.ContainsKey(waveIndex))
        {
            waves[waveIndex] = pendingMLWaves[waveIndex];
            pendingMLWaves.Remove(waveIndex);
        }
    }

    private WaveData CreateFixedWave1()
    {
        WaveData wave = new WaveData();
        wave.waveName = "Wave 1 - Tutorial Balanceado";
        wave.timeBetweenSpawns = 1.2f;
        wave.waveReward = 100;
        wave.timeLimit = 60f;
        wave.isBossWave = false;
        wave.enemyGroups = new List<EnemyGroup>();

        for (int i = 0; i < 3; i++)
        {
            EnemyGroup group = new EnemyGroup();
            group.enemyID = i + 1;
            group.count = 3;
            group.delayBeforeGroup = i * 1.5f;
            group.timeBetweenSpawnsInGroup = 1.0f;
            wave.enemyGroups.Add(group);
        }

        return wave;
    }

    private WaveData CreateRobustFallbackWave(int waveIndex)
    {
        WaveData wave = new WaveData();
        wave.waveName = $"Wave {waveIndex + 1} (Fallback)";
        wave.timeBetweenSpawns = Mathf.Max(0.8f, 1.2f - (waveIndex * 0.05f));
        wave.waveReward = CalculateFallbackReward(waveIndex);
        wave.timeLimit = 60f + (waveIndex * 12);
        wave.isBossWave = false;
        wave.enemyGroups = new List<EnemyGroup>();

        int totalEnemies = 10 + (waveIndex * 4);
        totalEnemies = Mathf.Clamp(totalEnemies, 10, 45);

        if (waveIndex < 5)
        {
            for (int i = 0; i < 3; i++)
            {
                EnemyGroup group = new EnemyGroup();
                group.enemyID = i + 1;
                group.count = totalEnemies / 3;
                group.delayBeforeGroup = i * 2f;
                group.timeBetweenSpawnsInGroup = wave.timeBetweenSpawns;
                wave.enemyGroups.Add(group);
            }
        }
        else if (waveIndex < 10)
        {
            EnemyGroup group1 = new EnemyGroup();
            group1.enemyID = 1;
            group1.count = Mathf.RoundToInt(totalEnemies * 0.4f);
            group1.delayBeforeGroup = 0f;
            group1.timeBetweenSpawnsInGroup = wave.timeBetweenSpawns;

            EnemyGroup group2 = new EnemyGroup();
            group2.enemyID = 2;
            group2.count = Mathf.RoundToInt(totalEnemies * 0.35f);
            group2.delayBeforeGroup = 3f;
            group2.timeBetweenSpawnsInGroup = wave.timeBetweenSpawns * 0.8f;

            EnemyGroup group3 = new EnemyGroup();
            group3.enemyID = 3;
            group3.count = totalEnemies - group1.count - group2.count;
            group3.delayBeforeGroup = 6f;
            group3.timeBetweenSpawnsInGroup = wave.timeBetweenSpawns * 1.2f;

            wave.enemyGroups.Add(group1);
            wave.enemyGroups.Add(group2);
            wave.enemyGroups.Add(group3);
        }
        else
        {
            EnemyGroup group1 = new EnemyGroup();
            group1.enemyID = 2;
            group1.count = Mathf.RoundToInt(totalEnemies * 0.5f);
            group1.delayBeforeGroup = 0f;
            group1.timeBetweenSpawnsInGroup = wave.timeBetweenSpawns * 0.7f;

            EnemyGroup group2 = new EnemyGroup();
            group2.enemyID = 3;
            group2.count = Mathf.RoundToInt(totalEnemies * 0.3f);
            group2.delayBeforeGroup = 4f;
            group2.timeBetweenSpawnsInGroup = wave.timeBetweenSpawns * 1.1f;

            EnemyGroup group3 = new EnemyGroup();
            group3.enemyID = 1;
            group3.count = totalEnemies - group1.count - group2.count;
            group3.delayBeforeGroup = 8f;
            group3.timeBetweenSpawnsInGroup = wave.timeBetweenSpawns;

            wave.enemyGroups.Add(group1);
            wave.enemyGroups.Add(group2);
            wave.enemyGroups.Add(group3);
        }

        int currentTotal = wave.enemyGroups.Sum(g => g.count);
        if (currentTotal != totalEnemies)
        {
            wave.enemyGroups[0].count += (totalEnemies - currentTotal);
        }

        return wave;
    }

    private int CalculateFallbackReward(int waveIndex)
    {
        int baseReward = 80 + (waveIndex * 15);
        return Mathf.Min(baseReward, 200);
    }

    private WaveData CreateEpicBossWave()
    {
        WaveData wave = new WaveData();
        wave.waveName = "OLEADA 15 - JEFE";
        wave.timeBetweenSpawns = 0.2f;
        wave.waveReward = 600;
        wave.timeLimit = 150f;
        wave.isBossWave = true;
        wave.enemyGroups = new List<EnemyGroup>();

        EnemyGroup phase1 = new EnemyGroup();
        phase1.enemyID = 2;
        phase1.count = 8;
        phase1.delayBeforeGroup = 0f;
        phase1.timeBetweenSpawnsInGroup = 0.1f;
        wave.enemyGroups.Add(phase1);

        EnemyGroup phase2 = new EnemyGroup();
        phase2.enemyID = 3;
        phase2.count = 6;
        phase2.delayBeforeGroup = 1f;
        phase2.timeBetweenSpawnsInGroup = 0.2f;
        wave.enemyGroups.Add(phase2);

        EnemyGroup phase3 = new EnemyGroup();
        phase3.enemyID = 1;
        phase3.count = 10;
        phase3.delayBeforeGroup = 2f;
        phase3.timeBetweenSpawnsInGroup = 0.1f;
        wave.enemyGroups.Add(phase3);

        EnemyGroup bossPhase = new EnemyGroup();
        bossPhase.enemyID = UnityEngine.Random.Range(4, 7);
        bossPhase.count = 1;
        bossPhase.delayBeforeGroup = 3f;
        bossPhase.timeBetweenSpawnsInGroup = 0f;
        wave.enemyGroups.Add(bossPhase);

        return wave;
    }

    private IEnumerator MoneyPerSecondDuringWave()
    {
        while (isWaveActive)
        {
            yield return new WaitForSeconds(1.0f);

            float waveDuration = Time.time - waveStartTime;

            if (waveDuration >= minSecondsForMoney)
            {
                int moneyToGive = CalculateDynamicMoneyPerSecond();
                if (moneyToGive > 0)
                {
                    EconomyManager.Instance?.ProcessTransaction(MoneyTransactionType.PassiveIncome,
                        "Tiempo_Oleada", moneyToGive);
                }
            }
        }
    }

    private int CalculateDynamicMoneyPerSecond()
    {
        float baseMoney = moneyPerSecondRate;
        float waveDuration = Time.time - waveStartTime;
        float durationFactor = Mathf.Clamp(waveDuration / 60f, 0.5f, 2.0f);

        float playerHealthPercent = playerManager != null ?
            (float)playerManager.GetCurrentLives() / playerManager.GetMaxLives() : 0.5f;

        float difficultyFactor = 1.0f + (1.0f - playerHealthPercent) * 0.5f;

        float finalMoney = baseMoney * durationFactor * difficultyFactor;
        return Mathf.Max(1, Mathf.RoundToInt(finalMoney));
    }

    private IEnumerator SpawnWaveCoroutine(WaveData wave)
    {
        if (wave.enemyGroups == null) yield break;

        totalEnemiesInWave = wave.enemyGroups.Sum(group => group.count);
        enemiesSpawned = 0;
        allEnemiesSpawned = false;

        foreach (var group in wave.enemyGroups)
        {
            if (group.delayBeforeGroup > 0f)
            {
                yield return new WaitForSeconds(group.delayBeforeGroup);
            }

            float spawnInterval = group.timeBetweenSpawnsInGroup > 0 ?
                                group.timeBetweenSpawnsInGroup :
                                wave.timeBetweenSpawns;

            for (int i = 0; i < group.count; i++)
            {
                Enemy enemy = EntitySummoner.SummonEnemy(group.enemyID);
                if (enemy != null)
                {
                    currentWaveEnemies.Add(enemy);
                    GameEvents.TriggerEnemySpawned(enemy);
                    enemiesSpawned++;
                }
                yield return new WaitForSeconds(spawnInterval);
            }
        }

        allEnemiesSpawned = true;
        CleanImmediateOrphans();

        if (currentWaveEnemies.Count == 0 && EntitySummoner.EnemiesInGame.Count == 0)
        {
            waveCompletionChecked = true;
            CompleteCurrentWave();
        }
    }

    private void CompleteCurrentWave()
    {
        if (!isWaveActive) return;
        if (GameStateManager.Instance?.GetCurrentState() == GameStateManager.GameState.Defeat) return;

        if (moneyPerSecondCoroutine != null)
        {
            StopCoroutine(moneyPerSecondCoroutine);
            moneyPerSecondCoroutine = null;
        }

        CleanImmediateOrphans();
        currentWaveEnemies.Clear();

        if (EconomyManager.Instance != null)
        {
            WaveData currentWave = GetCurrentWaveSafe();
            int reward = currentWave.waveReward;
            int passiveIncome = 8;

            EconomyManager.Instance.ProcessTransaction(MoneyTransactionType.WaveComplete,
                $"Wave_{currentWaveIndex + 1}", reward);

            if (passiveIncome > 0)
            {
                EconomyManager.Instance.ProcessTransaction(MoneyTransactionType.PassiveIncome,
                    "Passive_Income", passiveIncome);
            }
        }

        OnWaveCompleted?.Invoke(currentWaveIndex);
        GameStateManager.Instance?.OnWaveCompleted();

        if (useMLAgents && mlTrainingManager != null && playerManager != null &&
            currentWaveIndex >= 1 && currentWaveIndex < 14)
        {
            float playerHealthPercent = (float)playerManager.GetCurrentLives() / playerManager.GetMaxLives();
            mlTrainingManager.OnWaveCompleted(
                currentWaveIndex,
                enemiesReachedEnd,
                totalEnemiesInWave,
                playerHealthPercent
            );
        }

        isWaveActive = false;

        if (currentWaveIndex < totalWaves - 1)
        {
            StartPreparationPhase();
        }
        else
        {
            StartCoroutine(WaitForFinalVictory());
        }
    }

    public void StartPreparationPhase()
    {
        isInPreparation = true;
        preparationTimer = preparationTime;
        GameEvents.TriggerPreparationStarted();

        UpdateMLAgentsStatus();

        int nextWaveIndex = currentWaveIndex + 1;

        if (nextWaveIndex >= 1 && nextWaveIndex < 14)
        {
            RequestMLWaveDuringPreparation(nextWaveIndex);
        }

        preparationCoroutine = StartCoroutine(PreparationCoroutine());
    }

    private IEnumerator PreparationCoroutine()
    {
        preparationTimer = preparationTime;
        lastPreparationUpdateTime = Time.time;
        preparationTimePaused = false;

        while (preparationTimer > 0 && isInPreparation && !preparationTimePaused)
        {
            float currentTime = Time.time;
            float deltaTime = currentTime - lastPreparationUpdateTime;
            lastPreparationUpdateTime = currentTime;

            preparationTimer -= deltaTime;
            OnPreparationTimeUpdated?.Invoke(preparationTimer);

            yield return null;
        }

        if (preparationTimer <= 0 && isInPreparation)
        {
            StartNextWave();
        }
    }

    private IEnumerator WaitForFinalVictory()
    {
        yield return new WaitForSeconds(3f);
        GameEvents.TriggerVictory();
    }

    public void OnEnemyDied(Enemy enemy)
    {
        if (currentWaveEnemies.Contains(enemy))
        {
            currentWaveEnemies.Remove(enemy);
            enemiesDefeated++;

            if (allEnemiesSpawned && currentWaveEnemies.Count == 0 && EntitySummoner.EnemiesInGame.Count == 0)
            {
                if (!waveCompletionChecked)
                {
                    waveCompletionChecked = true;
                    CompleteCurrentWave();
                }
            }
        }
    }
    public void OnEnemyReachedEnd(Enemy enemy)
    {
        if (currentWaveEnemies.Contains(enemy))
        {
            currentWaveEnemies.Remove(enemy);
            enemiesReachedEnd++;

            if (allEnemiesSpawned && currentWaveEnemies.Count == 0 && EntitySummoner.EnemiesInGame.Count == 0)
            {
                if (!waveCompletionChecked)
                {
                    waveCompletionChecked = true;
                    CompleteCurrentWave();
                }
            }
        }
    }

    private void ForceCleanAllEnemies()
    {
        currentWaveEnemies.Clear();
        if (EntitySummoner.EnemiesInGame != null)
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
    }

    private void ResetWaveTrackingVariables()
    {
        currentWaveEnemies.Clear();
        totalEnemiesInWave = 0;
        enemiesSpawned = 0;
        enemiesDefeated = 0;
        enemiesReachedEnd = 0;
        allEnemiesSpawned = false;
        waveCompletionChecked = false;
    }

    public void RegisterMLWave(int waveIndex, WaveData mlWave)
    {
        if (waveIndex < 0 || waveIndex >= totalWaves) return;

        while (waves.Count <= waveIndex)
        {
            waves.Add(null);
        }

        pendingMLWaves[waveIndex] = mlWave;
        iaWaveGenerated[waveIndex] = true;

        if (isInPreparation && currentWaveIndex + 1 == waveIndex)
        {
            waves[waveIndex] = mlWave;
            pendingMLWaves.Remove(waveIndex);
        }
    }

    public void ResetWaves()
    {
        if (currentWaveCoroutine != null) StopCoroutine(currentWaveCoroutine);
        if (preparationCoroutine != null) StopCoroutine(preparationCoroutine);
        if (moneyPerSecondCoroutine != null) StopCoroutine(moneyPerSecondCoroutine);

        currentWaveIndex = -1;
        isWaveActive = false;
        isInPreparation = false;
        preparationTimer = 0f;

        ResetWaveTrackingVariables();
        ForceCleanAllEnemies();

        waves.Clear();
        waves.Add(CreateFixedWave1());
        pendingMLWaves.Clear();

        for (int i = 0; i < totalWaves; i++)
        {
            if (!iaWaveGenerated.ContainsKey(i))
                iaWaveGenerated.Add(i, false);
            else
                iaWaveGenerated[i] = false;

            if (!iaWaveRequested.ContainsKey(i))
                iaWaveRequested.Add(i, false);
            else
                iaWaveRequested[i] = false;
        }
    }

    public void MarkMLWaveAsGenerated(int waveIndex)
    {
        if (waveIndex < 0 || waveIndex >= totalWaves) return;

        if (!iaWaveGenerated.ContainsKey(waveIndex))
        {
            iaWaveGenerated.Add(waveIndex, true);
        }
        else
        {
            iaWaveGenerated[waveIndex] = true;
        }
    }

    public bool IsMLWaveGenerated(int waveIndex)
    {
        if (iaWaveGenerated.ContainsKey(waveIndex))
            return iaWaveGenerated[waveIndex];
        return false;
    }

    public bool IsMLWaveRequested(int waveIndex)
    {
        if (iaWaveRequested.ContainsKey(waveIndex))
            return iaWaveRequested[waveIndex];
        return false;
    }

    public bool IsWaveActive() => isWaveActive;
    public bool IsInPreparation() => isInPreparation;
    public int GetCurrentWaveIndex() => currentWaveIndex;
    public int GetTotalWaves() => totalWaves;
    public float GetPreparationTimeLeft() => preparationTimer;
    public bool IsFinalWave() => currentWaveIndex >= totalWaves - 1;
    public int GetCurrentWaveEnemyCount() => currentWaveEnemies.Count;
    public float GetCachedPlayerHealthForWave() => cachedPlayerHealthForWave;
}