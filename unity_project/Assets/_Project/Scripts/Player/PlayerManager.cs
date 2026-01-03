using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PlayerManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI livesDisplayText;
    public Image healthBarFill;
    public GameObject healthBarContainer;

    [Header("Player Data")]
    public int startingLives = 5;

    [Header("Configuración de Barra de Vida")]
    public Color fullHealthColor = Color.white;
    public Color mediumHealthColor = Color.white;
    public Color lowHealthColor = Color.white;
    public Color criticalHealthColor = Color.white;
    public float healthBarAnimationSpeed = 3f;
    public float minHealthBarWidth = 0.05f;

    private int currentLives;
    private int maxLives;
    private float targetHealthFill = 1f;
    private float currentHealthFill = 1f;
    private bool isTakingDamage = false;
    private bool isInitialized = false;

    public void Initialize()
    {
        if (isInitialized) return;

        currentLives = startingLives;
        maxLives = startingLives;

        InitializeHealthBar();
        UpdateLivesUI();
        SetupLayersAndPhysics();

        isInitialized = true;
    }

    void Update()
    {
        if (healthBarFill != null && Mathf.Abs(currentHealthFill - targetHealthFill) > 0.001f)
        {
            currentHealthFill = Mathf.Lerp(currentHealthFill, targetHealthFill, Time.deltaTime * healthBarAnimationSpeed);

            float displayFill = currentHealthFill;
            if (currentLives > 0 && displayFill < minHealthBarWidth)
            {
                displayFill = minHealthBarWidth;
            }

            healthBarFill.fillAmount = displayFill;
            UpdateHealthBarColor();
        }
    }

    private void SetupLayersAndPhysics()
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        int towersLayer = LayerMask.NameToLayer("Towers");
        int towerPreviewLayer = LayerMask.NameToLayer("TowerPreview");
        int enemyLayer = LayerMask.NameToLayer("Enemy");

        if (playerLayer == -1)
        {
            Debug.LogError("❌ No se encontró la capa 'Player'");
            return;
        }

        try
        {
            if (towersLayer != -1)
            {
                Physics.IgnoreLayerCollision(playerLayer, towersLayer, true);
            }

            if (towerPreviewLayer != -1)
            {
                Physics.IgnoreLayerCollision(playerLayer, towerPreviewLayer, true);
            }

            if (enemyLayer != -1)
            {
                Physics.IgnoreLayerCollision(playerLayer, enemyLayer, true);
            }

            if (towersLayer != -1 && towerPreviewLayer != -1)
            {
                Physics.IgnoreLayerCollision(towersLayer, towerPreviewLayer, false);
            }

            if (towerPreviewLayer != -1 && enemyLayer != -1)
            {
                Physics.IgnoreLayerCollision(towerPreviewLayer, enemyLayer, true);
            }

            if (enemyLayer != -1)
            {
                Physics.IgnoreLayerCollision(enemyLayer, enemyLayer, true);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"💥 Error configurando colisiones: {e.Message}");
        }
    }

    public void TakeDamage(int damage)
    {
        if (isTakingDamage) return;

        isTakingDamage = true;

        currentLives = Mathf.Max(0, currentLives - damage);

        float rawFill = (float)currentLives / maxLives;
        targetHealthFill = rawFill;

        if (currentLives == 1)
        {
            targetHealthFill = Mathf.Max(minHealthBarWidth, rawFill);
        }

        UpdateHealthBarColor();

        try
        {
            GameEvents.TriggerLivesChanged(currentLives);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error disparando eventos de vida: {e.Message}");
        }

        UpdateLivesUI();

        StartCoroutine(PlayDamageEffect());

        if (currentLives <= 0)
        {
            GameStateManager.Instance.OnPlayerDefeated();
        }

        isTakingDamage = false;
    }

    private void InitializeHealthBar()
    {
        if (healthBarFill != null)
        {
            healthBarFill.type = Image.Type.Filled;
            healthBarFill.fillMethod = Image.FillMethod.Horizontal;
            healthBarFill.fillOrigin = 0;
            healthBarFill.fillAmount = 1f;
            currentHealthFill = 1f;
            targetHealthFill = 1f;
            UpdateHealthBarColor();
        }

        if (healthBarContainer != null)
        {
            healthBarContainer.SetActive(true);
        }
    }

    private void UpdateHealthBarColor()
    {
        if (healthBarFill == null) return;

        float healthPercentage = (float)currentLives / maxLives;

        if (healthPercentage > 0.6f)
            healthBarFill.color = fullHealthColor;
        else if (healthPercentage > 0.3f)
            healthBarFill.color = mediumHealthColor;
        else if (healthPercentage > 0.1f)
            healthBarFill.color = lowHealthColor;
        else
            healthBarFill.color = criticalHealthColor;
    }

    private IEnumerator PlayDamageEffect()
    {
        if (healthBarFill == null) yield break;

        Image[] healthBarImages = healthBarContainer.GetComponentsInChildren<Image>();
        Color[] originalColors = new Color[healthBarImages.Length];

        for (int i = 0; i < healthBarImages.Length; i++)
        {
            originalColors[i] = healthBarImages[i].color;
            healthBarImages[i].color = criticalHealthColor;
        }

        yield return new WaitForSeconds(0.15f);

        for (int i = 0; i < healthBarImages.Length; i++)
        {
            healthBarImages[i].color = originalColors[i];
        }

        UpdateHealthBarColor();
    }

    private void UpdateLivesUI()
    {
        if (livesDisplayText != null)
            livesDisplayText.text = $"{currentLives}/{maxLives}";
    }

    public int GetCurrentLives() => currentLives;
    public int GetMaxLives() => maxLives;
    public void ResetPlayer()
    {
        currentLives = startingLives;
        maxLives = startingLives;

        targetHealthFill = 1f;
        currentHealthFill = 1f;

        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = 1f;
            UpdateHealthBarColor();
        }

        isTakingDamage = false;
        UpdateLivesUI();
    }

    private void OnDestroy()
    {
        GameEvents.OnEnemyReachedEnd -= (enemy) => TakeDamage(1);
    }
}