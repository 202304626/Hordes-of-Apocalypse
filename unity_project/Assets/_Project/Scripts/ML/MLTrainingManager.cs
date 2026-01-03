using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MLTrainingManager : MonoBehaviour
{
    [System.Serializable]
    public class StrategyEvolution
    {
        public string strategyName;
        public float[] successRates = new float[10];
        public float averagePerformance;
        public int usageCount;
    }

    [Header("Training Configuration")]
    public bool trainingMode = false;
    public int maxTrainingEpisodes = 1000;
    public bool showTrainingDebug = true;

    [Header("Agent References")]
    public MLWaveGeneratorAgent mlAgent;

    private List<StrategyEvolution> strategyHistory = new List<StrategyEvolution>();
    private int strategyMemorySize = 20;
    private int currentEpisode = 0;
    private bool isInitialized = false;

    void Start()
    {
        Invoke("DelayedInitialization", 0.1f);
    }

    void DelayedInitialization()
    {
        if (isInitialized) return;

        mlAgent = FindObjectOfType<MLWaveGeneratorAgent>();

        if (mlAgent == null)
        {
            Debug.LogError("No se encontró MLWaveGeneratorAgent en la escena");
            return;
        }

        if (!mlAgent.IsReady())
        {
            Debug.LogWarning("MLWaveGeneratorAgent no está listo, reintentando...");
            Invoke("DelayedInitialization", 0.5f);
            return;
        }

        InitializeTraining();
        isInitialized = true;
        Debug.Log("ML Training Manager COMPLETAMENTE INICIALIZADO");
    }

    void InitializeTraining()
    {
        if (mlAgent != null)
        {
            mlAgent.SetTrainingMode(trainingMode);
        }

        MLWaveInjector.ForceActivation();
        Debug.Log($"ML Training Manager inicializado - Modo: {(trainingMode ? "ENTRENAMIENTO" : "INFERENCIA")}");
    }

    public void OnWaveCompleted(int waveIndex, int enemiesReachedEnd, int totalEnemies, float playerHealthPercent)
    {
        if (mlAgent == null) return;

        if (waveIndex >= 1 && waveIndex < 14)
        {
            mlAgent.EvaluateWavePerformance(waveIndex, enemiesReachedEnd, totalEnemies, playerHealthPercent);

            if (showTrainingDebug)
            {
                float successRate = totalEnemies > 0 ? (float)enemiesReachedEnd / totalEnemies : 0f;
                Debug.Log($"IA Evaluada: Oleada {waveIndex + 1} - {successRate:P0} éxito");
            }
        }

        currentEpisode++;
    }

    public void RequestMLWaveGeneration(int waveIndex)
    {
        if (waveIndex < 1 || waveIndex >= 14)
        {
            Debug.LogWarning($"Índice de oleada no válido para IA: {waveIndex} (debe ser 1-13)");
            return;
        }

        if (mlAgent == null)
        {
            Debug.LogError("ML Agent es null en RequestMLWaveGeneration");
            return;
        }

        if (!IsInitialized())
        {
            Debug.LogError("ML Training Manager no está inicializado");
            return;
        }

        if (!MLWaveInjector.IsMLAgentActive())
        {
            Debug.LogError("MLWaveInjector no está activo");
            return;
        }

        Debug.Log($"SOLICITANDO IA: Generando oleada {waveIndex + 1} (índice {waveIndex})");
        mlAgent.RequestWaveGeneration(waveIndex);
    }

    public void AnalyzeStrategyEffectiveness(WaveData wave, float performanceScore)
    {
        string strategySignature = GetStrategySignature(wave);

        var existingStrategy = strategyHistory.Find(s => s.strategyName == strategySignature);

        if (existingStrategy == null)
        {
            existingStrategy = new StrategyEvolution
            {
                strategyName = strategySignature,
                successRates = new float[10] { performanceScore, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                averagePerformance = performanceScore,
                usageCount = 1
            };
            strategyHistory.Add(existingStrategy);

            if (strategyHistory.Count > strategyMemorySize)
            {
                strategyHistory.RemoveAt(0);
            }
        }
        else
        {
            UpdateStrategyHistory(existingStrategy, performanceScore);
        }
    }

    private void UpdateStrategyHistory(StrategyEvolution strategy, float newPerformance)
    {
        for (int i = 0; i < strategy.successRates.Length - 1; i++)
        {
            strategy.successRates[i] = strategy.successRates[i + 1];
        }

        strategy.successRates[strategy.successRates.Length - 1] = newPerformance;

        float sum = 0f;
        int validSamples = 0;
        foreach (float rate in strategy.successRates)
        {
            if (rate > 0)
            {
                sum += rate;
                validSamples++;
            }
        }

        strategy.averagePerformance = validSamples > 0 ? sum / validSamples : 0f;
        strategy.usageCount++;
    }

    private string GetStrategySignature(WaveData wave)
    {
        if (wave?.enemyGroups == null) return "Unknown";

        var groups = wave.enemyGroups.OrderBy(g => g.enemyID).ToList();
        string signature = "";

        foreach (var group in groups)
        {
            signature += $"{group.enemyID}x{group.count},";
        }

        signature += $"D{wave.timeBetweenSpawns:F1}";
        return signature.TrimEnd(',');
    }

    public bool IsTraining() => trainingMode;
    public bool IsInitialized() => isInitialized;
}