using UnityEngine;

public class TowerRangeVisual : MonoBehaviour
{
    [Header("Configuración de Rango Visual")]
    public Color placingValidColor = new Color(0, 1, 0, 0.4f);
    public Color placingInvalidColor = new Color(1, 0, 0, 0.4f);
    public Color selectedTowerColor = new Color(0, 0.5f, 1, 0.5f);

    [Header("Configuración Técnica")]
    public int circleSegments = 64;
    public float yOffset = 0.2f;

    private GameObject rangeCircle;
    private LineRenderer lineRenderer;
    private bool isShowingSelected = false;
    private bool isInitialized = false;
    private float currentRange = 0f;

    void Start()
    {
        InitializeRangeVisual();
    }

    void InitializeRangeVisual()
    {
        if (isInitialized) return;
        CreateRangeCircle();
        isInitialized = true;
    }

    void CreateRangeCircle()
    {
        if (rangeCircle != null)
        {
            Destroy(rangeCircle);
        }

        rangeCircle = new GameObject("RangeCircle");
        rangeCircle.transform.SetParent(transform);
        rangeCircle.transform.localPosition = Vector3.zero;

        lineRenderer = rangeCircle.AddComponent<LineRenderer>();
        lineRenderer.positionCount = circleSegments + 1;
        lineRenderer.loop = true;
        lineRenderer.useWorldSpace = false;

        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = placingValidColor;
        lineRenderer.endColor = placingValidColor;
        lineRenderer.startWidth = 0.25f;
        lineRenderer.endWidth = 0.25f;

        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;

        rangeCircle.SetActive(false);
    }

    public void ShowPlacingRange(string towerType, Vector3 position, float range, bool isValid, bool hasLimitSpace)
    {
        if (!isInitialized) InitializeRangeVisual();
        if (rangeCircle == null) CreateRangeCircle();

        isShowingSelected = false;
        currentRange = range;

        rangeCircle.transform.position = position + Vector3.up * yOffset;
        CreateCircle(range);

        Color color = isValid && hasLimitSpace ? placingValidColor : placingInvalidColor;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;

        rangeCircle.SetActive(true);
    }

    public void ShowSelectedRange(TowerBehaviour tower)
    {
        if (tower == null)
        {
            Debug.LogError("❌ Torre es null en ShowSelectedRange");
            return;
        }

        if (!isInitialized) InitializeRangeVisual();
        if (rangeCircle == null) CreateRangeCircle();

        isShowingSelected = true;
        currentRange = tower.Range;

        rangeCircle.transform.position = tower.transform.position + Vector3.up * yOffset;
        CreateCircle(tower.Range);

        lineRenderer.startColor = selectedTowerColor;
        lineRenderer.endColor = selectedTowerColor;

        rangeCircle.SetActive(true);
    }

    public void UpdatePlacingRange(Vector3 position, float range, bool isValid, bool hasLimitSpace)
    {
        if (!isInitialized || rangeCircle == null || !rangeCircle.activeInHierarchy || isShowingSelected)
            return;

        if (Mathf.Abs(currentRange - range) > 0.1f)
        {
            currentRange = range;
            CreateCircle(range);
        }

        rangeCircle.transform.position = position + Vector3.up * yOffset;

        Color color = isValid && hasLimitSpace ? placingValidColor : placingInvalidColor;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
    }

    private void CreateCircle(float radius)
    {
        if (lineRenderer == null) return;

        if (radius <= 0.1f)
        {
            radius = 10f;
            Debug.LogWarning("⚠️ Radio de rango muy pequeño, usando fallback");
        }

        float angle = 0f;
        for (int i = 0; i < circleSegments + 1; i++)
        {
            float x = Mathf.Sin(Mathf.Deg2Rad * angle) * radius;
            float z = Mathf.Cos(Mathf.Deg2Rad * angle) * radius;
            lineRenderer.SetPosition(i, new Vector3(x, 0, z));
            angle += 360f / circleSegments;
        }
    }

    public void HideRange()
    {
        if (rangeCircle != null)
        {
            rangeCircle.SetActive(false);
            isShowingSelected = false;
            currentRange = 0f;
        }
    }

    public bool IsShowingSelectedRange()
    {
        return isShowingSelected && rangeCircle != null && rangeCircle.activeInHierarchy;
    }

    void OnDestroy()
    {
        if (rangeCircle != null)
            Destroy(rangeCircle);
    }
}