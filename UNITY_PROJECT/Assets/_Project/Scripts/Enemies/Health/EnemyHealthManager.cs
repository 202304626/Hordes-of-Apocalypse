using UnityEngine;

public class EnemyHealthManager : MonoBehaviour
{
    [Header("Dynamic Health Scaling")]
    public float baseHealthMultiplier = 1.0f;
    public float healthIncreasePerWave = 0.15f;
    public float maxHealthMultiplier = 12.0f;
    public float adaptiveScalingFactor = 0.08f;
    public float playerPerformanceFactor = 0.15f;

    [Header("ML Health Scaling")]
    public float mlHealthBaseMultiplier = 1.0f;
    public bool useMLHealthScaling = true;

    [Header("Wave Phase Scaling")]
    public float earlyGameMultiplier = 1.0f;
    public float midGameMultiplier = 1.2f;
    public float lateGameMultiplier = 1.5f;

    public float GetHealthMultiplierForWave(int waveIndex, float playerSuccessRate = 0.5f)
    {
        if (waveIndex < 0) return baseHealthMultiplier;

        if (waveIndex == 0)
        {
            return 1.0f;
        }

        float baseMultiplier = GetBaseHealthMultiplier(waveIndex);
        float phaseMultiplier = GetPhaseMultiplier(waveIndex);
        baseMultiplier *= phaseMultiplier;

        float adaptiveBonus = CalculateAdaptiveScaling(playerSuccessRate, waveIndex);

        float mlMultiplier = GetMLHealthMultiplier();

        float finalMultiplier = baseMultiplier * (1f + adaptiveBonus) * mlMultiplier;
        finalMultiplier = Mathf.Clamp(finalMultiplier, baseHealthMultiplier, maxHealthMultiplier);

        return finalMultiplier;
    }

    private float GetMLHealthMultiplier()
    {
        if (!useMLHealthScaling) return mlHealthBaseMultiplier;

        MLWaveGeneratorAgent mlAgent = FindObjectOfType<MLWaveGeneratorAgent>();
        if (mlAgent != null && mlAgent.IsReady())
        {
            return mlAgent.GetCurrentHealthMultiplier();
        }

        return mlHealthBaseMultiplier;
    }
    private float GetPhaseMultiplier(int waveIndex)
    {
        if (waveIndex < 5) return earlyGameMultiplier;
        if (waveIndex < 10) return midGameMultiplier;
        return lateGameMultiplier;
    }

    private float CalculateAdaptiveScaling(float playerSuccessRate, int waveIndex)
    {
        float adaptiveBonus = 0f;

        if (playerSuccessRate < 0.3f)
        {
            adaptiveBonus = -0.15f;
            if (waveIndex > 8) adaptiveBonus = -0.08f;
        }
        else if (playerSuccessRate > 0.7f)
        {
            adaptiveBonus = 0.2f;
            if (waveIndex > 8) adaptiveBonus = 0.3f;
        }
        else if (playerSuccessRate > 0.4f && playerSuccessRate < 0.6f)
        {
            adaptiveBonus = 0.05f + (waveIndex * 0.01f);
        }

        return Mathf.Clamp(adaptiveBonus, -0.2f, 0.4f);
    }

    private float GetBaseHealthMultiplier(int waveIndex)
    {
        if (waveIndex == 0) return 1.0f;

        float waveFactor = waveIndex;

        if (waveIndex > 10) waveFactor *= 1.4f;
        else if (waveIndex > 5) waveFactor *= 1.2f;

        return baseHealthMultiplier + ((waveFactor - 1) * healthIncreasePerWave);
    }

}