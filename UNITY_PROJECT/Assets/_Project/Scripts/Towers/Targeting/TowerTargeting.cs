using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class TowerTargeting
{
    public enum TargetType
    {
        First,
        Last,
        Close,
        Strong,
        Weak
    }

    public static Enemy GetTarget(TowerBehaviour CurrentTower, TargetType TargetMethod)
    {
        if (CurrentTower == null || CurrentTower.transform == null)
        {
            return null;
        }

        if (EntitySummoner.EnemiesInGame == null || EntitySummoner.EnemiesInGame.Count == 0)
        {
            return null;
        }

        Collider[] EnemiesInRange = Physics.OverlapSphere(
            CurrentTower.transform.position,
            CurrentTower.Range,
            CurrentTower.EnemiesLayer
        );

        if (EnemiesInRange.Length == 0)
        {
            return null;
        }

        int validEnemyCount = 0;
        Enemy[] validEnemies = new Enemy[EnemiesInRange.Length];

        for (int i = 0; i < EnemiesInRange.Length; i++)
        {
            if (EnemiesInRange[i] == null || EnemiesInRange[i].transform == null)
            {
                continue;
            }

            Transform parentTransform = EnemiesInRange[i].transform.parent;
            if (parentTransform == null)
            {
                continue;
            }

            Enemy CurrentEnemy = parentTransform.GetComponent<Enemy>();
            if (CurrentEnemy == null || !CurrentEnemy.IsAliveAndActive())
            {
                continue;
            }

            validEnemies[validEnemyCount] = CurrentEnemy;
            validEnemyCount++;
        }

        if (validEnemyCount == 0)
        {
            return null;
        }

        NativeArray<EnemyData> EnemiesToCalculate = new NativeArray<EnemyData>(validEnemyCount, Allocator.TempJob);
        NativeArray<Vector3> NodePositions = new NativeArray<Vector3>(GameLoopManager.NodePositions, Allocator.TempJob);
        NativeArray<float> NodeDistances = new NativeArray<float>(GameLoopManager.NodeDistances, Allocator.TempJob);
        NativeArray<int> EnemyToIndex = new NativeArray<int>(new int[] { -1 }, Allocator.TempJob);
        NativeArray<float> BestValue = new NativeArray<float>(1, Allocator.TempJob);

        for (int i = 0; i < validEnemyCount; i++)
        {
            Enemy CurrentEnemy = validEnemies[i];
            int EnemyIndexInList = EntitySummoner.EnemiesInGame.FindIndex(x => x == CurrentEnemy);

            EnemiesToCalculate[i] = new EnemyData(
                CurrentEnemy.transform.position,
                CurrentEnemy.NodeIndex,
                CurrentEnemy.Health,
                EnemyIndexInList
            );
        }

        SearchForEnemy EnemySearchJob = new SearchForEnemy
        {
            _EnemiesToCalculate = EnemiesToCalculate,
            _NodeDistances = NodeDistances,
            _EnemyToIndex = EnemyToIndex,
            _NodePositions = NodePositions,
            _BestValue = BestValue,
            TowerPosition = CurrentTower.transform.position,
            TargetingType = (int)TargetMethod
        };

        switch ((int)TargetMethod)
        {
            case 0:
                BestValue[0] = Mathf.Infinity;
                break;
            case 1:
                BestValue[0] = Mathf.NegativeInfinity;
                break;
            case 2:
                BestValue[0] = Mathf.Infinity;
                break;
            case 3:
                goto case 1;
            case 4:
                goto case 0;
        }

        JobHandle dependency = new JobHandle();
        JobHandle SearchJobHandle = EnemySearchJob.Schedule(validEnemyCount, dependency);
        SearchJobHandle.Complete();

        int enemyArrayIndex = EnemyToIndex[0];
        int EnemyIndexToReturn = -1;

        if (enemyArrayIndex >= 0 && enemyArrayIndex < validEnemyCount)
        {
            EnemyIndexToReturn = EnemiesToCalculate[enemyArrayIndex].EnemyIndex;
        }

        EnemiesToCalculate.Dispose();
        NodePositions.Dispose();
        NodeDistances.Dispose();
        EnemyToIndex.Dispose();
        BestValue.Dispose();

        if (EnemyIndexToReturn == -1 || EnemyIndexToReturn >= EntitySummoner.EnemiesInGame.Count)
        {
            return null;
        }

        return EntitySummoner.EnemiesInGame[EnemyIndexToReturn];
    }

    struct EnemyData
    {
        public Vector3 EnemyPosition;
        public int EnemyIndex;
        public int NodeIndex;
        public float Health;

        public EnemyData(Vector3 position, int nodeindex, float hp, int enemyindex)
        {
            EnemyPosition = position;
            NodeIndex = nodeindex;
            EnemyIndex = enemyindex;
            Health = hp;
        }
    }

    struct SearchForEnemy : IJobFor
    {
        [ReadOnly] public NativeArray<EnemyData> _EnemiesToCalculate;
        [ReadOnly] public NativeArray<Vector3> _NodePositions;
        [ReadOnly] public NativeArray<float> _NodeDistances;
        [ReadOnly] public Vector3 TowerPosition;
        [ReadOnly] public int TargetingType;

        public NativeArray<int> _EnemyToIndex;
        public NativeArray<float> _BestValue;

        public void Execute(int index)
        {
            float CurrentEnemyDistanceToEnd = 0;
            float DistanceToEnemy = 0;

            switch (TargetingType)
            {
                case 0:
                    CurrentEnemyDistanceToEnd = GetDistanceToEnd(_EnemiesToCalculate[index]);

                    if (CurrentEnemyDistanceToEnd < _BestValue[0])
                    {
                        _EnemyToIndex[0] = index;
                        _BestValue[0] = CurrentEnemyDistanceToEnd;
                    }
                    break;

                case 1:
                    CurrentEnemyDistanceToEnd = GetDistanceToEnd(_EnemiesToCalculate[index]);

                    if (CurrentEnemyDistanceToEnd > _BestValue[0])
                    {
                        _EnemyToIndex[0] = index;
                        _BestValue[0] = CurrentEnemyDistanceToEnd;
                    }
                    break;

                case 2:
                    DistanceToEnemy = Vector3.Distance(TowerPosition, _EnemiesToCalculate[index].EnemyPosition);

                    if (DistanceToEnemy < _BestValue[0])
                    {
                        _EnemyToIndex[0] = index;
                        _BestValue[0] = DistanceToEnemy;
                    }
                    break;

                case 3:
                    if (_EnemiesToCalculate[index].Health > _BestValue[0])
                    {
                        _EnemyToIndex[0] = index;
                        _BestValue[0] = _EnemiesToCalculate[index].Health;
                    }
                    break;

                case 4:
                    if (_EnemiesToCalculate[index].Health < _BestValue[0])
                    {
                        _EnemyToIndex[0] = index;
                        _BestValue[0] = _EnemiesToCalculate[index].Health;
                    }
                    break;
            }
        }

        private float GetDistanceToEnd(EnemyData EnemyToEvaluate)
        {
            if (EnemyToEvaluate.NodeIndex < 0 || EnemyToEvaluate.NodeIndex >= _NodePositions.Length)
            {
                return Mathf.Infinity;
            }

            float FinalDistance = Vector3.Distance(
                EnemyToEvaluate.EnemyPosition,
                _NodePositions[EnemyToEvaluate.NodeIndex]
            );

            for (int i = EnemyToEvaluate.NodeIndex; i < _NodeDistances.Length; i++)
            {
                FinalDistance += _NodeDistances[i];
            }

            return FinalDistance;
        }
    }
}