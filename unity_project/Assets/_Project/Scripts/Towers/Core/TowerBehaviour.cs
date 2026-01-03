using System;
using UnityEngine;

public class TowerBehaviour : MonoBehaviour
{
    [Header("Referencias")]
    public LayerMask EnemiesLayer;
    public Enemy Target;
    public Transform TowerPivot;
    public Animator towerAnimator;
    public TowerTargeting.TargetType TargetingMethod = TowerTargeting.TargetType.First;

    [Header("Estadísticas Actuales (Auto-calculadas)")]
    public float Damage { get; private set; }
    public float Range { get; private set; }
    public float FireRate { get; private set; }

    private IDamageMethod CurrentDamageMethodClass;
    private float timeSinceLastShot;
    private string attackParam = "IsAttacking";
    private string attackSpeedParam = "AttackSpeed";
    private bool hasValidTarget = false;
    private float currentAnimationSpeed = 1f;
    private float attackAnimationLength = 1f;

    private TowerConfigManager.TowerConfig towerConfig;
    public int CurrentLevel { get; private set; } = 1;
    public int MaxLevel => towerConfig?.maxLevel ?? 6;

    void Start()
    {
        string towerName = TowerNaming.GetTowerName(this);
        towerConfig = TowerConfigManager.Instance.GetTowerConfig(towerName);

        if (towerConfig == null)
        {
            Debug.LogError($"❌ No se encontró configuración para: {towerName}");
            return;
        }

        InitializeTowerStats();
        timeSinceLastShot = 0f;

        CurrentDamageMethodClass = GetComponent<IDamageMethod>();
        if (CurrentDamageMethodClass == null)
        {
            Debug.LogError("TOWERS: No damage class attached to given tower!");
        }
        else
        {
            CurrentDamageMethodClass.Init(Damage, FireRate);
        }

        if (TowerPivot == null) TowerPivot = transform;

        if (towerAnimator == null)
        {
            towerAnimator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
        }

        if (towerAnimator != null)
        {
            AnimationClip[] clips = towerAnimator.runtimeAnimatorController.animationClips;
            foreach (AnimationClip clip in clips)
            {
                if (clip.name.ToLower().Contains("attack"))
                {
                    attackAnimationLength = clip.length;
                    break;
                }
            }
            towerAnimator.SetBool(attackParam, false);

            if (HasAnimatorParameter(attackSpeedParam))
            {
                towerAnimator.SetFloat(attackSpeedParam, 1f);
            }
        }
    }

    public void Tick()
    {
        if (CurrentDamageMethodClass == null)
        {
            Debug.LogError("❌ TORRE SIN MÉTODO DE DAÑO: " + gameObject.name);
            return;
        }

        Enemy potentialNewTarget = TowerTargeting.GetTarget(this, TargetingMethod);

        if (potentialNewTarget != null && potentialNewTarget != Target)
        {
            if (ShouldSwitchTarget(Target, potentialNewTarget, TargetingMethod))
            {
                Target = potentialNewTarget;
            }
        }

        if (Target != null)
        {
            CurrentDamageMethodClass.DamageTick(Target);
        }

        if (Target != null)
        {
            if (!Target.gameObject.activeInHierarchy)
            {
                Target = null;
            }
            else
            {
                float distanceToTarget = Vector3.Distance(transform.position, Target.transform.position);
                if (distanceToTarget > Range)
                {
                    Target = null;
                }
            }
        }

        bool previousTargetState = hasValidTarget;
        hasValidTarget = (Target != null && Target.gameObject.activeInHierarchy);

        if (hasValidTarget)
        {
            float distanceToTarget = Vector3.Distance(transform.position, Target.transform.position);
            hasValidTarget = (distanceToTarget <= Range);
        }

        if (towerAnimator != null)
        {
            towerAnimator.SetBool(attackParam, hasValidTarget);

            if (hasValidTarget && HasAnimatorParameter(attackSpeedParam))
            {
                float desiredAnimationDuration = 1f / FireRate;
                currentAnimationSpeed = attackAnimationLength / desiredAnimationDuration;
                towerAnimator.SetFloat(attackSpeedParam, currentAnimationSpeed);
            }
        }

        if (Target == null)
        {
            Target = TowerTargeting.GetTarget(this, TargetingMethod);
        }

        if (Target != null && Target.gameObject.activeInHierarchy)
        {
            float distanceToTarget = Vector3.Distance(transform.position, Target.transform.position);
            if (distanceToTarget <= Range)
            {
                RotateTowardsTarget();
                timeSinceLastShot += Time.deltaTime;
            }
            else
            {
                Target = null;
            }
        }
    }

    private bool ShouldSwitchTarget(Enemy currentTarget, Enemy newTarget, TowerTargeting.TargetType targetingMethod)
    {
        if (currentTarget == null) return true;
        if (newTarget == null) return false;

        switch (targetingMethod)
        {
            case TowerTargeting.TargetType.Strong:
                return newTarget.Health > currentTarget.Health;

            case TowerTargeting.TargetType.Weak:
                return newTarget.Health < currentTarget.Health;

            case TowerTargeting.TargetType.First:
                return GetDistanceToEnd(newTarget) < GetDistanceToEnd(currentTarget);

            case TowerTargeting.TargetType.Last:
                return GetDistanceToEnd(newTarget) > GetDistanceToEnd(currentTarget);

            case TowerTargeting.TargetType.Close:
                float currentDistance = Vector3.Distance(transform.position, currentTarget.transform.position);
                float newDistance = Vector3.Distance(transform.position, newTarget.transform.position);
                return newDistance < currentDistance;

            default:
                return false;
        }
    }

    private float GetDistanceToEnd(Enemy enemy)
    {
        if (enemy == null || GameLoopManager.NodePositions == null) return Mathf.Infinity;

        float distance = 0f;
        int currentNode = enemy.NodeIndex;

        if (currentNode < GameLoopManager.NodePositions.Length)
        {
            distance += Vector3.Distance(enemy.transform.position, GameLoopManager.NodePositions[currentNode]);
        }

        for (int i = currentNode; i < GameLoopManager.NodeDistances.Length; i++)
        {
            distance += GameLoopManager.NodeDistances[i];
        }

        return distance;
    }

    private bool HasAnimatorParameter(string paramName)
    {
        if (towerAnimator == null) return false;

        foreach (AnimatorControllerParameter parameter in towerAnimator.parameters)
        {
            if (parameter.name == paramName)
                return true;
        }
        return false;
    }

    private void RotateTowardsTarget()
    {
        if (Target == null || TowerPivot == null) return;

        Vector3 targetPosition = Target.transform.position;
        targetPosition.y = TowerPivot.position.y;

        Vector3 direction = targetPosition - TowerPivot.position;

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            TowerPivot.rotation = targetRotation;
        }
    }

    private void InitializeTowerStats()
    {
        if (towerConfig == null) return;

        var stats = towerConfig.GetStatsForLevel(CurrentLevel);
        Damage = stats.damage;
        Range = stats.range;
        FireRate = stats.fireRate;
    }

    private float CalculateStatForLevel(float baseValue, float increasePerLevel, int level)
    {
        return baseValue * Mathf.Pow(1 + increasePerLevel, level - 1);
    }

    public void UpgradeTower()
    {
        if (towerConfig == null || !CanUpgrade()) return;

        CurrentLevel++;
        InitializeTowerStats();

        if (CurrentDamageMethodClass != null)
        {
            CurrentDamageMethodClass.Init(Damage, FireRate);
        }

        ApplyUpgradeVisuals();
    }

    public (float nextDamage, float nextRange, float nextFireRate) GetNextLevelStats()
    {
        if (towerConfig == null || !CanUpgrade())
            return (Damage, Range, FireRate);

        return towerConfig.GetStatsForLevel(CurrentLevel + 1);
    }

    public int GetUpgradeCost()
    {
        if (towerConfig == null) return 50;
        return TowerConfigManager.Instance.GetUpgradeCost(towerConfig.towerName, CurrentLevel);
    }

    public bool CanUpgrade()
    {
        return towerConfig != null && CurrentLevel < towerConfig.maxLevel;
    }

    private void ApplyUpgradeVisuals()
    {
        ParticleSystem upgradeParticles = GetComponentInChildren<ParticleSystem>();
        upgradeParticles?.Play();
    }
}