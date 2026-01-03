using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TowerUpgradeUI : MonoBehaviour
{
    [Header("Referencias UI")]
    public GameObject upgradePanel;
    public Button upgradeButton;
    public Button closeButton;
    public TextMeshProUGUI upgradeCostText;
    public TextMeshProUGUI towerLevelText;
    public TextMeshProUGUI towerNameText;

    [Header("Estadísticas Actuales")]
    public TextMeshProUGUI currentDamageText;
    public TextMeshProUGUI currentRangeText;
    public TextMeshProUGUI currentFirerateText;

    [Header("Estadísticas Siguiente Nivel")]
    public TextMeshProUGUI nextDamageText;
    public TextMeshProUGUI nextRangeText;
    public TextMeshProUGUI nextFirerateText;

    [Header("Botón de Vender")]
    public Button sellButton;
    public TextMeshProUGUI sellValueText;

    [Header("Barra de Progreso")]
    public Image levelProgressBar;
    public TextMeshProUGUI progressText;

    [Header("Colores de la Barra")]
    public Color maxLevelColor = Color.blue;

    [Header("Animación de Barra")]
    public bool animateProgress = true;
    public float animationSpeed = 2f;

    [Header("Sistema de Targeting")]
    public GameObject targetingPanel;
    public Button targetingButton;
    public Button[] targetTypeButtons;
    public TextMeshProUGUI currentTargetingText;

    [Header("Efectos de Texto")]
    public TextEffectManager damageTextEffect;
    public TextEffectManager rangeTextEffect;
    public TextEffectManager firerateTextEffect;
    public TextEffectManager costTextEffect;
    public TextEffectManager levelTextEffect;

    [Header("Configuración de Textos")]
    public bool makeAllTextsBold = true;
    public bool addOutlineToTexts = true;
    public float outlineSize = 0.2f;

    [Header("Tower Image Display")]
    public Image towerHeaderImage;
    public Sprite defaultTowerSprite;

    [Header("Colores de Texto")]
    public Color upgradeAvailableColor = Color.white;
    public Color upgradeMaxLevelColor = Color.yellow;
    public Color upgradeCantAffordColor = Color.red;
    public Color sellTextColor = Color.white;
    public Color normalTextColor = Color.white;

    [Header("Actualización Automática")]
    public bool enableAutoUpdate = true;

    [System.Serializable]
    public class TowerSpriteMapping
    {
        public string towerName;
        public Sprite towerSprite;
    }

    public List<TowerSpriteMapping> towerSpriteMappings = new List<TowerSpriteMapping>();

    private TowerBehaviour selectedTower;
    private Camera playerCamera;
    private TowerRangeVisual rangeVisual;

    private bool isUpgrading = false;
    private float lastUpgradeTime = 0f;
    private float upgradeCooldown = 0.1f;

    private bool isPanelOpen = false;
    private Coroutine currentAnimationCoroutine;
    private Dictionary<TextMeshProUGUI, Vector3> originalTextScales = new Dictionary<TextMeshProUGUI, Vector3>();
    private Dictionary<string, Sprite> towerSprites = new Dictionary<string, Sprite>();

    private float targetFillAmount = 0f;
    private float currentFillAmount = 0f;
    private int lastKnownMoney = 0;

    private void Start()
    {
        playerCamera = Camera.main;
        upgradePanel.SetActive(false);

        InitializeTowerSprites();
        rangeVisual = FindObjectOfType<TowerRangeVisual>();

        SetupButtonListeners();
        SetupTargetingSystem();
        ApplySimpleTextStyles();
        SubscribeToMoneyEvents();
    }

    void Update()
    {
        HandleTowerSelection();

        if (isPanelOpen && selectedTower == null)
        {
            CloseUpgradePanel();
            return;
        }

        if (animateProgress && selectedTower != null && isPanelOpen)
        {
            UpdateProgressBarWithAnimation();
        }

        if (isPanelOpen && sellValueText != null)
        {
            sellValueText.color = Color.white;
        }
    }

    private void SubscribeToMoneyEvents()
    {
        GameEvents.OnMoneyChanged -= OnMoneyChanged;
        GameEvents.OnMoneyChanged += OnMoneyChanged;

        if (EconomyManager.Instance != null)
        {
            lastKnownMoney = EconomyManager.Instance.CurrentMoney;
        }
    }

    private void OnMoneyChanged(int currentMoney, int previousMoney)
    {
        if (!enableAutoUpdate) return;

        if (isPanelOpen && selectedTower != null)
        {
            if (currentMoney > lastKnownMoney)
            {
                StartCoroutine(UpdateUIAfterMoneyChange());
            }
            lastKnownMoney = currentMoney;
        }
    }

    private IEnumerator UpdateUIAfterMoneyChange()
    {
        yield return null;

        if (isPanelOpen && selectedTower != null)
        {
            UpdateUpgradeUI();

            if (CanAffordUpgradeNow())
            {
                PlayMoneyAvailableEffect();
            }
        }
    }

    private void PlayMoneyAvailableEffect()
    {
        if (upgradeCostText != null)
        {
            StartCoroutine(MoneyAvailableEffectCoroutine());
        }
    }

    private IEnumerator MoneyAvailableEffectCoroutine()
    {
        Color originalColor = upgradeCostText.color;

        for (int i = 0; i < 3; i++)
        {
            upgradeCostText.color = new Color(0.2f, 1f, 0.2f);
            yield return new WaitForSeconds(0.2f);
            upgradeCostText.color = originalColor;
            yield return new WaitForSeconds(0.2f);
        }

        upgradeCostText.color = originalColor;
    }

    private bool CanAffordUpgradeNow()
    {
        if (selectedTower == null || !selectedTower.CanUpgrade()) return false;
        int upgradeCost = selectedTower.GetUpgradeCost();
        return EconomyManager.Instance.CanAfford(upgradeCost);
    }

    void SetupButtonListeners()
    {
        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveAllListeners();
            upgradeButton.onClick.AddListener(OnUpgradeButtonClick);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseUpgradePanel);
        }

        if (sellButton != null)
        {
            sellButton.onClick.RemoveAllListeners();
            sellButton.onClick.AddListener(OnSellButtonClick);
        }
    }

    private void SetupTargetingSystem()
    {
        if (targetingButton != null)
        {
            targetingButton.onClick.RemoveAllListeners();
            targetingButton.onClick.AddListener(ToggleTargetingPanel);
        }

        if (targetTypeButtons != null && targetTypeButtons.Length >= 5)
        {
            targetTypeButtons[0].onClick.AddListener(() => SetTargetingType(TowerTargeting.TargetType.First));
            targetTypeButtons[1].onClick.AddListener(() => SetTargetingType(TowerTargeting.TargetType.Last));
            targetTypeButtons[2].onClick.AddListener(() => SetTargetingType(TowerTargeting.TargetType.Close));
            targetTypeButtons[3].onClick.AddListener(() => SetTargetingType(TowerTargeting.TargetType.Strong));
            targetTypeButtons[4].onClick.AddListener(() => SetTargetingType(TowerTargeting.TargetType.Weak));
        }

        if (targetingPanel != null)
            targetingPanel.SetActive(false);
    }


    void HandleTowerSelection()
    {
        if (Input.GetMouseButtonDown(0))
        {
            TowerPlacement towerPlacement = FindObjectOfType<TowerPlacement>();
            if (towerPlacement != null && towerPlacement.IsPlacingTower())
            {
                return;
            }

            if (IsPointerOverUpgradePanel())
            {
                return;
            }

            bool hitTower = TryDetectTower(out TowerBehaviour hitTowerComponent);

            if (hitTower && hitTowerComponent != null)
            {
                SelectTower(hitTowerComponent);
                return;
            }

            if (selectedTower != null)
            {
                CloseUpgradePanel();
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseUpgradePanel();
        }
    }

    bool TryDetectTower(out TowerBehaviour tower)
    {
        tower = null;
        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        int towerLayer = LayerMask.GetMask("Towers");
        if (towerLayer == 0)
        {
            if (Physics.Raycast(ray, out hit, 100f))
            {
                tower = hit.collider.GetComponentInParent<TowerBehaviour>();
                return tower != null;
            }
        }
        else
        {
            if (Physics.Raycast(ray, out hit, 100f, towerLayer))
            {
                tower = hit.collider.GetComponentInParent<TowerBehaviour>();
                if (tower != null)
                {
                    return true;
                }
            }
        }

        if (Physics.Raycast(ray, out hit, 100f))
        {
            tower = hit.collider.GetComponentInParent<TowerBehaviour>();
            if (tower != null)
            {
                return true;
            }
        }
        return false;
    }

    bool IsPointerOverUpgradePanel()
    {
        if (upgradePanel == null || !upgradePanel.activeInHierarchy)
            return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = Input.mousePosition;

        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (var result in results)
        {
            if (result.gameObject == upgradePanel || result.gameObject.transform.IsChildOf(upgradePanel.transform))
            {
                return true;
            }
        }

        return false;
    }

    public void SelectTower(TowerBehaviour tower)
    {
        if (tower == null) return;

        StopAllAnimations();
        SaveOriginalScales();

        selectedTower = tower;
        UpdateUpgradeUI();
        UpdateTargetingUI();
        UpdateTowerHeaderImage(tower);
        ShowSelectedTowerRange();

        if (targetingPanel != null)
            targetingPanel.SetActive(false);

        if (upgradePanel != null)
        {
            upgradePanel.SetActive(true);
            isPanelOpen = true;
        }
    }

    private void SaveOriginalScales()
    {
        if (originalTextScales.Count > 0) return;

        TextMeshProUGUI[] allTexts = upgradePanel.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI text in allTexts)
        {
            originalTextScales[text] = text.transform.localScale;
        }
    }

    private void StopAllAnimations()
    {
        if (currentAnimationCoroutine != null)
        {
            StopCoroutine(currentAnimationCoroutine);
            currentAnimationCoroutine = null;
        }

        StopAllCoroutines();
        ResetAllTextEffects();
    }

    private void ResetAllTextEffects()
    {
        try
        {
            TextEffectManager[] textEffects = GetComponentsInChildren<TextEffectManager>(true);

            foreach (TextEffectManager effect in textEffects)
            {
                if (effect == null || !effect.isActiveAndEnabled)
                    continue;

                effect.SetBlinkEffect(false, Color.red, 0f);
                effect.SetPulseEffect(false, 0f, 0f);
            }
        }
        catch (System.Exception) { }
    }

    private void ShowSelectedTowerRange()
    {
        if (rangeVisual != null && selectedTower != null)
        {
            rangeVisual.ShowSelectedRange(selectedTower);
        }
    }

    private void HideSelectedTowerRange()
    {
        if (rangeVisual != null)
        {
            rangeVisual.HideRange();
        }
    }

    public void CloseUpgradePanel()
    {
        StopAllAnimations();
        RestoreOriginalScales();
        HideSelectedTowerRange();
        ResetAllTextColors();

        selectedTower = null;
        isPanelOpen = false;

        if (upgradePanel != null)
            upgradePanel.SetActive(false);

        if (targetingPanel != null)
            targetingPanel.SetActive(false);
    }

    private void ResetAllTextColors()
    {
        if (upgradeCostText != null) upgradeCostText.color = normalTextColor;

        if (sellValueText != null)
        {
            sellValueText.color = Color.white;

            TextEffectManager sellEffect = sellValueText.GetComponent<TextEffectManager>();
            if (sellEffect != null)
            {
                sellEffect.SetBlinkEffect(false, Color.red, 0f);
                sellEffect.SetPulseEffect(false, 0f, 0f);
            }
        }

        if (towerLevelText != null) towerLevelText.color = normalTextColor;
        if (towerNameText != null) towerNameText.color = normalTextColor;

        if (currentDamageText != null) currentDamageText.color = normalTextColor;
        if (currentRangeText != null) currentRangeText.color = normalTextColor;
        if (currentFirerateText != null) currentFirerateText.color = normalTextColor;

        if (nextDamageText != null) nextDamageText.color = normalTextColor;
        if (nextRangeText != null) nextRangeText.color = normalTextColor;
        if (nextFirerateText != null) nextFirerateText.color = normalTextColor;
    }

    private void RestoreOriginalScales()
    {
        foreach (var kvp in originalTextScales)
        {
            if (kvp.Key != null)
            {
                kvp.Key.transform.localScale = kvp.Value;
            }
        }
    }

    private void UpdateUpgradeUI()
    {
        if (selectedTower == null)
        {
            if (upgradePanel != null)
                upgradePanel.SetActive(false);
            return;
        }

        towerNameText.text = TowerNaming.GetTowerDisplayName(selectedTower);
        towerLevelText.text = $"Nivel {selectedTower.CurrentLevel}/{selectedTower.MaxLevel}";

        UpdateCurrentStatsUI();
        UpdateNextLevelStatsUI();

        int upgradeCost = selectedTower.GetUpgradeCost();

        if (upgradeCostText != null)
            upgradeCostText.text = selectedTower.CanUpgrade() ? $"Mejorar: ${upgradeCost}" : "NIVEL MÁXIMO";

        bool canUpgrade = selectedTower.CanUpgrade();
        bool canAfford = EconomyManager.Instance.CanAfford(upgradeCost);
        bool upgradeAllowed = canUpgrade && canAfford;

        if (upgradeButton != null)
        {
            upgradeButton.interactable = upgradeAllowed;
        }

        if (upgradeCostText != null)
        {
            if (!canUpgrade)
                upgradeCostText.color = upgradeMaxLevelColor;
            else if (!canAfford)
                upgradeCostText.color = upgradeCantAffordColor;
            else
                upgradeCostText.color = upgradeAvailableColor;
        }

        if (sellValueText != null)
        {
            int sellValue = CalculateSellValue(selectedTower);
            sellValueText.text = $"{sellValue}$";
            sellValueText.color = Color.white; 

            TextEffectManager sellTextEffect = sellValueText.GetComponent<TextEffectManager>();
            if (sellTextEffect != null)
            {
                sellTextEffect.SetBlinkEffect(false, Color.red, 0f);
                sellTextEffect.SetPulseEffect(false, 0f, 0f);
            }
        }

        if (sellButton != null)
            sellButton.interactable = true;

        ApplyTextEffectsBasedOnState();
        UpdateProgressBarWithAnimation();
    }

    private void UpdateCurrentStatsUI()
    {
        if (selectedTower == null) return;

        if (currentDamageText != null)
            currentDamageText.text = $"{selectedTower.Damage:F1}";

        if (currentRangeText != null)
            currentRangeText.text = $"{selectedTower.Range:F1}";

        if (currentFirerateText != null)
        {
            float currentDelay = 1f / selectedTower.FireRate;
            currentFirerateText.text = $"{currentDelay:F2}s";
        }
    }

    private void UpdateNextLevelStatsUI()
    {
        if (selectedTower == null) return;

        if (selectedTower.CanUpgrade())
        {
            var nextStats = selectedTower.GetNextLevelStats();

            if (nextDamageText != null)
            {
                nextDamageText.text = $"{nextStats.nextDamage:F1}";
                nextDamageText.color = Color.green;
            }

            if (nextRangeText != null)
            {
                nextRangeText.text = $"{nextStats.nextRange:F1}";
                nextRangeText.color = Color.green;
            }

            if (nextFirerateText != null)
            {
                float nextDelay = 1f / nextStats.nextFireRate;
                nextFirerateText.text = $"{nextDelay:F2}s";
                nextFirerateText.color = Color.green;
            }
        }
        else
        {
            if (nextDamageText != null)
            {
                nextDamageText.text = "MAX";
                nextDamageText.color = Color.yellow;
            }

            if (nextRangeText != null)
            {
                nextRangeText.text = "MAX";
                nextRangeText.color = Color.yellow;
            }

            if (nextFirerateText != null)
            {
                nextFirerateText.text = "MAX";
                nextFirerateText.color = Color.yellow;
            }
        }
    }

    private void ApplySimpleTextStyles()
    {
        if (!makeAllTextsBold && !addOutlineToTexts) return;

        TextMeshProUGUI[] allTexts = GetComponentsInChildren<TextMeshProUGUI>(true);

        foreach (TextMeshProUGUI text in allTexts)
        {
            if (makeAllTextsBold)
            {
                text.fontStyle = FontStyles.Bold;
            }

            if (addOutlineToTexts)
            {
                text.outlineWidth = outlineSize;
                text.outlineColor = Color.black;
            }
        }
    }

    private void ApplyTextEffectsBasedOnState()
    {
        if (selectedTower == null) return;

        int upgradeCost = selectedTower.GetUpgradeCost();
        bool canAfford = EconomyManager.Instance.CanAfford(upgradeCost);

        if (upgradeCostText != null)
        {
            if (!selectedTower.CanUpgrade())
                upgradeCostText.color = upgradeMaxLevelColor;
            else if (!canAfford)
                upgradeCostText.color = upgradeCantAffordColor;
            else
                upgradeCostText.color = upgradeAvailableColor;
        }

        if (sellValueText != null)
        {
            TextEffectManager sellEffect = sellValueText.GetComponent<TextEffectManager>();
            if (sellEffect != null)
            {
                sellEffect.SetBlinkEffect(false, Color.red, 0f);
                sellEffect.SetPulseEffect(false, 0f, 0f);
            }
            sellValueText.color = Color.white;
        }
    }

    public void OnUpgradeButtonClick()
    {
        if (!isPanelOpen || selectedTower == null || isUpgrading) return;
        if (Time.time - lastUpgradeTime < upgradeCooldown) return;

        isUpgrading = true;
        lastUpgradeTime = Time.time;

        if (upgradeButton != null)
            upgradeButton.interactable = false;

        try
        {
            // ✅ SOLUCIÓN: Confiar ÚNICAMENTE en UpgradeTower para todas las verificaciones
            bool success = GameStateManager.Instance.towerManager.UpgradeTower(selectedTower);

            if (success)
            {
                // Éxito - reproducir efectos
                if (isPanelOpen)
                {
                    currentAnimationCoroutine = StartCoroutine(PlayAllUpgradeEffects());
                    UpdateUpgradeUI();
                    ShowSelectedTowerRange();
                }
            }
           
        }
        catch (System.Exception)
        {
        }
        finally
        {
            StartCoroutine(ResetUpgradeStateAfterDelay());
        }
    }
    private IEnumerator ResetUpgradeStateAfterDelay()
    {
        yield return new WaitForSeconds(0.3f);

        if (isPanelOpen && selectedTower != null)
        {
            ResetUpgradeState();
        }
        else
        {
            isUpgrading = false;
        }
    }

    private void ResetUpgradeState()
    {
        isUpgrading = false;

        if (upgradeButton != null && selectedTower != null)
        {
            bool canStillUpgrade = selectedTower.CanUpgrade();
            int upgradeCost = selectedTower.GetUpgradeCost();
            bool canAfford = EconomyManager.Instance.CanAfford(upgradeCost);

            upgradeButton.interactable = canStillUpgrade && canAfford;
        }
    }

    private IEnumerator PlayAllUpgradeEffects()
    {
        if (!isPanelOpen || selectedTower == null) yield break;

        Dictionary<TextMeshProUGUI, Color> originalColors = new Dictionary<TextMeshProUGUI, Color>();
        var textColorPairs = new List<(TextMeshProUGUI text, Color color)>();

        if (currentDamageText != null)
        {
            originalColors[currentDamageText] = currentDamageText.color;
            textColorPairs.Add((currentDamageText, new Color(1f, 0.3f, 0.3f)));
        }

        if (currentRangeText != null)
        {
            originalColors[currentRangeText] = currentRangeText.color;
            textColorPairs.Add((currentRangeText, new Color(1f, 0.8f, 0.3f)));
        }

        if (currentFirerateText != null)
        {
            originalColors[currentFirerateText] = currentFirerateText.color;
            textColorPairs.Add((currentFirerateText, new Color(0.3f, 0.6f, 1f)));
        }

        if (nextDamageText != null)
        {
            originalColors[nextDamageText] = nextDamageText.color;
            textColorPairs.Add((nextDamageText, new Color(1f, 0.3f, 0.3f)));
        }

        if (nextRangeText != null)
        {
            originalColors[nextRangeText] = nextRangeText.color;
            textColorPairs.Add((nextRangeText, new Color(1f, 0.8f, 0.3f)));
        }

        if (nextFirerateText != null)
        {
            originalColors[nextFirerateText] = nextFirerateText.color;
            textColorPairs.Add((nextFirerateText, new Color(0.3f, 0.6f, 1f)));
        }

        if (towerLevelText != null)
        {
            originalColors[towerLevelText] = towerLevelText.color;
            textColorPairs.Add((towerLevelText, new Color(0.3f, 1f, 0.3f)));
        }

        foreach (var (text, color) in textColorPairs)
        {
            if (text != null)
                text.color = color;
        }

        yield return new WaitForSeconds(0.4f);

        if (!isPanelOpen || selectedTower == null)
        {
            RestoreOriginalColors(originalColors);
            yield break;
        }

        float duration = 0.6f;
        float elapsed = 0f;

        while (elapsed < duration && isPanelOpen && selectedTower != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            foreach (var (text, effectColor) in textColorPairs)
            {
                if (text != null && originalColors.ContainsKey(text))
                {
                    text.color = Color.Lerp(effectColor, originalColors[text], t);
                }
            }
            yield return null;
        }

        if (isPanelOpen && selectedTower != null)
        {
            RestoreOriginalColors(originalColors);
            UpdateUpgradeUI();
        }
    }

    private void RestoreOriginalColors(Dictionary<TextMeshProUGUI, Color> originalColors)
    {
        foreach (var kvp in originalColors)
        {
            if (kvp.Key != null)
                kvp.Key.color = kvp.Value;
        }
    }

    public void OnSellButtonClick()
    {
        if (selectedTower == null) return;

        int actualValue = GameStateManager.Instance.towerManager.SellTower(selectedTower);

        if (actualValue > 0)
        {
            CloseUpgradePanel();
        }
    }

    private int CalculateSellValue(TowerBehaviour tower)
    {
        if (tower == null) return 0;

        string towerName = TowerNaming.GetTowerName(tower);
        int originalCost = EconomyManager.Instance.GetTowerCost(towerName);
        int sellValue = EconomyManager.Instance.CalculateSellValue(towerName, originalCost, tower.CurrentLevel);

        return sellValue;
    }

    private void ToggleTargetingPanel()
    {
        if (targetingPanel != null)
        {
            bool isActive = !targetingPanel.activeInHierarchy;
            targetingPanel.SetActive(isActive);

            if (isActive)
            {
                UpdateTargetingUI();
            }
        }
    }

    private void SetTargetingType(TowerTargeting.TargetType targetType)
    {
        if (selectedTower != null)
        {
            selectedTower.TargetingMethod = targetType;
            UpdateTargetingUI();

            if (targetingPanel != null)
                targetingPanel.SetActive(false);
        }
    }

    private void UpdateTargetingUI()
    {
        if (selectedTower != null && currentTargetingText != null)
        {
            string targetName = GetTargetingDisplayName(selectedTower.TargetingMethod);
            currentTargetingText.text = $"{targetName}";
        }
    }

    private string GetTargetingDisplayName(TowerTargeting.TargetType targetType)
    {
        switch (targetType)
        {
            case TowerTargeting.TargetType.First: return "First";
            case TowerTargeting.TargetType.Last: return "Last";
            case TowerTargeting.TargetType.Close: return "Close";
            case TowerTargeting.TargetType.Strong: return "Strong";
            case TowerTargeting.TargetType.Weak: return "Weak";
            default: return "First";
        }
    }

    private void UpdateProgressBarWithAnimation()
    {
        if (levelProgressBar == null || selectedTower == null) return;

        targetFillAmount = (float)selectedTower.CurrentLevel / selectedTower.MaxLevel;

        if (animateProgress)
        {
            currentFillAmount = Mathf.Lerp(currentFillAmount, targetFillAmount, Time.deltaTime * animationSpeed);
            levelProgressBar.fillAmount = currentFillAmount;
        }
        else
        {
            levelProgressBar.fillAmount = targetFillAmount;
            currentFillAmount = targetFillAmount;
        }

        if (selectedTower.CurrentLevel >= selectedTower.MaxLevel)
        {
            levelProgressBar.color = maxLevelColor;
            if (progressText != null) progressText.color = maxLevelColor;
        }
        else
        {
            levelProgressBar.color = Color.green;
            if (progressText != null) progressText.color = Color.white;
        }

        if (progressText != null)
            progressText.text = $"{selectedTower.CurrentLevel}/{selectedTower.MaxLevel}";
    }

    private void InitializeTowerSprites()
    {
        towerSprites.Clear();

        foreach (var mapping in towerSpriteMappings)
        {
            if (!string.IsNullOrEmpty(mapping.towerName) && mapping.towerSprite != null)
            {
                towerSprites[mapping.towerName] = mapping.towerSprite;
            }
        }
    }

    private void UpdateTowerHeaderImage(TowerBehaviour tower)
    {
        if (towerHeaderImage == null) return;

        string towerName = TowerNaming.GetTowerDisplayName(tower);
        Sprite newSprite = defaultTowerSprite;

        if (towerSprites.ContainsKey(towerName))
        {
            newSprite = towerSprites[towerName];
        }

        if (towerHeaderImage.sprite != newSprite)
        {
            StartCoroutine(AnimateTowerImageTransition(newSprite));
        }
    }

    private IEnumerator AnimateTowerImageTransition(Sprite newSprite)
    {
        if (towerHeaderImage == null) yield break;

        float duration = 0.2f;
        float elapsed = 0f;
        Color originalColor = towerHeaderImage.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            towerHeaderImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            yield return null;
        }

        towerHeaderImage.sprite = newSprite;

        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
            towerHeaderImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            yield return null;
        }

        towerHeaderImage.color = originalColor;
    }

    void OnDestroy()
    {
        StopAllAnimations();
        RestoreOriginalScales();

        if (rangeVisual != null)
        {
            rangeVisual.HideRange();
        }

        GameEvents.OnMoneyChanged -= OnMoneyChanged;
    }
}