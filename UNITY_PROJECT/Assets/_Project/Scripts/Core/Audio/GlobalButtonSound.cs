using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GlobalButtonSound : MonoBehaviour
{
    public static GlobalButtonSound Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(SetupButtonsNextFrame());
    }

    private System.Collections.IEnumerator SetupButtonsNextFrame()
    {
        yield return null;

        Button[] allButtons = FindObjectsOfType<Button>(true);
        int buttonsConfigured = 0;

        foreach (Button button in allButtons)
        {
            if (button.GetComponent<ButtonSound>() == null)
            {
                button.gameObject.AddComponent<ButtonSound>();
                buttonsConfigured++;
            }
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}