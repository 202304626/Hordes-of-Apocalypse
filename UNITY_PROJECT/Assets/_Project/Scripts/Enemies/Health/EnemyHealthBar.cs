using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBar : MonoBehaviour
{
    public Image fill;
    private Enemy enemy;

    void Start()
    {
        enemy = GetComponentInParent<Enemy>();
        if (enemy == null)
        {
            Debug.LogError("No se encontró Enemy en el padre!");
            return;
        }

        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 1000;

        transform.localScale = new Vector3(0.02f, 0.02f, 0.02f);
    }

    void Update()
    {
        if (enemy == null) return;

        float percent = enemy.Health / enemy.MaxHealth;
        fill.fillAmount = percent;

        transform.rotation = Camera.main.transform.rotation;
    }
}