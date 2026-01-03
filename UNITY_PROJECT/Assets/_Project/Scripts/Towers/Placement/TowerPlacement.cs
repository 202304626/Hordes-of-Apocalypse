using System;
using System.Linq;
using UnityEngine;

public class TowerPlacement : MonoBehaviour
{
    [Header("Collision Settings")]
    [SerializeField] private LayerMask PlacementCheckMask;
    [SerializeField] private LayerMask PlacementCollideMask;

    [Header("References")]
    [SerializeField] private Camera PlayerCamera;
    [SerializeField] private TowerRangeVisual rangeVisual;

    private GameObject currentPlacingTower;
    private string currentTowerType;

    void Start()
    {
        if (rangeVisual == null)
        {
            rangeVisual = FindObjectOfType<TowerRangeVisual>();
            if (rangeVisual == null)
            {
                GameObject visualObj = new GameObject("TowerRangeVisual");
                rangeVisual = visualObj.AddComponent<TowerRangeVisual>();
            }
        }
    }

    private void Update()
    {
        if (currentPlacingTower != null)
        {
            Ray camRay = PlayerCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hitInfo;

            bool hitSomething = Physics.Raycast(camRay, out hitInfo, 100f, PlacementCollideMask);

            if (hitSomething)
            {
                currentPlacingTower.transform.position = hitInfo.point;
                UpdateRangeVisual();

                if (Input.GetMouseButtonDown(0))
                {
                    TowerBehaviour existingTower = hitInfo.collider.GetComponentInParent<TowerBehaviour>();
                    if (existingTower != null)
                    {
                        CancelTowerPlacement();

                        TowerUpgradeUI upgradeUI = FindObjectOfType<TowerUpgradeUI>();
                        if (upgradeUI != null)
                        {
                            upgradeUI.SelectTower(existingTower);
                        }
                        return;
                    }

                    if (IsPlacementValid(currentPlacingTower.transform.position) &&
                        !hitInfo.collider.CompareTag("CantPlace"))
                    {
                        TowerBehaviour placedTower = GameStateManager.Instance.towerManager.PurchaseTower(currentTowerType, currentPlacingTower.transform.position);

                        if (placedTower != null)
                        {
                            if (rangeVisual != null)
                            {
                                rangeVisual.HideRange();
                            }

                            Destroy(currentPlacingTower);
                            currentPlacingTower = null;
                            currentTowerType = null;

                            StartCoroutine(ActivateUpgradeMenuNextFrame(placedTower));
                        }
                        else
                        {
                            CancelTowerPlacement();
                        }
                    }
                }
            }

            if (Input.GetKeyDown(KeyCode.Q))
            {
                CancelTowerPlacement();
                return;
            }
        }
    }

    private System.Collections.IEnumerator ActivateUpgradeMenuNextFrame(TowerBehaviour tower)
    {
        yield return null;

        TowerUpgradeUI upgradeUI = FindObjectOfType<TowerUpgradeUI>();
        if (upgradeUI != null && tower != null)
        {
            upgradeUI.SelectTower(tower);
        }
    }

    public void SetTowerToPlace(string towerName)
    {
        if (currentPlacingTower != null)
        {
            Destroy(currentPlacingTower);
        }

        GameObject towerPrefab = TowerConfigManager.Instance.GetTowerPrefab(towerName);
        if (towerPrefab == null)
        {
            Debug.LogError($"❌ No se encontró prefab para: {towerName}");
            return;
        }

        int cost = TowerConfigManager.Instance.GetTowerCost(towerName);

        if (!EconomyManager.Instance.CanAfford(cost))
        {
            return;
        }

        TowerBehaviour towerBehaviour = towerPrefab.GetComponent<TowerBehaviour>();
        if (towerBehaviour == null)
        {
            Debug.LogError("❌ Tower prefab doesn't have TowerBehaviour component!");
            return;
        }

        var towerManager = GameStateManager.Instance.towerManager;
        if (towerManager.enableTowerLimits)
        {
            var limitInfo = towerManager.GetTowerTypeLimitInfo(towerName);
            if (!limitInfo.hasSpace)
            {
                return;
            }
        }

        currentTowerType = towerName;

        currentPlacingTower = Instantiate(towerPrefab, Vector3.zero, Quaternion.identity);
        SetGameObjectLayer(currentPlacingTower, "TowerPreview");

        TowerBehaviour previewBehaviour = currentPlacingTower.GetComponent<TowerBehaviour>();
        if (previewBehaviour != null)
        {
            previewBehaviour.enabled = false;
        }

        MonoBehaviour damageMethod = currentPlacingTower.GetComponent<MonoBehaviour>();
        if (damageMethod != null && damageMethod is IDamageMethod)
        {
            damageMethod.enabled = false;
        }

        float range = GetTowerRangeDirectly(towerPrefab);
        bool hasLimitSpace = GameStateManager.Instance.towerManager.CanPlaceTowerType(currentTowerType);

        if (rangeVisual != null)
        {
            rangeVisual.ShowPlacingRange(currentTowerType, currentPlacingTower.transform.position, range, true, hasLimitSpace);
        }
    }

    private float GetTowerRangeDirectly(GameObject towerPrefab)
    {
        string towerName = TowerNaming.GetTowerNameFromPrefab(towerPrefab);
        var towerConfig = TowerConfigManager.Instance.GetTowerConfig(towerName);
        if (towerConfig != null)
        {
            var stats = towerConfig.GetStatsForLevel(1);
            return stats.range;
        }

        Debug.LogError($"❌ No se pudo obtener configuración de {towerName}");
        return 10f;
    }

    private void SetGameObjectLayer(GameObject obj, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer == -1)
        {
            Debug.LogError($"❌ La capa '{layerName}' no existe.");
            return;
        }

        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            child.gameObject.layer = layer;
        }
    }

    private void CancelTowerPlacement()
    {
        TowerUpgradeUI upgradeUI = FindObjectOfType<TowerUpgradeUI>();
        if (upgradeUI != null)
        {
            upgradeUI.CloseUpgradePanel();
        }

        if (currentPlacingTower != null)
        {
            Destroy(currentPlacingTower);
            currentPlacingTower = null;
            currentTowerType = null;
        }

        if (rangeVisual != null && !rangeVisual.IsShowingSelectedRange())
        {
            rangeVisual.HideRange();
        }
    }

    private void UpdateRangeVisual()
    {
        if (currentPlacingTower != null && rangeVisual != null)
        {
            float range = GetTowerRangeDirectly(currentPlacingTower);
            Vector3 position = currentPlacingTower.transform.position;

            bool isValid = IsPlacementValid(position);
            bool hasLimitSpace = GameStateManager.Instance.towerManager.CanPlaceTowerType(currentTowerType);

            rangeVisual.UpdatePlacingRange(position, range, isValid, hasLimitSpace);
        }
    }

    private bool IsPlacementValid(Vector3 position)
    {
        Ray ray = new Ray(position + Vector3.up * 10f, Vector3.down);
        RaycastHit hit;
        bool isOnGround = Physics.Raycast(ray, out hit, 15f, PlacementCollideMask);

        if (!isOnGround)
            return false;

        if (hit.collider.CompareTag("CantPlace"))
        {
            return false;
        }

        if (IsCollidingWithOtherTowers(position))
        {
            return false;
        }

        return true;
    }

    private bool IsCollidingWithOtherTowers(Vector3 position)
    {
        float collisionRadius = 1.0f;
        int towersLayer = LayerMask.NameToLayer("Towers");
        int towerLayerMask = 1 << towersLayer;

        Collider[] colliders = Physics.OverlapSphere(position, collisionRadius, towerLayerMask);

        if (currentPlacingTower != null)
        {
            colliders = colliders.Where(c => c.gameObject != currentPlacingTower).ToArray();
        }

        return colliders.Length > 0;
    }

    public bool IsPlacingTower()
    {
        return currentPlacingTower != null;
    }

    public void OnKnightTowerButtonClick() => SetTowerToPlace("Knight Tower");
    public void OnChickenTowerButtonClick() => SetTowerToPlace("Chicken Tower");
    public void OnAlienTowerButtonClick() => SetTowerToPlace("Alien Tower");
    public void OnCoupleTowerButtonClick() => SetTowerToPlace("Couple Tower");
    public void OnMageTowerButtonClick() => SetTowerToPlace("Mage Tower");
    public void OnOrcTowerButtonClick() => SetTowerToPlace("Orc Tower");
}