using System.Collections.Generic;

[System.Serializable]
public class WaveData
{
    public string waveName;
    public List<EnemyGroup> enemyGroups;
    public float timeBetweenSpawns;
    public int waveReward;
    public float timeLimit;
    public bool isBossWave;
}

[System.Serializable]
public class EnemyGroup
{
    public int enemyID;
    public int count;
    public float delayBeforeGroup;
    public float timeBetweenSpawnsInGroup;
}