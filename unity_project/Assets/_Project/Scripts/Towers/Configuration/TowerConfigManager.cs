using System.Collections.Generic;
using UnityEngine;

public class TowerConfigManager : MonoBehaviour
{
    public static TowerConfigManager Instance { get; private set; }

    [System.Serializable]
    public class TowerConfig
    {
        [Header("Identificación")]
        public string towerName;
        public GameObject towerPrefab;
        public string description;
        public Sprite towerSprite;

        [Header("Costos")]
        public int baseCost = 0;
        public int baseUpgradeCost = 0;
        [Range(0.1f, 1f)]
        public float sellPercentage = 0f;

        [Header("Estadísticas Base (Nivel 1)")]
        public float baseDamage = 0f;
        public float baseRange = 0f;
        public float baseFireRate = 0f;

        [Header("Mejoras por Nivel")]
        public float damageIncreasePerLevel = 0f;
        public float rangeIncreasePerLevel = 0f;
        public float fireRateIncreasePerLevel = 0f;
        public int maxLevel = 0;

        public (float damage, float range, float fireRate) GetStatsForLevel(int level)
        {
            level = Mathf.Clamp(level, 1, maxLevel);
            float damage = baseDamage * Mathf.Pow(1 + damageIncreasePerLevel, level - 1);
            float range = baseRange * Mathf.Pow(1 + rangeIncreasePerLevel, level - 1);
            float fireRate = baseFireRate * Mathf.Pow(1 + fireRateIncreasePerLevel, level - 1);

            return (damage, range, fireRate);
        }

        public int GetUpgradeCostForLevel(int currentLevel)
        {
            return Mathf.RoundToInt(baseUpgradeCost * Mathf.Pow(1.5f, currentLevel - 1));
        }
    }

    [Header("Configuraciones de Torres")]
    public List<TowerConfig> towerConfigs = new List<TowerConfig>();

    private Dictionary<string, TowerConfig> towerConfigDict;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializeConfigs();
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeConfigs()
    {
        towerConfigDict = new Dictionary<string, TowerConfig>();

        foreach (var config in towerConfigs)
        {
            if (string.IsNullOrEmpty(config.towerName))
            {
                Debug.LogError("❌ Torre sin nombre en la configuración!");
                continue;
            }

            ApplyDefaultValues(config);

            if (config.towerPrefab == null)
            {
                config.towerPrefab = FindTowerPrefabByName(config.towerName);
                if (config.towerPrefab == null)
                {
                    Debug.LogError($"❌ No se pudo encontrar prefab para: {config.towerName}");
                    continue;
                }
            }

            towerConfigDict[config.towerName] = config;
        }
    }

    private void ApplyDefaultValues(TowerConfig config)
    {
        var defaultValues = GetDefaultTowerValues(config.towerName);

        if (config.baseCost == 0) config.baseCost = defaultValues.baseCost;
        if (config.baseUpgradeCost == 0) config.baseUpgradeCost = defaultValues.baseUpgradeCost;
        if (config.sellPercentage == 0) config.sellPercentage = defaultValues.sellPercentage;

        if (config.baseDamage == 0) config.baseDamage = defaultValues.baseDamage;
        if (config.baseRange == 0) config.baseRange = defaultValues.baseRange;
        if (config.baseFireRate == 0) config.baseFireRate = defaultValues.baseFireRate;

        if (config.damageIncreasePerLevel == 0) config.damageIncreasePerLevel = defaultValues.damageIncreasePerLevel;
        if (config.rangeIncreasePerLevel == 0) config.rangeIncreasePerLevel = defaultValues.rangeIncreasePerLevel;
        if (config.fireRateIncreasePerLevel == 0) config.fireRateIncreasePerLevel = defaultValues.fireRateIncreasePerLevel;

        if (config.maxLevel == 0) config.maxLevel = defaultValues.maxLevel;

        if (string.IsNullOrEmpty(config.description))
            config.description = defaultValues.description;
    }

    private TowerConfig GetDefaultTowerValues(string towerName)
    {
        var defaultValues = new Dictionary<string, TowerConfig>
        {
            {
                "Knight Tower", new TowerConfig {
                    towerName = "Knight Tower",
                    baseCost = 130,
                    baseDamage = 15,
                    baseRange = 4.8f,
                    baseFireRate = 1.0f,
                    damageIncreasePerLevel = 0.28f,
                    rangeIncreasePerLevel = 0.09f,
                    fireRateIncreasePerLevel = 0.14f,
                    baseUpgradeCost = 90,
                    sellPercentage = 0.6f,
                    maxLevel = 6,
                    description = "Torre equilibrada, buena para cualquier situación"
                }
            },
            {
                "Orc Tower", new TowerConfig {
                    towerName = "Orc Tower",
                    baseCost = 170,
                    baseDamage = 22,
                    baseRange = 3.2f,
                    baseFireRate = 0.65f,
                    damageIncreasePerLevel = 0.35f,
                    rangeIncreasePerLevel = 0.07f,
                    fireRateIncreasePerLevel = 0.16f,
                    baseUpgradeCost = 110,
                    sellPercentage = 0.55f,
                    maxLevel = 6,
                    description = "Alto daño pero rango corto y ataque lento"
                }
            },
            {
                "Mage Tower", new TowerConfig {
                    towerName = "Mage Tower",
                    baseCost = 190,
                    baseDamage = 18,
                    baseRange = 6.5f,
                    baseFireRate = 0.85f,
                    damageIncreasePerLevel = 0.30f,
                    rangeIncreasePerLevel = 0.12f,
                    fireRateIncreasePerLevel = 0.11f,
                    baseUpgradeCost = 120,
                    sellPercentage = 0.58f,
                    maxLevel = 6,
                    description = "Rango extremo con daño consistente"
                }
            },
            {
                "Chicken Tower", new TowerConfig {
                    towerName = "Chicken Tower",
                    baseCost = 100,
                    baseDamage = 8,
                    baseRange = 2.8f,
                    baseFireRate = 2.2f,
                    damageIncreasePerLevel = 0.22f,
                    rangeIncreasePerLevel = 0.06f,
                    fireRateIncreasePerLevel = 0.20f,
                    baseUpgradeCost = 65,
                    sellPercentage = 0.65f,
                    maxLevel = 6,
                    description = "Ataque muy rápido pero daño y rango bajos"
                }
            },
            {
                "Alien Tower", new TowerConfig {
                    towerName = "Alien Tower",
                    baseCost = 220,
                    baseDamage = 20,
                    baseRange = 8.0f,
                    baseFireRate = 0.75f,
                    damageIncreasePerLevel = 0.25f,
                    rangeIncreasePerLevel = 0.14f,
                    fireRateIncreasePerLevel = 0.09f,
                    baseUpgradeCost = 140,
                    sellPercentage = 0.57f,
                    maxLevel = 6,
                    description = "Rango masivo con daño sólido"
                }
            },
            {
                "Couple Tower", new TowerConfig {
                    towerName = "Couple Tower",
                    baseCost = 150,
                    baseDamage = 10,
                    baseRange = 4.2f,
                    baseFireRate = 1.4f,
                    damageIncreasePerLevel = 0.20f,
                    rangeIncreasePerLevel = 0.08f,
                    fireRateIncreasePerLevel = 0.15f,
                    baseUpgradeCost = 95,
                    sellPercentage = 0.62f,
                    maxLevel = 6,
                    description = "Buffea torres cercanas (+18% daño)"
                }
            }
        };

        if (defaultValues.ContainsKey(towerName))
            return defaultValues[towerName];
        else
        {
            Debug.LogWarning($"⚠️ No hay valores por defecto para: {towerName}");
            return new TowerConfig
            {
                baseCost = 100,
                baseDamage = 10,
                baseRange = 5f,
                baseFireRate = 1f,
                damageIncreasePerLevel = 0.2f,
                rangeIncreasePerLevel = 0.1f,
                fireRateIncreasePerLevel = 0.15f,
                baseUpgradeCost = 60,
                sellPercentage = 0.6f,
                maxLevel = 6,
                description = "Torre personalizada"
            };
        }
    }

    private GameObject FindTowerPrefabByName(string towerName)
    {
        GameObject prefab = Resources.Load<GameObject>($"Towers/{towerName}");
        if (prefab != null) return prefab;

        prefab = Resources.Load<GameObject>($"Prefabs/Towers/{towerName}");
        if (prefab != null) return prefab;

        GameObject[] allPrefabs = Resources.LoadAll<GameObject>("");
        foreach (GameObject p in allPrefabs)
        {
            if (p.name.ToLower().Contains(towerName.ToLower()))
            {
                return p;
            }
        }

        Debug.LogError($"❌ No se encontró prefab para: {towerName}");
        return null;
    }

    public TowerConfig GetTowerConfig(string towerName)
    {
        if (towerConfigDict.ContainsKey(towerName))
        {
            return towerConfigDict[towerName];
        }

        Debug.LogWarning($"🏰 Torre no configurada: {towerName}");
        return null;
    }

    public int GetTowerCost(string towerName)
    {
        var config = GetTowerConfig(towerName);
        return config?.baseCost ?? 100;
    }

    public int GetUpgradeCost(string towerName, int currentLevel)
    {
        var config = GetTowerConfig(towerName);
        if (config == null) return 50;

        return config.GetUpgradeCostForLevel(currentLevel);
    }

    public (float damage, float range, float fireRate) GetTowerStats(string towerName, int level)
    {
        var config = GetTowerConfig(towerName);
        if (config == null) return (5f, 10f, 1f);

        return config.GetStatsForLevel(level);
    }

    public float GetSellPercentage(string towerName)
    {
        var config = GetTowerConfig(towerName);
        return config?.sellPercentage ?? 0.5f;
    }

    public GameObject GetTowerPrefab(string towerName)
    {
        var config = GetTowerConfig(towerName);
        if (config?.towerPrefab == null)
        {
            return FindTowerPrefabByName(towerName);
        }
        return config.towerPrefab;
    }

    public List<TowerConfig> GetAllTowerConfigs()
    {
        return new List<TowerConfig>(towerConfigs);
    }

    public void PrintAllTowerStats()
    {
        Debug.Log("=== 🏰 ESTADÍSTICAS DE TORRES ===");
        foreach (var config in towerConfigs)
        {
            var lvl1 = config.GetStatsForLevel(1);
            var lvl6 = config.GetStatsForLevel(config.maxLevel);

            Debug.Log($"{config.towerName}:");
            Debug.Log($"  Nvl1 - Daño: {lvl1.damage:F1}, Rango: {lvl1.range:F1}, Vel: {lvl1.fireRate:F2}");
            Debug.Log($"  Nvl6 - Daño: {lvl6.damage:F1}, Rango: {lvl6.range:F1}, Vel: {lvl6.fireRate:F2}");
            Debug.Log($"  Costos: Base ${config.baseCost}, Mejora ${config.baseUpgradeCost}");
        }
    }
}