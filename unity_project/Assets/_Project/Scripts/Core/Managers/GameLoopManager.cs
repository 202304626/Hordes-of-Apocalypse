using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameLoopManager : MonoBehaviour
{
    public static List<TowerBehaviour> TowersInGame;
    public static Vector3[] NodePositions;
    public static float[] NodeDistances;

    [Header("Path Configuration")]
    public Transform NodeParent;

    [Header("Game State")]
    public bool LoopShouldEnd;

    private static Queue<Enemy> EnemiesToRemove;
    private static Queue<EnemyDamageData> DamageData;
    private EnemyManager enemyManager;

    public static int TotalNodes => NodePositions != null ? NodePositions.Length : 0;
    public static int LastNodeIndex => TotalNodes > 0 ? TotalNodes - 1 : 0;

    void Start()
    {
        DamageData = new Queue<EnemyDamageData>();
        TowersInGame = new List<TowerBehaviour>();
        EnemiesToRemove = new Queue<Enemy>();

        EntitySummoner.Init();
        InitializePathNodes();
        enemyManager = GameStateManager.Instance.enemyManager;

        StartCoroutine(GameLoop());
    }

    private void InitializePathNodes()
    {
        if (NodeParent == null)
        {
            Debug.LogError("NodeParent no está asignado en GameLoopManager!");
            return;
        }

        NodePositions = new Vector3[NodeParent.childCount];
        for (int i = 0; i < NodeParent.childCount; i++)
        {
            NodePositions[i] = NodeParent.GetChild(i).position;
        }

        NodeDistances = new float[NodePositions.Length - 1];
        for (int i = 0; i < NodeDistances.Length; i++)
        {
            NodeDistances[i] = Vector3.Distance(NodePositions[i], NodePositions[i + 1]);
        }

        if (TotalNodes < 2)
        {
            Debug.LogError("El camino necesita al menos 2 nodos!");
        }
    }

    IEnumerator GameLoop()
    {
        int frameCount = 0;
        while (!LoopShouldEnd)
        {
            frameCount++;

            if (GameStateManager.Instance.IsGameActive())
            {
                enemyManager.Tick();
                ApplyTick();
                DamageEnemies();
                RemoveEnemies();

                if (frameCount % 600 == 0)
                {
                    VerifyGameStateIntegrity();
                }
            }

            yield return null;
        }
    }

    private void VerifyGameStateIntegrity()
    {
        if (EntitySummoner.EnemiesInGame == null) return;

        int enemiesInList = EntitySummoner.EnemiesInGame.Count;
        int activeEnemies = 0;

        Transform enemiesContainer = EntitySummoner.GetEnemiesContainer();
        if (enemiesContainer != null)
        {
            activeEnemies = enemiesContainer.childCount;
        }
        else
        {
            foreach (Enemy enemy in EntitySummoner.EnemiesInGame)
            {
                if (enemy != null && enemy.gameObject.activeInHierarchy && enemy.IsAliveAndActive())
                {
                    activeEnemies++;
                }
            }
        }

        if (enemiesInList != activeEnemies)
        {
            EntitySummoner.CleanOrphanedEnemies();
        }
    }

    void ApplyTick()
    {
        foreach (TowerBehaviour tower in TowersInGame)
        {
            if (tower == null) continue;

            Collider[] enemiesNearby = Physics.OverlapSphere(tower.transform.position, tower.Range, tower.EnemiesLayer);
            tower.Tick();
        }
    }

    void DamageEnemies()
    {
        if (DamageData.Count > 0)
        {
            int damageCount = DamageData.Count;

            for (int i = 0; i < damageCount; i++)
            {
                EnemyDamageData CurrentDamageData = DamageData.Dequeue();

                if (CurrentDamageData.TargetedEnemy == null ||
                    !CurrentDamageData.TargetedEnemy.gameObject.activeInHierarchy)
                {
                    continue;
                }

                CurrentDamageData.TargetedEnemy.TakeDamage(CurrentDamageData.TotalDamage);
            }
        }
    }

    void RemoveEnemies()
    {
        if (EnemiesToRemove.Count > 0)
        {
            int enemyCount = EnemiesToRemove.Count;

            for (int i = 0; i < enemyCount; i++)
            {
                Enemy enemyToRemove = EnemiesToRemove.Dequeue();
                if (enemyToRemove != null)
                {
                    EntitySummoner.RemoveEnemy(enemyToRemove);
                }
            }
        }
    }

    public static void EnqueDamageData(EnemyDamageData damagedata)
    {
        if (damagedata.TargetedEnemy == null)
        {
            Debug.LogError("Intento de encolar daño a enemigo NULL");
            return;
        }

        DamageData.Enqueue(damagedata);
    }

    public static void EnqueEnemyToRemove(Enemy EnemyToRemove)
    {
        EnemiesToRemove.Enqueue(EnemyToRemove);
    }
}

public struct EnemyDamageData
{
    public EnemyDamageData(Enemy target, float damage, float resistance)
    {
        TargetedEnemy = target;
        TotalDamage = damage;
        Resistance = resistance;
    }

    public Enemy TargetedEnemy;
    public float TotalDamage;
    public float Resistance;
}