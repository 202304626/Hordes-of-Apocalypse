using UnityEngine;

public static class TowerNaming
{
    public static string GetTowerName(TowerBehaviour tower)
    {
        if (tower == null) return "Knight Tower";

        string nameByComponent = GetTowerNameByComponent(tower.gameObject);
        if (!string.IsNullOrEmpty(nameByComponent)) return nameByComponent;

        return GetTowerNameByName(tower.gameObject.name);
    }

    public static string GetTowerNameFromPrefab(GameObject prefab)
    {
        if (prefab == null) return "Knight Tower";

        string nameByComponent = GetTowerNameByComponent(prefab);
        if (!string.IsNullOrEmpty(nameByComponent)) return nameByComponent;

        return GetTowerNameByName(prefab.name);
    }

    public static string GetTowerDisplayName(TowerBehaviour tower)
    {
        return GetTowerName(tower);
    }

    private static string GetTowerNameByComponent(GameObject gameObject)
    {
        TowerDamageBase damageComponent = gameObject.GetComponent<TowerDamageBase>();
        if (damageComponent != null)
        {
            return damageComponent.GetTowerType();
        }

        if (gameObject.GetComponent<KnightDamage>() != null) return "Knight Tower";
        if (gameObject.GetComponent<AlienDamage>() != null) return "Alien Tower";
        if (gameObject.GetComponent<MageDamage>() != null) return "Mage Tower";
        if (gameObject.GetComponent<OrcDamage>() != null) return "Orc Tower";
        if (gameObject.GetComponent<ChickenDamage>() != null) return "Chicken Tower";
        if (gameObject.GetComponent<CoupleDamage>() != null) return "Couple Tower";

        return null;
    }

    private static string GetTowerNameByName(string objectName)
    {
        string towerName = objectName.ToLower();

        if (towerName.Contains("knight") || towerName.Contains("caballero")) return "Knight Tower";
        if (towerName.Contains("alien") || towerName.Contains("alienigena")) return "Alien Tower";
        if (towerName.Contains("mage") || towerName.Contains("mago")) return "Mage Tower";
        if (towerName.Contains("orc") || towerName.Contains("orco")) return "Orc Tower";
        if (towerName.Contains("chicken") || towerName.Contains("pollo")) return "Chicken Tower";
        if (towerName.Contains("couple") || towerName.Contains("pareja")) return "Couple Tower";

        return "Knight Tower";
    }

    public static bool IsValidTowerName(string towerName)
    {
        string[] validNames = {
            "Knight Tower", "Alien Tower", "Mage Tower",
            "Orc Tower", "Chicken Tower", "Couple Tower"
        };

        foreach (string validName in validNames)
        {
            if (towerName == validName) return true;
        }

        return false;
    }
}