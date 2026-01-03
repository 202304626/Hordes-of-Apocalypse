using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class MLWaveInjector
{
    private static bool mlAgentActive = false;
    private static MLWaveGeneratorAgent mlAgent;
    private static WaveManager waveManager;

    public static void Initialize()
    {
        mlAgent = Object.FindObjectOfType<MLWaveGeneratorAgent>();
        waveManager = Object.FindObjectOfType<WaveManager>();

        mlAgentActive = (mlAgent != null && waveManager != null);
    }

    public static void InjectWave(WaveData mlWave, int waveIndex)
    {
        if (waveIndex < 0 || waveManager == null)
        {
            return;
        }

        try
        {
            if (mlWave?.enemyGroups == null)
            {
                return;
            }

            waveManager.RegisterMLWave(waveIndex, mlWave);
        }
        catch (System.Exception e)
        {
        }
    }

    public static bool IsMLAgentActive()
    {
        bool active = mlAgentActive && mlAgent != null;
        return active;
    }

    public static void ForceActivation()
    {
        Initialize();
    }
}