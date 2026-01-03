using UnityEngine;
using UnityEngine.UI;

public class ButtonSound : MonoBehaviour
{
    private Button button;

    void Start()
    {
        button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(OnButtonClick);
        }
        else
        {
            Debug.LogError($"❌ ButtonSound en objeto sin Button: {gameObject.name}");
        }
    }

    private void OnButtonClick()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySound(SoundManager.SoundType.ButtonClick);
        }
    }

    void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnButtonClick);
        }
    }
}