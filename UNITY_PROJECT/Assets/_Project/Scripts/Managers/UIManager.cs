using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("Main UI")]
    public GameObject gameUI;

    [Header("Game Over UI")]
    public GameObject gameOverPanel;
    public GameObject victoryPanel;
    public TextMeshProUGUI finalStatsText;

    [Header("Game Over Buttons")]
    public Button gameOverRestartButton;
    public Button gameOverMainMenuButton;

    [Header("Victory Buttons")]
    public Button victoryRestartButton;
    public Button victoryMainMenuButton;

    [Header("UI Elements")]
    public TextMeshProUGUI moneyText;
    public TextMeshProUGUI healthText;
    public Image healthBar;
    public TextMeshProUGUI waveText;
    public TextMeshProUGUI preparationText;
    public TextMeshProUGUI enemiesRemainingText;

    [Header("Action Buttons")]
    public Button startWaveButton;

    [Header("References")]
    public WaveManager waveManager;
    public PlayerManager playerManager;
    public EnemyManager enemyManager;

    [Header("Configuración Barra de Vida")]
    public GameObject healthBarContainer;
    public bool showHealthText = true;

    private TowerButtonAutoConfig[] towerButtons;
    private Coroutine currentMoneyFlashCoroutine;
    private bool gameEnded = false;

    private void Start()
    {
        SetupButtons();
        RegisterEvents();
        InitializeHealthUI();
        InitializeGameOverUI();
        InitializeUI();

        if (EconomyManager.Instance != null)
        {
            UpdateMoneyUI(EconomyManager.Instance.CurrentMoney, EconomyManager.Instance.CurrentMoney);
        }

        if (GameStateManager.Instance != null)
        {
            OnGameStateChanged(GameStateManager.Instance.GetCurrentState());
        }
    }

    private void InitializeUI()
    {
        if (preparationText != null)
        {
            preparationText.text = "Presiona 'Play' para comenzar";
            preparationText.color = Color.green;
        }

        if (enemiesRemainingText != null)
            enemiesRemainingText.text = "";

        if (startWaveButton != null)
        {
            startWaveButton.gameObject.SetActive(true);
            startWaveButton.interactable = true;
        }

        if (waveText != null && waveManager != null)
        {
            waveText.text = $"Oleada: 1/{waveManager.GetTotalWaves()}";
        }
    }

    private void InitializeHealthUI()
    {
        if (healthBar != null)
        {
            healthBar.type = Image.Type.Filled;
            healthBar.fillMethod = Image.FillMethod.Horizontal;
            healthBar.fillOrigin = 0;
        }

        if (healthText != null)
        {
            healthText.gameObject.SetActive(showHealthText);
        }

        if (healthBarContainer != null)
        {
            healthBarContainer.SetActive(true);
        }
    }

    private void Update()
    {
        UpdateUIState();
    }

    private void UpdateUIState()
    {
        UpdateStartButton();
        UpdateEnemiesRemainingUI();
        UpdatePreparationUI();
    }

    private void UpdateStartButton()
    {
        if (startWaveButton == null || waveManager == null) return;

        bool isWaveActive = waveManager.IsWaveActive();
        bool isInPreparation = waveManager.IsInPreparation();
        int currentWave = waveManager.GetCurrentWaveIndex();

        bool shouldShowButton = !isWaveActive && !isInPreparation && currentWave < waveManager.GetTotalWaves() - 1;

        startWaveButton.gameObject.SetActive(shouldShowButton);

        if (shouldShowButton)
        {
            TextMeshProUGUI buttonText = startWaveButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                if (currentWave == -1)
                    buttonText.text = "";
                else
                    buttonText.text = $"Iniciar Wave {currentWave + 2}";
            }
        }
    }

    private void SetupButtons()
    {
        InitializeTowerButtons();

        if (startWaveButton != null)
        {
            startWaveButton.onClick.RemoveAllListeners();
            startWaveButton.onClick.AddListener(OnStartWaveButtonClicked);
        }
    }

    private void OnStartWaveButtonClicked()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.StartWave();
        }
        else
        {
            Debug.LogError("❌ GameStateManager es null!");
        }
    }

    private void RegisterEvents()
    {
        GameEvents.OnMoneyChanged += UpdateMoneyUI;
        GameEvents.OnMoneyTransaction += OnMoneyTransaction;
        GameEvents.OnTransactionFailed += OnTransactionFailed;
        GameEvents.OnMoneyChanged += OnTowerMoneyChanged;

        if (playerManager != null)
        {
            GameEvents.OnLivesChanged += UpdateHealthUI;
        }

        if (waveManager != null)
        {
            waveManager.OnWaveStarted += OnWaveStarted;
            waveManager.OnWaveCompleted += OnWaveCompleted;
            waveManager.OnPreparationTimeUpdated += OnPreparationTimeUpdated;
        }

        GameEvents.OnWaveChanged += UpdateWaveText;
    }

    private void UpdateHealthUI(int currentLives)
    {
        if (playerManager == null) return;

        int maxLives = playerManager.GetMaxLives();
        float healthPercentage = (float)currentLives / maxLives;

        if (healthBar != null)
        {
            healthBar.fillAmount = healthPercentage;
            healthBar.color = Color.white;
        }

        if (healthText != null && showHealthText)
        {
            healthText.text = $"{currentLives}/{maxLives}";
            healthText.color = Color.white;
        }
    }

    private void UpdateMoneyUI(int currentMoney, int previousMoney)
    {
        if (moneyText != null)
        {
            moneyText.text = $"{currentMoney}$";

            if (currentMoneyFlashCoroutine != null)
            {
                StopCoroutine(currentMoneyFlashCoroutine);
                moneyText.color = Color.yellow;
            }

            int difference = currentMoney - previousMoney;
            if (Mathf.Abs(difference) > 5)
            {
                if (difference > 0)
                    currentMoneyFlashCoroutine = StartCoroutine(FlashTextColor(moneyText, Color.green, 0.5f));
                else if (difference < 0)
                    currentMoneyFlashCoroutine = StartCoroutine(FlashTextColor(moneyText, Color.red, 0.5f));
            }
        }
    }

    private void UpdateEnemiesRemainingUI()
    {
        if (enemiesRemainingText != null && waveManager != null)
        {
            if (waveManager.IsWaveActive())
            {
                int enemiesInWave = waveManager.GetCurrentWaveEnemyCount();
                enemiesRemainingText.text = $"Enemigos: {enemiesInWave}";
                enemiesRemainingText.color = enemiesInWave <= 3 ? Color.yellow : Color.white;
            }
            else
            {
                enemiesRemainingText.text = "";
            }
        }
    }

    private void UpdatePreparationUI()
    {
        if (preparationText != null && waveManager != null)
        {
            if (waveManager.IsInPreparation())
            {
                float timeLeft = waveManager.GetPreparationTimeLeft();
                preparationText.text = $"Siguiente oleada en: {Mathf.CeilToInt(timeLeft)}s";
                preparationText.color = timeLeft <= 3f ? Color.yellow : Color.white;
            }
            else if (!waveManager.IsWaveActive() && waveManager.GetCurrentWaveIndex() < waveManager.GetTotalWaves() - 1)
            {
                preparationText.text = "Presiona 'Play' para comenzar";
                preparationText.color = Color.green;
            }
            else if (waveManager.IsWaveActive())
            {
                preparationText.text = "¡Oleada en progreso!";
                preparationText.color = Color.red;
            }
            else
            {
                preparationText.text = "";
            }
        }
    }

    private void UpdateWaveText(int waveIndex)
    {
        if (waveText != null && waveManager != null)
        {
            waveText.text = $"Oleada: {waveIndex + 1}/{waveManager.GetTotalWaves()}";
        }
    }

    private void OnWaveStarted(int waveIndex)
    {
        if (startWaveButton != null) startWaveButton.gameObject.SetActive(false);
        if (preparationText != null) preparationText.text = "¡Oleada en progreso!";
    }

    private void OnWaveCompleted(int waveIndex)
    {
    }

    private void OnPreparationTimeUpdated(float timeLeft)
    {
    }

    public void ShowDamageEffect()
    {
        if (healthBarContainer != null)
        {
            StartCoroutine(PlayDamageEffect());
        }
    }

    private IEnumerator PlayDamageEffect()
    {
        RectTransform rect = healthBarContainer.GetComponent<RectTransform>();
        Vector3 originalPos = rect.localPosition;

        rect.localPosition = originalPos + new Vector3(10f, 0, 0);
        yield return new WaitForSeconds(0.05f);
        rect.localPosition = originalPos - new Vector3(10f, 0, 0);
        yield return new WaitForSeconds(0.05f);
        rect.localPosition = originalPos;

        Image background = healthBarContainer.GetComponent<Image>();
        if (background != null)
        {
            Color originalColor = background.color;
            background.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            background.color = originalColor;
        }
    }

    private void OnDestroy()
    {
        GameEvents.OnMoneyChanged -= UpdateMoneyUI;
        GameEvents.OnMoneyTransaction -= OnMoneyTransaction;
        GameEvents.OnTransactionFailed -= OnTransactionFailed;
        GameEvents.OnMoneyChanged -= OnTowerMoneyChanged;

        if (playerManager != null)
        {
            GameEvents.OnLivesChanged -= UpdateHealthUI;
        }

        if (waveManager != null)
        {
            waveManager.OnWaveStarted -= OnWaveStarted;
            waveManager.OnWaveCompleted -= OnWaveCompleted;
            waveManager.OnPreparationTimeUpdated -= OnPreparationTimeUpdated;
        }

        GameEvents.OnWaveChanged -= UpdateWaveText;
    }

    private void InitializeGameOverUI()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);

        if (gameOverRestartButton != null)
            gameOverRestartButton.onClick.AddListener(RestartGame);

        if (gameOverMainMenuButton != null)
            gameOverMainMenuButton.onClick.AddListener(GoToMainMenu);

        if (victoryRestartButton != null)
            victoryRestartButton.onClick.AddListener(RestartGame);

        if (victoryMainMenuButton != null)
            victoryMainMenuButton.onClick.AddListener(GoToMainMenu);
    }

    public void OnGameStateChanged(GameStateManager.GameState newState)
    {
        switch (newState)
        {
            case GameStateManager.GameState.Defeat:
                ShowGameOverPanel();
                break;
            case GameStateManager.GameState.Victory:
                ShowVictoryPanel();
                break;
            default:
                if (gameOverPanel != null) gameOverPanel.SetActive(false);
                if (victoryPanel != null) victoryPanel.SetActive(false);
                break;
        }
    }

    private void ShowGameOverPanel()
    {
        if (gameEnded) return;
        gameEnded = true;

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            if (gameUI != null) gameUI.SetActive(false);
            UpdateFinalStats();
        }

        PauseGame();
    }

    private void ShowVictoryPanel()
    {
        if (gameEnded) return;
        gameEnded = true;

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
            if (gameUI != null) gameUI.SetActive(false);
            UpdateFinalStats();
        }

        PauseGame();
    }

    private void UpdateFinalStats()
    {
        if (finalStatsText != null && GameStateManager.Instance != null)
        {
            EconomyManager Economy = GameStateManager.Instance.economyManager;
            WaveManager wave = GameStateManager.Instance.waveManager;

            string stats = $"Oleadas completadas: {wave.GetCurrentWaveIndex() + 1}\n";
            stats += $"Dinero final: ${Economy.CurrentMoney}\n";
            stats += $"Vidas restantes: {Economy.CurrentMoney}";

            finalStatsText.text = stats;
        }
    }

    private void PauseGame()
    {
        Time.timeScale = 0f;
    }

    private void RestartGame()
    {
        Time.timeScale = 1f;
        gameEnded = false;

        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.RestartCurrentScene();
        }
        else if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ResetGame();
            if (gameUI != null) gameUI.SetActive(true);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (victoryPanel != null) victoryPanel.SetActive(false);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
    }

    private void GoToMainMenu()
    {
        Time.timeScale = 1f;
        gameEnded = false;

        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.GoMainMenu();
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }
    }

    private void OnMoneyTransaction(int amount, MoneyTransaction transaction)
    {
    }

    private void OnTransactionFailed(string reason, int amount)
    {
    }


    private IEnumerator FlashTextColor(TextMeshProUGUI text, Color flashColor, float duration)
    {
        Color originalColor = Color.yellow;
        text.color = flashColor;
        yield return new WaitForSeconds(duration);
        text.color = originalColor;
        currentMoneyFlashCoroutine = null;
    }

    private void InitializeTowerButtons()
    {
        towerButtons = FindObjectsOfType<TowerButtonAutoConfig>();
        UpdateAllTowerButtons();
    }

    private void UpdateAllTowerButtons()
    {
        if (EconomyManager.Instance == null) return;

        int currentMoney = EconomyManager.Instance.CurrentMoney;

        foreach (TowerButtonAutoConfig button in towerButtons)
        {
            if (button != null)
            {
                button.UpdateButtonState(currentMoney);
            }
        }
    }

    private void OnTowerMoneyChanged(int currentMoney, int previousMoney)
    {
        UpdateAllTowerButtons();
    }
}