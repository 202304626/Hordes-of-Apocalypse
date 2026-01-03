using System;

public static class GameEvents
{
    public static Action<int> OnLivesChanged;
    public static Action<int> OnWaveChanged;

    public static Action<TowerBehaviour> OnTowerPlaced;
    public static Action<TowerBehaviour> OnTowerSold;
    public static Action<TowerBehaviour> OnTowerUpgraded;

    public static Action<Enemy> OnEnemySpawned;
    public static Action<Enemy> OnEnemyDied;
    public static Action<Enemy> OnEnemyReachedEnd;

    public static Action OnGameOver;
    public static Action OnVictory;
    public static Action OnRestartGame;
    public static Action<int, int> OnHealthChanged;

    public static Action<int, MoneyTransaction> OnMoneyTransaction;
    public static Action<int, int> OnMoneyChanged;
    public static Action<string, int> OnTransactionFailed;
    public static Action<MoneyTransactionType, string, int> OnTransactionProcessed;

    public static Action OnButtonClick;
    public static Action OnPreparationStarted;

    public static void TriggerPreparationStarted() => OnPreparationStarted?.Invoke();
    public static void TriggerButtonClick() => OnButtonClick?.Invoke();
    public static void TriggerLivesChanged(int newLives) => OnLivesChanged?.Invoke(newLives);
    public static void TriggerWaveChanged(int waveNumber) => OnWaveChanged?.Invoke(waveNumber);

    public static void TriggerTowerPlaced(TowerBehaviour tower) => OnTowerPlaced?.Invoke(tower);
    public static void TriggerTowerSold(TowerBehaviour tower) => OnTowerSold?.Invoke(tower);
    public static void TriggerTowerUpgraded(TowerBehaviour tower) => OnTowerUpgraded?.Invoke(tower);

    public static void TriggerEnemySpawned(Enemy enemy) => OnEnemySpawned?.Invoke(enemy);
    public static void TriggerEnemyDied(Enemy enemy) => OnEnemyDied?.Invoke(enemy);
    public static void TriggerEnemyReachedEnd(Enemy enemy) => OnEnemyReachedEnd?.Invoke(enemy);

    public static void TriggerGameOver() => OnGameOver?.Invoke();
    public static void TriggerVictory() => OnVictory?.Invoke();
    public static void TriggerRestartGame() => OnRestartGame?.Invoke();

    public static void TriggerMoneyTransaction(int amount, MoneyTransaction transaction) =>
        OnMoneyTransaction?.Invoke(amount, transaction);
    public static void TriggerMoneyChanged(int current, int previous) =>
        OnMoneyChanged?.Invoke(current, previous);
    public static void TriggerTransactionFailed(string reason, int amount) =>
        OnTransactionFailed?.Invoke(reason, amount);
    public static void TriggerTransactionProcessed(MoneyTransactionType type, string source, int amount) =>
        OnTransactionProcessed?.Invoke(type, source, amount);

    public static void ClearAllSubscriptions()
    {
        OnEnemyReachedEnd = null;
    }
}

public enum MoneyTransactionType
{
    EnemyKill,
    WaveComplete,
    PassiveIncome,
    TowerRefund,
    TowerPurchase,
    TowerUpgrade,
    GameStart,
    Cheat,
    Debug
}

public struct MoneyTransaction
{
    public MoneyTransactionType type;
    public string source;
    public int amount;
    public bool success;
    public int balanceBefore;
    public int balanceAfter;
    public string timestamp;

    public MoneyTransaction(MoneyTransactionType transactionType, string transactionSource,
                          int transactionAmount, bool transactionSuccess, int before, int after)
    {
        type = transactionType;
        source = transactionSource;
        amount = transactionAmount;
        success = transactionSuccess;
        balanceBefore = before;
        balanceAfter = after;
        timestamp = System.DateTime.Now.ToString("HH:mm:ss");
    }
}