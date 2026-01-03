using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class TextEffectManager : MonoBehaviour
{
    [Header("Configuración de Bordes")]
    public bool useBold = true;
    public bool useOutline = true;
    [Range(0.1f, 1f)]
    public float outlineThickness = 0.3f;
    public Color outlineColor = Color.black;

    [Header("Efectos de Animación")]
    public bool enableNumberAnimation = true;
    public EffectType upgradeEffectType = EffectType.ScaleAndColor;

    private TextMeshProUGUI textComponent;
    private Color originalColor;
    private Vector3 originalScale;

    public enum EffectType
    {
        ScaleAndColor,
        Bounce,
        Shake
    }

    void Start()
    {
        textComponent = GetComponent<TextMeshProUGUI>();
        if (textComponent == null) return;

        originalColor = textComponent.color;
        originalScale = transform.localScale;

        ApplyTextStyles();
    }

    void ApplyTextStyles()
    {
        if (useBold)
        {
            textComponent.fontStyle = FontStyles.Bold;
        }

        if (useOutline)
        {
            textComponent.outlineWidth = outlineThickness;
            textComponent.outlineColor = outlineColor;
            textComponent.ForceMeshUpdate();
        }
    }

    public void SetBlinkEffect(bool active, Color blinkColor, float speed)
    {
        if (active)
        {
            textComponent.color = blinkColor;
        }
        else
        {
            textComponent.color = originalColor;
        }
    }

    public void SetPulseEffect(bool active, float speed, float intensity)
    {
        if (active)
        {
            float scale = 1f + Mathf.Sin(Time.time * speed) * intensity;
            transform.localScale = originalScale * scale;
        }
        else
        {
            transform.localScale = originalScale;
        }
    }
}