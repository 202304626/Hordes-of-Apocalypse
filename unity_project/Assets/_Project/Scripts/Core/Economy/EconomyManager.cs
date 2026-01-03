using System.Collections.Generic;
using UnityEngine;

public class EconomyManager : MonoBehaviour
{
    [Header("Configuración Económica Central")]
    public int startingMoney = 300;
    public int maxMoney = 99999;
    public bool enableDebt = false;

    [Header("Balance del Juego - Todas las recompensas aquí")]
    public int baseWaveReward = 70;
    public int passiveIncomePerWave = 10;
    public int enemyKillRewardBase = 10;
    public float enemyKillRewardMultiplier = 1.0f;

    private int currentMoney;
    private int previousMoney;
    private List<MoneyTransaction> transactionHistory = new List<MoneyTransaction>();

    public static EconomyManager Instance { get; private set; }
    public int CurrentMoney => currentMoney;

    private bool isInitialized = false;

    private readonly object transactionLock = new object();
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializeEconomy();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void InitializeEconomy()
    {
        if (isInitialized) return;

        currentMoney = startingMoney;
        previousMoney = startingMoney;
        transactionHistory.Clear();

        RecordTransaction(MoneyTransactionType.GameStart, "Game Start", startingMoney, true);

        isInitialized = true;
    }

    public bool ProcessTransaction(MoneyTransactionType type, string source, int amount)
    {
        if (!isInitialized) return false;

        lock (transactionLock)
        {
            if (amount < 0 && currentMoney + amount < 0 && !enableDebt)
            {
                GameEvents.TriggerTransactionFailed("Fondos insuficientes", amount);
                return false;
            }

            int previousMoney = currentMoney;
            MoneyTransaction transaction;

            try
            {
                currentMoney += amount;
                currentMoney = Mathf.Clamp(currentMoney, 0, maxMoney);

                transaction = new MoneyTransaction(
                    type, source, amount, true, previousMoney, currentMoney
                );

                transactionHistory.Add(transaction);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"🛑 Error CRÍTICO calculando dinero (Transacción cancelada): {e.Message}");
                return false;
            }

            try
            {
                GameEvents.TriggerMoneyTransaction(amount, transaction);
                GameEvents.TriggerMoneyChanged(currentMoney, previousMoney);
                GameEvents.TriggerTransactionProcessed(type, source, amount);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"⚠️ Error secundario actualizando UI/Eventos (La transacción fue válida): {e.Message}");
            }

            return true;
        }
    }

    private void RecordTransaction(MoneyTransactionType type, string source, int amount, bool success)
    {
        var transaction = new MoneyTransaction(type, source, amount, success, previousMoney, currentMoney);
        transactionHistory.Add(transaction);
        GameEvents.TriggerMoneyTransaction(amount, transaction);
    }

    public bool CanAfford(int amount)
    {
        return currentMoney >= amount;
    }

    public int GetTowerCost(string towerName)
    {
        return TowerConfigManager.Instance.GetTowerCost(towerName);
    }

    public float GetSellPercentage(string towerName)
    {
        return TowerConfigManager.Instance.GetSellPercentage(towerName);
    }

    public int CalculateSellValue(string towerName, int originalCost, int currentLevel)
    {
        float sellPercentage = GetSellPercentage(towerName);

        float levelBonus = 0.05f * (currentLevel - 1);
        float finalPercentage = Mathf.Min(sellPercentage + levelBonus, 0.9f);

        int sellValue = Mathf.RoundToInt(originalCost * finalPercentage);
        sellValue = Mathf.Max(sellValue, 1);

        return sellValue;
    }
    public int CalculateEnemyReward(Enemy enemy)
    {
        if (enemy == null) return enemyKillRewardBase;

        int baseReward = enemy.goldValue;
        float healthMultiplier = 1.0f;
        int reward = Mathf.RoundToInt(baseReward * healthMultiplier * enemyKillRewardMultiplier);
        reward = Mathf.Max(reward, 1);

        return reward;
    }
    public void ResetEconomy()
    {
        isInitialized = false;
        InitializeEconomy();
    }
}

[System.Serializable]
public class TowerEconomyConfig
{
    public string towerName;
    public int baseCost;
    public int upgradeCost;
    [Range(0.1f, 1f)]
    public float sellPercentage;

    public TowerEconomyConfig(string name, int cost, int upgrade, float sellPercent)
    {
        towerName = name;
        baseCost = cost;
        upgradeCost = upgrade;
        sellPercentage = sellPercent;
    }
}