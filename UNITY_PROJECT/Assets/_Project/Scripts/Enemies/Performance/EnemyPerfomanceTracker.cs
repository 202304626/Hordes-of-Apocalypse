using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EnemyPerformanceTracker : MonoBehaviour
{
    public static EnemyPerformanceTracker Instance;

    [System.Serializable]
    public class EnemyPerformanceData
    {
        public int enemyID;
        public int timesSpawned = 0;
        public int timesReachedEnd = 0;
        public int totalMaxNodesReached = 0;
        public int analysisSamples = 0;

        public List<int> nodesReachedCount = new List<int>();

        public float totalSpeed = 0f;
        public int speedSamples = 0;
        public float averageSpeed = 0f;

        public float SuccessRate => timesSpawned > 0 ? (float)timesReachedEnd / timesSpawned : 0f;

        public float AverageMaxNode => analysisSamples > 0 ? (float)totalMaxNodesReached / analysisSamples : 0f;

        public float AverageSpeed => speedSamples > 0 ? totalSpeed / speedSamples : 1f;

        public float WeightedProgressRate
        {
            get
            {
                if (timesSpawned == 0) return 0f;

                int totalNodes = GameLoopManager.TotalNodes;
                if (totalNodes <= 1) return 0f;

                float totalWeightedProgress = 0f;
                int totalSamples = 0;

                while (nodesReachedCount.Count < totalNodes)
                {
                    nodesReachedCount.Add(0);
                }

                for (int node = 0; node < totalNodes && node < nodesReachedCount.Count; node++)
                {
                    if (nodesReachedCount[node] > 0)
                    {
                        float nodeWeight = Mathf.Pow((node + 1) / (float)totalNodes, 3f) * 8f;
                        totalWeightedProgress += nodesReachedCount[node] * nodeWeight;
                        totalSamples += nodesReachedCount[node];
                    }
                }

                return totalSamples > 0 ? Mathf.Clamp01(totalWeightedProgress / totalSamples) : 0f;
            }
        }

        public float EffectivenessScore =>
            (WeightedProgressRate * 0.8f) +
            (SuccessRate * 0.1f) +
            (AverageSpeed * 0.1f);

        public Queue<float> recentProgress = new Queue<float>();
        public int maxRecentSamples = 7;

        public float RecentWeightedProgress
        {
            get
            {
                if (recentProgress.Count == 0) return WeightedProgressRate;

                float sum = 0f;
                foreach (float progress in recentProgress)
                    sum += progress;

                return sum / recentProgress.Count;
            }
        }

        public void AddRecentProgress(float progress)
        {
            recentProgress.Enqueue(progress);
            while (recentProgress.Count > maxRecentSamples)
                recentProgress.Dequeue();
        }
    }

    [System.Serializable]
    public class WavePerformance
    {
        public int waveIndex;
        public List<EnemyPerformanceData> enemyPerformances = new List<EnemyPerformanceData>();
    }

    public List<WavePerformance> wavePerformances = new List<WavePerformance>();
    public List<EnemyPerformanceData> globalEnemyStats = new List<EnemyPerformanceData>();
    public int minSamplesForReliability = 2;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void RecordEnemySpawn(int enemyID, int waveIndex)
    {
        var wavePerf = GetOrCreateWavePerformance(waveIndex);
        var enemyPerf = GetOrCreateEnemyPerformance(enemyID, wavePerf);
        enemyPerf.timesSpawned++;

        var globalEnemyPerf = GetOrCreateGlobalEnemyPerformance(enemyID);
        globalEnemyPerf.timesSpawned++;
    }

    public void RecordEnemySpeed(int enemyID, float speed)
    {
        var globalEnemyPerf = GetOrCreateGlobalEnemyPerformance(enemyID);
        globalEnemyPerf.totalSpeed += speed;
        globalEnemyPerf.speedSamples++;
        globalEnemyPerf.averageSpeed = globalEnemyPerf.AverageSpeed;
    }

    public void RecordEnemyReachedEnd(int enemyID, int waveIndex)
    {
        var wavePerf = GetOrCreateWavePerformance(waveIndex);
        var enemyPerf = GetOrCreateEnemyPerformance(enemyID, wavePerf);
        enemyPerf.timesReachedEnd++;
        enemyPerf.totalMaxNodesReached += GameLoopManager.LastNodeIndex;
        enemyPerf.analysisSamples++;

        var globalEnemyPerf = GetOrCreateGlobalEnemyPerformance(enemyID);
        globalEnemyPerf.timesReachedEnd++;
        globalEnemyPerf.totalMaxNodesReached += GameLoopManager.LastNodeIndex;
        globalEnemyPerf.analysisSamples++;

        RecordEnemyProgress(enemyID, GameLoopManager.LastNodeIndex);
    }

    public void RecordEnemyDefeated(int enemyID, int waveIndex, int maxNodeReached)
    {
        var wavePerf = GetOrCreateWavePerformance(waveIndex);
        var enemyPerf = GetOrCreateEnemyPerformance(enemyID, wavePerf);
        enemyPerf.totalMaxNodesReached += maxNodeReached;
        enemyPerf.analysisSamples++;

        var globalEnemyPerf = GetOrCreateGlobalEnemyPerformance(enemyID);
        globalEnemyPerf.totalMaxNodesReached += maxNodeReached;
        globalEnemyPerf.analysisSamples++;

        RecordEnemyProgress(enemyID, maxNodeReached);
    }

    public List<int> GetMostSuccessfulEnemies(int count = 2)
    {
        var successful = globalEnemyStats
            .Where(e => e.timesSpawned >= minSamplesForReliability)
            .OrderByDescending(e => e.SuccessRate)
            .ThenByDescending(e => e.AverageMaxNode)
            .Take(count)
            .Select(e => e.enemyID)
            .ToList();

        return successful;
    }

    public List<int> GetMostProgressiveEnemies(int count)
    {
        var validStats = globalEnemyStats
            .Where(stat => stat.analysisSamples >= minSamplesForReliability)
            .ToList();

        if (validStats.Count == 0)
        {
            return new List<int> { 1, 2, 3 }.Take(count).ToList();
        }

        var scoredEnemies = validStats
            .Select(stat => new {
                enemyID = stat.enemyID,
                score = (stat.WeightedProgressRate * 0.6f) + (stat.SuccessRate * 0.4f)
            })
            .OrderByDescending(x => x.score)
            .Take(count)
            .Select(x => x.enemyID)
            .ToList();

        return scoredEnemies;
    }

    public List<int> GetFastestEnemies(int count = 2)
    {
        var fastest = globalEnemyStats
            .Where(e => e.speedSamples >= minSamplesForReliability)
            .OrderByDescending(e => e.AverageSpeed)
            .ThenByDescending(e => e.SuccessRate)
            .Take(count)
            .Select(e => e.enemyID)
            .ToList();

        return fastest;
    }

    public List<int> GetMostEffectiveEnemies(int count = 2)
    {
        var effective = globalEnemyStats
            .Where(e => e.timesSpawned >= minSamplesForReliability)
            .OrderByDescending(e => e.EffectivenessScore)
            .Take(count)
            .Select(e => e.enemyID)
            .ToList();

        return effective;
    }

    public float GetCurrentGlobalSuccessRate()
    {
        int totalSpawned = globalEnemyStats.Sum(e => e.timesSpawned);
        int totalReachedEnd = globalEnemyStats.Sum(e => e.timesReachedEnd);
        float rate = totalSpawned > 0 ? (float)totalReachedEnd / totalSpawned : 0f;

        return rate;
    }

    private WavePerformance GetOrCreateWavePerformance(int waveIndex)
    {
        var wavePerf = wavePerformances.Find(w => w.waveIndex == waveIndex);
        if (wavePerf == null)
        {
            wavePerf = new WavePerformance { waveIndex = waveIndex };
            wavePerformances.Add(wavePerf);
        }
        return wavePerf;
    }

    private EnemyPerformanceData GetOrCreateEnemyPerformance(int enemyID, WavePerformance wavePerf)
    {
        var enemyPerf = wavePerf.enemyPerformances.Find(e => e.enemyID == enemyID);
        if (enemyPerf == null)
        {
            enemyPerf = new EnemyPerformanceData { enemyID = enemyID };
            wavePerf.enemyPerformances.Add(enemyPerf);
        }
        return enemyPerf;
    }

    private EnemyPerformanceData GetOrCreateGlobalEnemyPerformance(int enemyID)
    {
        var enemyPerf = globalEnemyStats.Find(e => e.enemyID == enemyID);
        if (enemyPerf == null)
        {
            enemyPerf = new EnemyPerformanceData { enemyID = enemyID };
            globalEnemyStats.Add(enemyPerf);
        }
        return enemyPerf;
    }

    public void ResetAllStats()
    {
        wavePerformances.Clear();
        globalEnemyStats.Clear();
    }

    public void RecordEnemyProgress(int enemyID, int maxNodeReached)
    {
        var globalEnemyPerf = GetOrCreateGlobalEnemyPerformance(enemyID);

        while (globalEnemyPerf.nodesReachedCount.Count <= maxNodeReached)
        {
            globalEnemyPerf.nodesReachedCount.Add(0);
        }

        globalEnemyPerf.nodesReachedCount[maxNodeReached]++;

        float progress = (float)maxNodeReached / GameLoopManager.LastNodeIndex;
        globalEnemyPerf.AddRecentProgress(progress);
    }

    public float GetEnemyEffectiveness(int enemyID)
    {
        var stat = globalEnemyStats.Find(s => s.enemyID == enemyID);
        if (stat == null || stat.analysisSamples < minSamplesForReliability)
            return 0.5f;

        float progressScore = stat.WeightedProgressRate;
        float successScore = stat.SuccessRate;
        float speedScore = stat.AverageSpeed / 5f;

        return (progressScore * 0.5f) + (successScore * 0.3f) + (speedScore * 0.2f);
    }
}