using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class MLWaveGeneratorAgent : Agent
{
    [Header("ML-Agents Configuration")]
    public WaveManager waveManager;
    public EnemyPerformanceTracker performanceTracker;
    public PlayerManager playerManager;
    public EconomyManager economyManager;

    [Header("Observation Settings")]
    public int maxWaves = 15;
    public int maxEnemyTypes = 3;
    public float maxMoney = 1000f;

    [Header("Health Scaling Configuration")]
    public float minHealthMultiplier = 0.4f;
    public float maxHealthMultiplier = 16f;
    public float healthAdjustmentSensitivity = 0.3f;

    private float currentHealthMultiplier = 1.0f;
    public int continuousActionsSize = 6;
    private bool isTraining = false;
    private float cachedMapDifficulty = 0.5f;
    private List<int> cachedProgressiveEnemies = new List<int>();
    private List<int> cachedSuccessfulEnemies = new List<int>();
    private float lastCacheUpdate = 0f;
    private int currentWaveIndex = 0;
    private int pendingWaveIndex = -1;
    private bool hasPendingWave = false;

    void Start()
    {
        InitializeReferences();
        PrecalculateStaticData();
    }

    private void InitializeReferences()
    {
        if (waveManager == null)
        {
            waveManager = FindObjectOfType<WaveManager>();
        }
        if (performanceTracker == null)
        {
            performanceTracker = EnemyPerformanceTracker.Instance;
        }
        if (playerManager == null)
        {
            playerManager = FindObjectOfType<PlayerManager>();
        }
        if (economyManager == null)
        {
            economyManager = EconomyManager.Instance;
        }
    }

    private void PrecalculateStaticData()
    {
        cachedMapDifficulty = CalculateMapDifficulty();
        UpdateCachedEnemyData();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (!IsReady())
        {
            return;
        }

        try
        {
            int waveIndexForObservations = hasPendingWave ? pendingWaveIndex : currentWaveIndex;

            sensor.AddObservation((float)waveIndexForObservations / maxWaves);
            sensor.AddObservation(GetPlayerHealthPercent());
            sensor.AddObservation(GetPlayerMoneyPercent());
            sensor.AddObservation(performanceTracker.GetCurrentGlobalSuccessRate());

            foreach (var enemyStat in performanceTracker.globalEnemyStats)
            {
                sensor.AddObservation(enemyStat.WeightedProgressRate);
                sensor.AddObservation(enemyStat.AverageMaxNode / GameLoopManager.LastNodeIndex);
                sensor.AddObservation(enemyStat.SuccessRate);
                sensor.AddObservation(enemyStat.AverageSpeed / 5f);
            }

            for (int i = performanceTracker.globalEnemyStats.Count; i < maxEnemyTypes; i++)
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
            }

            var progressiveEnemies = GetProgressiveEnemies(maxEnemyTypes);
            for (int i = 0; i < maxEnemyTypes; i++)
            {
                sensor.AddObservation(i < progressiveEnemies.Count ? (float)progressiveEnemies[i] / maxEnemyTypes : 0f);
            }

            sensor.AddObservation(cachedMapDifficulty);
            sensor.AddObservation(currentHealthMultiplier / maxHealthMultiplier);
            sensor.AddObservation(CalculateEnemyProgressPressure());
            sensor.AddObservation(GetPlayerKillingEfficiency());

            foreach (var enemyStat in performanceTracker.globalEnemyStats)
            {
                sensor.AddObservation(enemyStat.WeightedProgressRate);
                sensor.AddObservation(enemyStat.AverageMaxNode / GameLoopManager.LastNodeIndex);
            }
        }
        catch (System.Exception e)
        {
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!IsReady())
        {
            return;
        }

        int waveIndexToUse = hasPendingWave ? pendingWaveIndex : currentWaveIndex;

        if (waveIndexToUse < 1 || waveIndexToUse >= 14)
        {
            hasPendingWave = false;
            pendingWaveIndex = -1;
            return;
        }

        try
        {
            float[] continuousActions = actions.ContinuousActions.Array;
            float[] enemyProportions = new float[3];

            for (int i = 0; i < 3 && i < continuousActions.Length; i++)
            {
                enemyProportions[i] = Mathf.Clamp01(continuousActions[i]);
            }

            float waveDensity = continuousActions.Length > 3 ? Mathf.Clamp01(continuousActions[3]) : 0.6f;
            float spawnSpeed = continuousActions.Length > 4 ? Mathf.Clamp01(continuousActions[4]) : 0.5f;

            float healthAdjustment = 0f;
            if (continuousActions.Length > 5)
            {
                healthAdjustment = Mathf.Clamp(continuousActions[5], -1f, 1f);
                UpdateHealthMultiplier(healthAdjustment, waveIndexToUse);
            }
            else
            {
                healthAdjustment = CalculateFallbackHealthAdjustment(waveIndexToUse);
            }

            ApplyProgressiveStrategy(ref enemyProportions, ref waveDensity, ref spawnSpeed, waveIndexToUse);

            WaveData mlWave = GenerateMixedGroupsWave(enemyProportions, waveDensity, spawnSpeed, waveIndexToUse);

            if (mlWave != null)
            {
                MLWaveInjector.InjectWave(mlWave, waveIndexToUse);
                AddReward(0.1f);

                LogWaveGeneration(mlWave, enemyProportions, waveDensity);

                if (waveManager != null)
                {
                    waveManager.MarkMLWaveAsGenerated(waveIndexToUse);
                }
            }
        }
        catch (System.Exception e)
        {
            AddReward(-0.5f);
        }
        finally
        {
            hasPendingWave = false;
            pendingWaveIndex = -1;
        }
    }

    private float CalculateFallbackHealthAdjustment(int waveIndex)
    {
        float progressPressure = CalculateEnemyProgressPressure();
        float killingEfficiency = GetPlayerKillingEfficiency();

        if (waveIndex >= 5 && waveIndex <= 10)
        {
            if (progressPressure > 0.6f)
                return 0.8f;
            else if (progressPressure > 0.4f)
                return 0.5f;
            else
                return 0.3f;
        }

        return 0.2f;
    }

    private void UpdateHealthMultiplier(float adjustment, int waveIndex)
    {
        float progressPressure = CalculateEnemyProgressPressure();
        float killingEfficiency = GetPlayerKillingEfficiency();
        float phaseAggressiveness = GetPhaseAggressiveness(waveIndex);

        float baseAdjustment = adjustment * healthAdjustmentSensitivity * phaseAggressiveness;
        currentHealthMultiplier += baseAdjustment;

        if (progressPressure > 0.7f)
        {
            currentHealthMultiplier += 0.05f * phaseAggressiveness;
        }
        else if (progressPressure > 0.5f)
        {
            currentHealthMultiplier += 0.02f * phaseAggressiveness;
        }

        if (killingEfficiency > 0.8f)
        {
            currentHealthMultiplier += 0.03f * phaseAggressiveness;
        }

        float effectiveMaxMultiplier = GetProgressiveMaxMultiplier(waveIndex);
        currentHealthMultiplier = Mathf.Clamp(currentHealthMultiplier, minHealthMultiplier, effectiveMaxMultiplier);
    }

    private float GetProgressiveMaxMultiplier(int waveIndex)
    {
        if (waveIndex < 3) return 2f;
        if (waveIndex < 6) return 3.0f;
        if (waveIndex < 9) return 6f;
        if (waveIndex < 12) return 12f;
        return 15f;
    }

    private float GetPhaseAggressiveness(int waveIndex)
    {
        if (waveIndex < 3)
            return 0.1f;
        else if (waveIndex < 6)
            return 0.3f;
        else if (waveIndex < 9)
            return 0.5f;
        else if (waveIndex < 12)
            return 1f;
        else
            return 1.8f;
    }

    private float CalculateEnemyProgressPressure()
    {
        if (performanceTracker == null) return 0.5f;

        float totalProgress = 0f;
        int validEnemies = 0;

        foreach (var enemyStat in performanceTracker.globalEnemyStats)
        {
            if (enemyStat.analysisSamples >= performanceTracker.minSamplesForReliability)
            {
                float progressScore = (enemyStat.WeightedProgressRate * 0.7f) +
                                    (enemyStat.RecentWeightedProgress * 0.3f);
                totalProgress += progressScore;
                validEnemies++;
            }
        }

        return validEnemies > 0 ? totalProgress / validEnemies : 0.5f;
    }

    private float GetPlayerKillingEfficiency()
    {
        if (performanceTracker == null) return 0.5f;

        int totalSpawned = performanceTracker.globalEnemyStats.Sum(e => e.timesSpawned);
        int totalKilled = totalSpawned - performanceTracker.globalEnemyStats.Sum(e => e.timesReachedEnd);

        return totalSpawned > 0 ? (float)totalKilled / totalSpawned : 0.5f;
    }

    public float GetCurrentHealthMultiplier()
    {
        return currentHealthMultiplier;
    }

    private void ApplyProgressiveStrategy(ref float[] enemyProportions, ref float density, ref float spawnSpeed, int waveIndex)
    {
        if (Time.time - lastCacheUpdate > 3f)
        {
            UpdateCachedEnemyData();
            lastCacheUpdate = Time.time;
        }

        float phaseMultiplier = GetPhaseMultiplier(waveIndex);
        float playerPressure = CalculatePlayerPressure();
        float mapAdaptation = GetMapAdaptationFactor();

        if (waveIndex < 4)
        {
            ApplyEarlyGameProgressiveStrategy(ref enemyProportions, ref density, ref spawnSpeed, phaseMultiplier, playerPressure, mapAdaptation);
        }
        else if (waveIndex < 9)
        {
            ApplyMidGameProgressiveStrategy(ref enemyProportions, ref density, ref spawnSpeed, phaseMultiplier, playerPressure, mapAdaptation);
        }
        else
        {
            ApplyLateGameProgressiveStrategy(ref enemyProportions, ref density, ref spawnSpeed, phaseMultiplier, playerPressure, mapAdaptation);
        }

        ApplyProgressiveSynergies(ref enemyProportions, waveIndex, mapAdaptation);
        NormalizeProportions(ref enemyProportions);
    }

    private void ApplyEarlyGameProgressiveStrategy(ref float[] proportions, ref float density, ref float speed, float multiplier, float pressure, float mapAdaptation)
    {
        density = Mathf.Clamp(0.5f + (multiplier * 0.3f) + (pressure * 0.2f), 0.4f, 0.7f);
        speed = Mathf.Clamp(0.5f + (pressure * 0.3f), 0.4f, 0.7f);

        if (mapAdaptation > 0.6f && cachedProgressiveEnemies.Count > 0)
        {
            int bestProgressive = cachedProgressiveEnemies[0] - 1;
            proportions[bestProgressive] *= 1.4f + (pressure * 0.3f);
        }
    }

    private void ApplyMidGameProgressiveStrategy(ref float[] proportions, ref float density, ref float speed, float multiplier, float pressure, float mapAdaptation)
    {
        density = Mathf.Clamp(0.7f + (multiplier * 0.4f) + (pressure * 0.3f), 0.6f, 0.9f);
        speed = Mathf.Clamp(0.6f + (pressure * 0.4f), 0.5f, 0.8f);

        var progressive = cachedProgressiveEnemies.Take(2).ToList();
        var successful = cachedSuccessfulEnemies.Take(1).ToList();

        foreach (int enemyID in progressive)
        {
            int index = enemyID - 1;
            proportions[index] *= 1.6f + (pressure * 0.4f);
        }

        if (mapAdaptation > 0.7f)
        {
            var fastest = GetFastestEnemies(1);
            if (fastest.Count > 0)
            {
                int fastIndex = fastest[0] - 1;
                proportions[fastIndex] *= 1.3f;
            }
        }
    }

    private void ApplyLateGameProgressiveStrategy(ref float[] proportions, ref float density, ref float speed, float multiplier, float pressure, float mapAdaptation)
    {
        density = Mathf.Clamp(0.8f + (multiplier * 0.5f) + (pressure * 0.4f), 0.7f, 1.0f);
        speed = Mathf.Clamp(0.7f + (pressure * 0.5f), 0.6f, 0.9f);

        var progressive = cachedProgressiveEnemies.Take(3).ToList();

        foreach (int enemyID in progressive)
        {
            int index = enemyID - 1;
            float boost = 1.8f + (pressure * 0.5f) + (mapAdaptation * 0.3f);
            proportions[index] *= boost;
        }

        if (mapAdaptation > 0.8f)
        {
            proportions[2] *= 1.4f;
        }
    }

    private void ApplyProgressiveSynergies(ref float[] proportions, int waveIndex, float mapAdaptation)
    {
        if (waveIndex > 3 && mapAdaptation > 0.6f)
        {
            float synergyBonus = Mathf.Lerp(0.1f, 0.4f, mapAdaptation);
            if (cachedProgressiveEnemies.Contains(2))
            {
                proportions[1] *= (1f + synergyBonus);
            }
        }

        if (waveIndex > 7 && cachedProgressiveEnemies.Contains(3))
        {
            proportions[2] *= 1.3f;
        }
    }

    private WaveData GenerateMixedGroupsWave(float[] enemyProportions, float density, float spawnSpeed, int waveIndex)
    {
        if (waveIndex < 0) return null;

        WaveData wave = new WaveData();
        wave.waveName = $"Oleada IA #{waveIndex + 1} 🧠";
        wave.timeBetweenSpawns = 1.6f - (spawnSpeed * 1.4f);
        wave.waveReward = CalculateMLWaveReward(waveIndex);
        wave.timeLimit = 65f + (waveIndex * 15);
        wave.isBossWave = false;
        wave.enemyGroups = new List<EnemyGroup>();

        int baseEnemies = CalculateBaseEnemies(waveIndex);
        int totalEnemies = Mathf.RoundToInt(baseEnemies * (0.7f + density * 0.6f));
        totalEnemies = Mathf.Clamp(totalEnemies, 8, 25 + (waveIndex * 2));

        CreateMixedEnemyGroups(wave, enemyProportions, totalEnemies, waveIndex, spawnSpeed);

        return wave;
    }

    private int CalculateMLWaveReward(int waveIndex)
    {
        int baseReward = 90 + (waveIndex * 20);
        float difficultyBonus = 0f;
        if (cachedProgressiveEnemies.Count > 0)
        {
            difficultyBonus = 0.3f;
        }

        int finalReward = Mathf.RoundToInt(baseReward * (1f + difficultyBonus));
        return Mathf.Clamp(finalReward, 100, 300);
    }

    private int CalculateBaseEnemies(int waveIndex)
    {
        int baseEnemies = 8 + (waveIndex * 3);

        if (waveIndex > 10)
        {
            baseEnemies += (waveIndex - 10) * 4;
        }
        else if (waveIndex > 5)
        {
            baseEnemies += (waveIndex - 5) * 2;
        }

        float mapFactor = GetMapAdaptationFactor();
        if (mapFactor > 0.7f)
        {
            baseEnemies = Mathf.RoundToInt(baseEnemies * 0.9f);
        }
        else if (mapFactor < 0.4f)
        {
            baseEnemies = Mathf.RoundToInt(baseEnemies * 1.1f);
        }

        return Mathf.Max(8, baseEnemies);
    }

    private void CreateMixedEnemyGroups(WaveData wave, float[] proportions, int totalEnemies, int waveIndex, float spawnSpeed)
    {
        List<EnemyGroup> allGroups = new List<EnemyGroup>();
        int remainingEnemies = totalEnemies;

        int[] enemiesPerType = new int[3];
        for (int i = 0; i < 3; i++)
        {
            if (proportions[i] > 0.05f)
            {
                enemiesPerType[i] = Mathf.RoundToInt(totalEnemies * proportions[i]);
                enemiesPerType[i] = Mathf.Clamp(enemiesPerType[i], 0, remainingEnemies);
                remainingEnemies -= enemiesPerType[i];
            }
        }

        if (remainingEnemies > 0)
        {
            for (int i = 0; i < remainingEnemies; i++)
            {
                if (cachedProgressiveEnemies.Count > 0)
                {
                    int bestType = cachedProgressiveEnemies[0] - 1;
                    enemiesPerType[bestType]++;
                }
                else
                {
                    enemiesPerType[0]++;
                }
            }
        }

        int groupSize = CalculateGroupSize(waveIndex);
        float baseSpawnInterval = wave.timeBetweenSpawns;

        for (int type = 0; type < 3; type++)
        {
            if (enemiesPerType[type] > 0)
            {
                CreateMixedGroupsForType(allGroups, type + 1, enemiesPerType[type], groupSize, baseSpawnInterval, spawnSpeed);
            }
        }

        wave.enemyGroups = StrategicGroupOrdering(allGroups, waveIndex);
    }

    private void CreateMixedGroupsForType(List<EnemyGroup> groups, int enemyID, int totalCount, int groupSize, float baseInterval, float spawnSpeed)
    {
        int groupsNeeded = Mathf.CeilToInt((float)totalCount / groupSize);

        for (int i = 0; i < groupsNeeded; i++)
        {
            EnemyGroup group = new EnemyGroup();
            group.enemyID = enemyID;
            group.count = Mathf.Min(groupSize, totalCount - (i * groupSize));
            group.delayBeforeGroup = i * 0.5f;
            group.timeBetweenSpawnsInGroup = CalculateDynamicSpawnInterval(baseInterval, spawnSpeed, enemyID);

            groups.Add(group);
        }
    }

    private List<EnemyGroup> StrategicGroupOrdering(List<EnemyGroup> groups, int waveIndex)
    {
        var orderedGroups = new List<EnemyGroup>();
        var groupsByType = groups.GroupBy(g => g.enemyID).ToDictionary(g => g.Key, g => g.ToList());

        int maxGroups = groupsByType.Values.Max(list => list.Count);

        for (int i = 0; i < maxGroups; i++)
        {
            foreach (var type in groupsByType.Keys.OrderBy(k => Random.Range(0, 100)))
            {
                if (i < groupsByType[type].Count)
                {
                    var group = groupsByType[type][i];
                    group.delayBeforeGroup = i * 1.2f;
                    orderedGroups.Add(group);
                }
            }
        }

        return orderedGroups;
    }

    private int CalculateGroupSize(int waveIndex)
    {
        if (waveIndex < 3) return 3;
        if (waveIndex < 7) return 2;
        return 2;
    }

    private float CalculateDynamicSpawnInterval(float baseInterval, float spawnSpeed, int enemyID)
    {
        float speedFactor = 1f - (spawnSpeed * 0.6f);
        float typeFactor = GetEnemyTypeSpawnFactor(enemyID);
        return baseInterval * speedFactor * typeFactor;
    }

    private float GetEnemyTypeSpawnFactor(int enemyID)
    {
        switch (enemyID)
        {
            case 1: return 1.0f;
            case 2: return 0.8f;
            case 3: return 1.3f;
            default: return 1.0f;
        }
    }

    public void EvaluateWavePerformance(int waveIndex, int enemiesReachedEnd, int totalEnemies, float playerHealthPercent)
    {
        if (!IsReady()) return;

        try
        {
            float successRate = totalEnemies > 0 ? (float)enemiesReachedEnd / totalEnemies : 0f;
            float reward = CalculateProfessionalReward(successRate, playerHealthPercent, totalEnemies, waveIndex);

            AddReward(reward);

            if (isTraining) EndEpisode();
        }
        catch (System.Exception e)
        {
        }
    }

    private float CalculateProfessionalReward(float successRate, float playerHealth, int totalEnemies, int waveIndex)
    {
        float reward = 0f;

        if (successRate >= 0.25f && successRate <= 0.65f)
        {
            reward += 2.5f;

            if (playerHealth >= 0.25f && playerHealth <= 0.75f)
            {
                reward += 1.5f;
            }

            if (totalEnemies > 20 && successRate > 0.3f)
            {
                reward += 1.2f;
            }

            if (waveIndex > 5 && successRate > 0.35f)
            {
                reward += 0.8f;
            }
        }

        if (successRate < 0.1f) reward -= 2.0f;
        if (successRate > 0.9f) reward -= 2.5f;
        if (playerHealth < 0.1f) reward -= 1.5f;
        if (totalEnemies < 8) reward -= 1.0f;

        return reward;
    }

    private float GetPlayerHealthPercent()
    {
        return playerManager != null ? (float)playerManager.GetCurrentLives() / playerManager.GetMaxLives() : 0.5f;
    }

    private float GetPlayerMoneyPercent()
    {
        return economyManager != null ?
            Mathf.Clamp01(economyManager.CurrentMoney / maxMoney) : 0.5f;
    }

    private float CalculatePlayerPressure()
    {
        float healthPressure = 1f - GetPlayerHealthPercent();
        float moneyPressure = 1f - GetPlayerMoneyPercent();
        return (healthPressure * 0.7f) + (moneyPressure * 0.3f);
    }

    private float GetPhaseMultiplier(int waveIndex)
    {
        return Mathf.Clamp01((float)waveIndex / 14f);
    }

    private float CalculateMapDifficulty()
    {
        if (GameLoopManager.NodePositions == null || GameLoopManager.NodePositions.Length < 3)
            return 0.5f;

        int totalNodes = GameLoopManager.NodePositions.Length;
        float lengthFactor = Mathf.Clamp01((totalNodes - 8f) / 20f);

        float complexity = 0f;
        for (int i = 1; i < GameLoopManager.NodePositions.Length - 1; i++)
        {
            Vector3 prevDir = (GameLoopManager.NodePositions[i] - GameLoopManager.NodePositions[i-1]).normalized;
            Vector3 nextDir = (GameLoopManager.NodePositions[i+1] - GameLoopManager.NodePositions[i]).normalized;
            float angle = Vector3.Angle(prevDir, nextDir);
            complexity += angle > 30f ? 1f : 0f;
        }

        float complexityFactor = Mathf.Clamp01(complexity / (GameLoopManager.NodePositions.Length - 2));

        return (lengthFactor * 0.6f) + (complexityFactor * 0.4f);
    }

    private float GetMapAdaptationFactor()
    {
        float difficulty = cachedMapDifficulty;
        float pathLength = (float)GameLoopManager.LastNodeIndex / 20f;
        return (difficulty * 0.6f) + (pathLength * 0.4f);
    }

    private void UpdateCachedEnemyData()
    {
        cachedProgressiveEnemies = GetProgressiveEnemies(3);
        cachedSuccessfulEnemies = GetSuccessfulEnemies(3);
    }

    private List<int> GetProgressiveEnemies(int count)
    {
        if (performanceTracker != null)
        {
            try
            {
                return performanceTracker.GetMostProgressiveEnemies(count);
            }
            catch (System.Exception e)
            {
            }
        }
        return new List<int> { 1, 2, 3 }.Take(count).ToList();
    }

    private List<int> GetSuccessfulEnemies(int count)
    {
        if (performanceTracker != null)
        {
            try
            {
                return performanceTracker.GetMostSuccessfulEnemies(count);
            }
            catch (System.Exception e)
            {
            }
        }
        return new List<int> { 1, 2, 3 }.Take(count).ToList();
    }

    private List<int> GetFastestEnemies(int count)
    {
        if (performanceTracker != null)
        {
            try
            {
                return performanceTracker.GetFastestEnemies(count);
            }
            catch (System.Exception e)
            {
            }
        }
        return new List<int> { 2, 1, 3 }.Take(count).ToList();
    }

    private void NormalizeProportions(ref float[] proportions)
    {
        float total = proportions.Sum();
        if (total > 0f)
        {
            for (int i = 0; i < proportions.Length; i++)
            {
                proportions[i] /= total;
            }
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;

        if (!IsReady())
        {
            SetDefaultActions(continuousActions);
            return;
        }

        float[] proportions = CalculateProgressiveHeuristicProportions();
        float density = CalculateHeuristicDensity();
        float speed = CalculateHeuristicSpeed();

        for (int i = 0; i < proportions.Length && i < 3; i++)
        {
            continuousActions[i] = proportions[i];
        }

        if (continuousActions.Length > 3)
        {
            continuousActions[3] = density;
        }

        if (continuousActions.Length > 4)
        {
            continuousActions[4] = speed;
        }

        if (continuousActions.Length > 5)
        {
            float progressPressure = CalculateEnemyProgressPressure();
            int currentWave = hasPendingWave ? pendingWaveIndex : currentWaveIndex;

            float healthAdjustment = 0f;

            if (currentWave < 3)
            {
                if (progressPressure > 0.6f)
                    healthAdjustment = 0.1f;
                else
                    healthAdjustment = 0.05f;
            }
            else if (currentWave < 6)
            {
                if (progressPressure > 0.6f)
                    healthAdjustment = 0.2f;
                else if (progressPressure > 0.4f)
                    healthAdjustment = 0.1f;
                else
                    healthAdjustment = 0.05f;
            }
            else if (currentWave < 9)
            {
                if (progressPressure > 0.6f)
                    healthAdjustment = 0.4f;
                else if (progressPressure > 0.4f)
                    healthAdjustment = 0.2f;
                else
                    healthAdjustment = 0.1f;
            }
            else
            {
                if (progressPressure > 0.6f)
                    healthAdjustment = 0.7f;
                else if (progressPressure > 0.4f)
                    healthAdjustment = 0.4f;
                else
                    healthAdjustment = 0.2f;
            }

            continuousActions[5] = healthAdjustment;
        }
    }

    private float[] CalculateProgressiveHeuristicProportions()
    {
        float[] proportions = new float[3];

        if (performanceTracker == null)
        {
            return new float[] { 0.33f, 0.33f, 0.34f };
        }

        float[] effectivenessScores = new float[3];
        float totalEffectiveness = 0f;

        for (int i = 0; i < 3; i++)
        {
            int enemyID = i + 1;
            float effectiveness = performanceTracker.GetEnemyEffectiveness(enemyID);
            effectivenessScores[i] = Mathf.Max(0.1f, effectiveness);
            totalEffectiveness += effectivenessScores[i];
        }

        if (totalEffectiveness > 0)
        {
            for (int i = 0; i < 3; i++)
            {
                proportions[i] = effectivenessScores[i] / totalEffectiveness;
            }
        }
        else
        {
            proportions = new float[] { 0.33f, 0.33f, 0.34f };
        }

        var progressive = GetProgressiveEnemies(1);
        if (progressive.Count > 0 && currentWaveIndex > 5)
        {
            int bestIndex = progressive[0] - 1;
            float boost = 1.0f + (currentWaveIndex * 0.03f);
            proportions[bestIndex] *= Mathf.Min(boost, 1.5f);
        }

        NormalizeProportions(ref proportions);
        return proportions;
    }

    private float CalculateHeuristicDensity()
    {
        float baseDensity = Mathf.Lerp(0.4f, 0.7f, (float)currentWaveIndex / 14f);
        float pressureBonus = CalculatePlayerPressure() * 0.2f;
        float mapFactor = GetMapAdaptationFactor() * 0.1f;

        return Mathf.Clamp(baseDensity + pressureBonus - mapFactor, 0.3f, 0.8f);
    }

    private float CalculateHeuristicSpeed()
    {
        float baseSpeed = Mathf.Lerp(0.4f, 0.7f, (float)currentWaveIndex / 14f);
        float mapFactor = GetMapAdaptationFactor();

        if (mapFactor > 0.6f)
        {
            baseSpeed += 0.1f;
        }

        return Mathf.Clamp(baseSpeed, 0.4f, 0.8f);
    }

    private void SetDefaultActions(ActionSegment<float> actions)
    {
        if (actions.Length >= 5)
        {
            actions[0] = 0.33f;
            actions[1] = 0.33f;
            actions[2] = 0.34f;
            actions[3] = 0.6f;
            actions[4] = 0.5f;
        }
    }

    private void LogWaveGeneration(WaveData wave, float[] proportions, float density)
    {
        int totalEnemies = wave.enemyGroups.Sum(g => g.count);
        int uniqueTypes = wave.enemyGroups.Select(g => g.enemyID).Distinct().Count();
    }

    public void RequestWaveGeneration(int waveIndex)
    {
        if (!IsReady())
        {
            return;
        }

        if (waveIndex < 1 || waveIndex >= 14)
        {
            return;
        }

        pendingWaveIndex = waveIndex;
        hasPendingWave = true;

        RequestDecision();
    }

    public bool IsReady()
    {
        bool ready = waveManager != null && performanceTracker != null &&
               playerManager != null && economyManager != null;

        return ready;
    }

    public void SetTrainingMode(bool training)
    {
        isTraining = training;
    }

    public void ResetHealthMultiplier()
    {
        currentHealthMultiplier = 1.0f;
    }
}