using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("Scenes")]
    public string[] gameScenes;

    [Header("Transition Settings")]
    public Image fadeOverlay;
    public float fadeDuration = 1f;
    public Color fadeColor = Color.black;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;

            if (fadeOverlay == null)
                CreateFadeOverlay();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void CreateFadeOverlay()
    {
        GameObject fadeObject = new GameObject("FadeOverlay");
        fadeObject.transform.SetParent(transform);

        Canvas canvas = fadeObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        fadeOverlay = fadeObject.AddComponent<Image>();
        fadeOverlay.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0);
        fadeOverlay.raycastTarget = false;

        RectTransform rect = fadeOverlay.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    public void GoMainMenu() => StartCoroutine(TransitionCoroutine(0));
    public void GoMapSelection() => StartCoroutine(TransitionCoroutine(1));
    public void GoFirstMap() { CleanupBeforeSceneChange(); StartCoroutine(TransitionCoroutine(2)); }
    public void GoSecondMap() { CleanupBeforeSceneChange(); StartCoroutine(TransitionCoroutine(3)); }
    public void GoThirdMap() { CleanupBeforeSceneChange(); StartCoroutine(TransitionCoroutine(4)); }
    public void QuitApplication() => Application.Quit();

    public void RestartCurrentScene()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        if (IsGameScene(currentScene)) CleanupBeforeSceneChange();
        StartCoroutine(TransitionCoroutine(SceneManager.GetActiveScene().buildIndex));
    }

    private IEnumerator TransitionCoroutine(int sceneIndex)
    {
        yield return StartCoroutine(FadeOut());

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneIndex);
        asyncLoad.allowSceneActivation = false;

        while (asyncLoad.progress < 0.9f)
        {
            yield return null;
        }

        asyncLoad.allowSceneActivation = true;
    }

    private IEnumerator FadeOut()
    {
        float timer = 0f;
        fadeOverlay.raycastTarget = true;

        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01(timer / fadeDuration);
            fadeOverlay.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, alpha);
            yield return null;
        }
    }

    private IEnumerator FadeIn()
    {
        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float alpha = 1f - Mathf.Clamp01(timer / fadeDuration);
            fadeOverlay.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, alpha);
            yield return null;
        }

        fadeOverlay.raycastTarget = false;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ReconnectAllButtons();
        StartCoroutine(FadeIn());

        if (IsGameScene(scene.name))
        {
            StartCoroutine(InitializeGameAfterFrame());
        }
    }

    private void ReconnectAllButtons()
    {
        Button[] allButtons = FindObjectsOfType<Button>(true);

        foreach (Button button in allButtons)
        {
            button.onClick.RemoveAllListeners();
            string buttonName = button.gameObject.name.ToLower();

            if (buttonName.Contains("menu"))
            {
                button.onClick.AddListener(GoMainMenu);
            }
            else if (buttonName.Contains("map") && buttonName.Contains("select"))
            {
                button.onClick.AddListener(GoMapSelection);
            }
            else if (buttonName.Contains("1"))
            {
                button.onClick.AddListener(GoFirstMap);
            }
            else if ( buttonName.Contains("2"))
            {
                button.onClick.AddListener(GoSecondMap);
            }
            else if (buttonName.Contains("3"))
            {
                button.onClick.AddListener(GoThirdMap);
            }
            else if (buttonName.Contains("restart"))
            {
                button.onClick.AddListener(RestartCurrentScene);
            }
            else if (buttonName.Contains("quit"))
            {
                button.onClick.AddListener(QuitApplication);
            }
        }
    }

    private IEnumerator InitializeGameAfterFrame()
    {
        yield return null;
        EntitySummoner.ClearPools();
        GameStateManager gameManager = FindObjectOfType<GameStateManager>();
        if (gameManager != null) gameManager.ResetGame();
        else Debug.LogError("❌ No se encontró GameStateManager en la escena");
    }

    private void CleanupBeforeSceneChange()
    {
        StopAllCoroutines();
        EntitySummoner.ClearPools();
        Time.timeScale = 1f;
    }

    private bool IsGameScene(string sceneName)
    {
        foreach (string gameScene in gameScenes)
            if (sceneName == gameScene) return true;
        return false;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}