using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TowerButtonAutoConfig : MonoBehaviour
{
    [Header("Configuración de Torre")]
    public string towerName;

    [Header("Referencias UI")]
    public TextMeshProUGUI costText;
    public Image towerIcon;

    private Button button;
    private int currentCost;

    void Start()
    {
        button = GetComponent<Button>();
        ConfigureButtonAutomatically();
        SubscribeToEvents();
    }

    private void ConfigureButtonAutomatically()
    {
        if (TowerConfigManager.Instance == null)
        {
            Debug.LogError("❌ TowerConfigManager no encontrado!");
            return;
        }

        var config = TowerConfigManager.Instance.GetTowerConfig(towerName);

        if (config == null)
        {
            Debug.LogError($"❌ No se encontró configuración para: {towerName}");
            return;
        }

        if (costText != null)
        {
            currentCost = config.baseCost;
            costText.text = $"{currentCost}$";
        }

        if (towerIcon != null && config.towerSprite != null)
            towerIcon.sprite = config.towerSprite;
    }

    public void UpdateButtonState(int currentMoney)
    {
        if (button != null)
        {
            bool canAfford = currentMoney >= currentCost;
            button.interactable = canAfford;

            if (costText != null)
            {
                costText.color = canAfford ? Color.white : Color.red;
            }

            Image buttonImage = button.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = canAfford ? Color.white : new Color(0.7f, 0.7f, 0.7f, 0.5f);
            }
        }
    }

    private void SubscribeToEvents()
    {
        GameEvents.OnMoneyChanged += OnMoneyChanged;
    }

    private void UnsubscribeFromEvents()
    {
        GameEvents.OnMoneyChanged -= OnMoneyChanged;
    }

    private void OnMoneyChanged(int currentMoney, int previousMoney)
    {
        UpdateButtonState(currentMoney);
    }

    void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    public void OnTowerButtonClicked()
    {
        TowerPlacement placement = FindObjectOfType<TowerPlacement>();
        if (placement != null)
        {
            string methodName = GetMethodNameFromTowerName(towerName);
            var method = typeof(TowerPlacement).GetMethod(methodName);

            if (method != null)
            {
                method.Invoke(placement, null);
            }
            else
            {
                Debug.LogError($"❌ Método {methodName} no encontrado en TowerPlacement");
            }
        }
    }

    private string GetMethodNameFromTowerName(string towerName)
    {
        switch (towerName)
        {
            case "Knight Tower": return "OnKnightTowerButtonClick";
            case "Chicken Tower": return "OnChickenTowerButtonClick";
            case "Alien Tower": return "OnAlienTowerButtonClick";
            case "Couple Tower": return "OnCoupleTowerButtonClick";
            case "Mage Tower": return "OnMageTowerButtonClick";
            case "Orc Tower": return "OnOrcTowerButtonClick";
            default:
                Debug.LogError($"❌ Nombre de torre no reconocido: {towerName}");
                return "OnKnightTowerButtonClick";
        }
    }
}