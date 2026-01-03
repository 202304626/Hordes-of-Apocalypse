using System.Collections.Generic;
using UnityEngine;

public class TowerManager : MonoBehaviour
{
    [Header("Configuración de Torres")]
    public bool enableTowerLimits = true;
    public int globalTowerLimit = 24;

    [Header("Límites por tipo de torre")]
    public List<TowerTypeLimit> towerTypeLimits = new List<TowerTypeLimit>();

    private List<TowerBehaviour> placedTowers = new List<TowerBehaviour>();
    private Dictionary<string, int> towerCounts = new Dictionary<string, int>();

    public void Initialize()
    {
        CreateTowerHierarchySystem();
        InitializeTowerCounts();
    }

    private void InitializeTowerCounts()
    {
        towerCounts.Clear();

        string[] towerTypes = { "Knight Tower", "Alien Tower", "Mage Tower", "Orc Tower", "Chicken Tower", "Couple Tower" };

        foreach (string towerType in towerTypes)
        {
            towerCounts[towerType] = 0;
        }

        if (towerTypeLimits.Count == 0)
        {
            foreach (string towerType in towerTypes)
            {
                towerTypeLimits.Add(new TowerTypeLimit { towerName = towerType, maxTowers = 10 });
            }
        }
    }

    private void RegisterTower(TowerBehaviour tower, string towerType)
    {
        if (tower == null) return;

        if (!placedTowers.Contains(tower))
        {
            placedTowers.Add(tower);
        }

        if (towerCounts.ContainsKey(towerType))
        {
            towerCounts[towerType]++;
        }
        else
        {
            towerCounts[towerType] = 1;
        }
    }

    private void UnregisterTower(TowerBehaviour tower, string towerType)
    {
        if (tower == null) return;

        if (placedTowers.Contains(tower))
        {
            placedTowers.Remove(tower);
        }

        if (towerCounts.ContainsKey(towerType))
        {
            towerCounts[towerType] = Mathf.Max(0, towerCounts[towerType] - 1);
        }
    }

    private void CreateTowerHierarchySystem()
    {
        GameObject towerSystem = GameObject.Find("TowerSystem");
        if (towerSystem == null)
        {
            towerSystem = new GameObject("TowerSystem");
            towerSystem.transform.position = Vector3.zero;
            DontDestroyOnLoad(towerSystem);
        }

        Transform towersContainer = towerSystem.transform.Find("Towers");
        if (towersContainer == null)
        {
            GameObject towersObj = new GameObject("Towers");
            towersObj.transform.SetParent(towerSystem.transform);
            towersObj.transform.localPosition = Vector3.zero;
        }
    }

    public Transform GetTowersContainer()
    {
        GameObject towerSystem = GameObject.Find("TowerSystem");
        if (towerSystem == null) return null;

        Transform towersContainer = towerSystem.transform.Find("Towers");
        return towersContainer;
    }

    public TowerBehaviour PurchaseTower(string towerType, Vector3 position)
    {
        CreateTowerHierarchySystem();

        GameObject towerPrefab = TowerConfigManager.Instance.GetTowerPrefab(towerType);
        if (towerPrefab == null)
        {
            Debug.LogError($"❌ No se encontró prefab para: {towerType}");
            return null;
        }

        int cost = TowerConfigManager.Instance.GetTowerCost(towerType);

        bool transactionSuccess = EconomyManager.Instance.ProcessTransaction(
            MoneyTransactionType.TowerPurchase,
            $"Tower_{towerType}",
            -cost
        );

        if (!transactionSuccess)
        {
            return null;
        }

        if (!CanPlaceTowerType(towerType))
        {
            EconomyManager.Instance.ProcessTransaction(
                MoneyTransactionType.TowerRefund,
                $"Refund_{towerType}",
                cost
            );
            return null;
        }

        Transform towersContainer = GetTowersContainer();
        GameObject towerObject;

        if (towersContainer != null)
        {
            towerObject = Instantiate(towerPrefab, position, Quaternion.identity, towersContainer);
        }
        else
        {
            towerObject = Instantiate(towerPrefab, position, Quaternion.identity);
        }

        TowerBehaviour towerBehaviour = towerObject.GetComponent<TowerBehaviour>();
        if (towerBehaviour != null)
        {
            SetTowerLayer(towerObject);
            RegisterTower(towerBehaviour, towerType);

            if (!GameLoopManager.TowersInGame.Contains(towerBehaviour))
            {
                GameLoopManager.TowersInGame.Add(towerBehaviour);
            }

            GameEvents.TriggerTowerPlaced(towerBehaviour);
            return towerBehaviour;
        }

        Debug.LogError($"❌ Error: Torre {towerType} no tiene TowerBehaviour");
        Destroy(towerObject);
        return null;
    }

    private void SetTowerLayer(GameObject towerObject)
    {
        int towersLayer = LayerMask.NameToLayer("Towers");
        if (towersLayer == -1)
        {
            Debug.LogError("❌ La capa 'Towers' no existe");
            return;
        }

        towerObject.layer = towersLayer;
        foreach (Transform child in towerObject.transform)
        {
            child.gameObject.layer = towersLayer;
        }
    }

    // En TowerManager.cs

    public bool UpgradeTower(TowerBehaviour tower)
    {
        // 1. Chequeo de referencias
        if (tower == null)
        {
            return false;
        }

        // 2. Chequeo de si es mejorable
        if (!tower.CanUpgrade())
        {
            return false;
        }

        int upgradeCost = tower.GetUpgradeCost();
       
        // 3. Chequeo de la transacción económica
        bool transactionSuccess = EconomyManager.Instance.ProcessTransaction(
            MoneyTransactionType.TowerUpgrade,
            $"Upgrade_{TowerNaming.GetTowerName(tower)}",
            -upgradeCost
        );

        if (!transactionSuccess)
        {
            return false;
        }

        // 4. Éxito
        Debug.Log("✅ [DEBUG] ¡Éxito! Aplicando mejora...");
        tower.UpgradeTower();
        GameEvents.TriggerTowerUpgraded(tower);

        return true;
    }

    public int SellTower(TowerBehaviour tower)
    {
        if (tower == null) return 0;

        string towerName = TowerNaming.GetTowerName(tower);
        int originalCost = EconomyManager.Instance.GetTowerCost(towerName);
        int sellValue = EconomyManager.Instance.CalculateSellValue(towerName, originalCost, tower.CurrentLevel);

        bool transactionSuccess = EconomyManager.Instance.ProcessTransaction(
            MoneyTransactionType.TowerRefund,
            $"Sell_{towerName}",
            sellValue
        );

        if (transactionSuccess)
        {
            UnregisterTower(tower, towerName);

            if (GameLoopManager.TowersInGame.Contains(tower))
            {
                GameLoopManager.TowersInGame.Remove(tower);
            }

            GameEvents.TriggerTowerSold(tower);
            Destroy(tower.gameObject);

            return sellValue;
        }

        Debug.LogError("❌ Error al vender torre - transacción fallida");
        return 0;
    }

    public bool CanPlaceTowerType(string towerName)
    {
        if (!enableTowerLimits) return true;

        var limitInfo = GetTowerTypeLimitInfo(towerName);
        return limitInfo.hasSpace;
    }

    public (int current, int max, bool hasSpace) GetTowerTypeLimitInfo(string towerName)
    {
        if (!enableTowerLimits) return (0, 0, true);

        int current = towerCounts.ContainsKey(towerName) ? towerCounts[towerName] : 0;
        int max = globalTowerLimit;

        foreach (var limit in towerTypeLimits)
        {
            if (limit.towerName == towerName)
            {
                max = limit.maxTowers;
                break;
            }
        }

        return (current, max, current < max);
    }

    public void ResetTowers()
    {
        Transform towersContainer = GetTowersContainer();
        if (towersContainer != null)
        {
            foreach (Transform child in towersContainer)
            {
                if (child != null)
                {
                    Destroy(child.gameObject);
                }
            }
        }
        else
        {
            TowerBehaviour[] allTowers = FindObjectsOfType<TowerBehaviour>();
            foreach (TowerBehaviour tower in allTowers)
            {
                if (tower != null)
                {
                    Destroy(tower.gameObject);
                }
            }
        }

        placedTowers.Clear();
        InitializeTowerCounts();
        GameLoopManager.TowersInGame.Clear();
    }

    public List<TowerBehaviour> GetAllTowers()
    {
        return new List<TowerBehaviour>(placedTowers);
    }
}

[System.Serializable]
public class TowerTypeLimit
{
    public string towerName;
    public int maxTowers;
}