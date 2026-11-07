using UnityEngine;

/// <summary>
/// Prosty pasek życia w przestrzeni świata, zbudowany z dwóch SpriteRendererów (tło + wypełnienie),
/// bez Canvasa. Wypełnienie skalujemy w osi X – sprite ma pivot przy lewej krawędzi,
/// więc pasek "kurczy się" w stronę lewej.
///
/// Pasek tylko słucha zdarzenia Health.Changed – nie wie, kto i dlaczego zmienia HP.
/// </summary>
public class WorldHealthBar : MonoBehaviour
{
    [Tooltip("Puste = weź Health z rodzica.")]
    [SerializeField] private Health health;
    [SerializeField] private SpriteRenderer fill;
    [SerializeField] private Color fullColor = new(0.30f, 0.85f, 0.30f);
    [SerializeField] private Color emptyColor = new(0.90f, 0.20f, 0.15f);

    private float fullWidth;

    private void Awake()
    {
        if (health == null)
            health = GetComponentInParent<Health>();

        // Szerokość ustawiona w edytorze = pasek pełny.
        fullWidth = fill.transform.localScale.x;
    }

    private void OnEnable() => health.Changed += Refresh;
    private void OnDisable() => health.Changed -= Refresh;

    private void Start()
    {
        // Start wykonuje się po Awake wszystkich obiektów, więc Health ma już ustawione Current.
        Refresh(health.Current, health.Max);
    }

    private void Refresh(float current, float max)
    {
        float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;

        Vector3 scale = fill.transform.localScale;
        scale.x = fullWidth * ratio;
        fill.transform.localScale = scale;

        fill.color = Color.Lerp(emptyColor, fullColor, ratio);
    }
}
