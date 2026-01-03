using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneInitializer : MonoBehaviour
{
    [Header("Scene Configuration")]
    public string sceneName;
    public bool isGameScene = true;

    private void Start()
    {
        sceneName = SceneManager.GetActiveScene().name;

        if (isGameScene)
        {
            InitializeGameScene();
        }
    }
    private void InitializeGameScene()
    {
        StartCoroutine(InitializeAfterFrame());
    }

    private System.Collections.IEnumerator InitializeAfterFrame()
    {
        yield return null;

        GameStateManager gameManager = FindObjectOfType<GameStateManager>();
        if (gameManager == null)
        {
            Debug.LogError("❌ No se encontró GameStateManager en la escena!");
            yield break;
        }

        if (!gameManager.AreManagersInitialized)
        {
            gameManager.ResetGame();
        }

        if (!EntitySummoner.IsInitialized)
        {
            EntitySummoner.Init();
        }

        if (gameManager.playerManager == null)
        {
            Debug.LogError("❌ PlayerManager no está asignado en GameStateManager!");
        }
    }
}