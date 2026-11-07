using UnityEngine;

/// <summary>
/// "Miraże" (cienie) w trakcie dasha: półprzezroczyste kopie aktualnego sprite'a gracza,
/// ustawione za nim – w kierunku przeciwnym do dasha.
///
/// Maksymalnie dwa naraz i stopniowane: każdy kolejny jest dalej od postaci
/// i bardziej przezroczysty od poprzedniego (krycie * alphaFalloff).
/// Miraż nigdy nie wychodzi za punkt, w którym dash się zaczął – na początku dasha
/// "wysuwa się" zza postaci, a po zakończeniu szybko gaśnie.
/// </summary>
[RequireComponent(typeof(PlayerDash))]
public class DashAfterimages : MonoBehaviour
{
    private const int MaxAfterimages = 2;

    [Tooltip("Renderer, którego sprite kopiujemy. Puste = SpriteRenderer na tym obiekcie.")]
    [SerializeField] private SpriteRenderer source;

    [Tooltip("Ile mirażów naraz (0–2).")]
    [SerializeField, Range(0, MaxAfterimages)] private int count = 2;

    [Tooltip("Odstęp (jednostki świata) między postacią a pierwszym mirażem oraz między kolejnymi mirażami.")]
    [SerializeField, Min(0f)] private float spacing = 0.3f;

    [Tooltip("Krycie pierwszego, bliższego mirażu (0 = niewidoczny, 1 = pełny).")]
    [SerializeField, Range(0f, 1f)] private float firstAlpha = 0.35f;

    [Tooltip("Stopniowanie: krycie każdego kolejnego mirażu = krycie poprzedniego * ta wartość.")]
    [SerializeField, Range(0f, 1f)] private float alphaFalloff = 0.45f;

    [Header("Color")]
    [Tooltip("Materiał mirażu. Z shaderem \"BiteTheBullet/Sprite Flash\" (Materials/SpriteFlash) piksele są MIESZANE " +
             "z kolorem mirażu, więc barwi się też czerń. Puste = materiał gracza (wtedy kolor tylko mnoży, ciemne piksele zostają ciemne).")]
    [SerializeField] private Material afterimageMaterial;

    [Tooltip("Kolor każdego mirażu: [0] = bliższy, [1] = dalszy.")]
    [SerializeField] private Color[] colors = { new(0.35f, 0.95f, 1f), new(0.75f, 0.4f, 1f) };

    [Tooltip("Jak mocno zabarwić miraż (0 = oryginalne kolory postaci, 1 = jednolita sylwetka w kolorze). Działa z materiałem Sprite Flash.")]
    [SerializeField, Range(0f, 1f)] private float colorStrength = 0.75f;

    [Tooltip("Jak szybko (s) miraże gasną po zakończeniu dasha. 0 = znikają od razu.")]
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.12f;

    private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
    private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

    private PlayerDash dash;
    private MaterialPropertyBlock propertyBlock;
    private bool materialMixesColor;
    private readonly SpriteRenderer[] afterimages = new SpriteRenderer[MaxAfterimages];
    private Vector2 dashStart;
    private Vector2 dashDirection = Vector2.right;
    private float travelled;
    private float visibility; // 1 = w trakcie dasha, potem spada do 0

    private void Awake() => EnsureInitialized();

    // Pobiera komponenty i tworzy obiekty mirażów (raz – w trakcie gry tylko je włączamy/wyłączamy).
    // Wołane też z OnEnable i LateUpdate: po przeładowaniu skryptów w trakcie Play Mode
    // niezserializowane pola są puste, a Awake nie wykonuje się ponownie.
    private void EnsureInitialized()
    {
        if (dash == null)
            dash = GetComponent<PlayerDash>();
        if (source == null)
            source = GetComponent<SpriteRenderer>();
        propertyBlock ??= new MaterialPropertyBlock();
        Material material = afterimageMaterial != null ? afterimageMaterial : source.sharedMaterial;
        materialMixesColor = material != null && material.HasProperty(FlashAmountId);

        for (int i = 0; i < MaxAfterimages; i++)
        {
            if (afterimages[i] != null)
                continue;

            Transform existing = transform.Find($"DashAfterimage{i + 1}");
            if (existing != null && existing.TryGetComponent(out SpriteRenderer found))
            {
                afterimages[i] = found;
                continue;
            }

            var go = new GameObject($"DashAfterimage{i + 1}");
            go.transform.SetParent(transform, false); // dziecko gracza = odwraca się razem z nim
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = material;
            sr.sortingLayerID = source.sortingLayerID;
            sr.sortingOrder = source.sortingOrder - (i + 1); // za postacią, dalszy jeszcze dalej
            sr.enabled = false;
            afterimages[i] = sr;
        }
    }

    private void OnEnable()
    {
        EnsureInitialized();
        dash.DashStarted += OnDashStarted;
    }

    private void OnDisable()
    {
        dash.DashStarted -= OnDashStarted;
        visibility = 0f;
        foreach (var sr in afterimages)
            if (sr != null)
                sr.enabled = false;
    }

    private void OnDashStarted()
    {
        dashStart = transform.position;
        dashDirection = dash.Direction;
        travelled = 0f;
        visibility = 1f;
    }

    // LateUpdate: po Animatorze, więc kopiujemy sprite, który faktycznie zostanie narysowany w tej klatce.
    private void LateUpdate()
    {
        EnsureInitialized();

        if (dash.IsDashing)
        {
            visibility = 1f;
            travelled = Mathf.Abs(transform.position.x - dashStart.x);
        }
        else if (visibility > 0f)
        {
            visibility = fadeOutDuration > 0f ? Mathf.Max(0f, visibility - Time.deltaTime / fadeOutDuration) : 0f;
        }

        float alpha = firstAlpha;
        for (int i = 0; i < MaxAfterimages; i++)
        {
            SpriteRenderer sr = afterimages[i];
            bool visible = i < count && visibility > 0f;
            sr.enabled = visible;
            if (visible)
            {
                // Za postacią (przeciwnie do dasha), ale nie dalej niż punkt startu dasha.
                float offset = Mathf.Min(spacing * (i + 1), travelled);
                Vector3 position = transform.position - (Vector3)(dashDirection * offset);
                sr.transform.position = position;

                // Klatkę kopiujemy tylko w trakcie dasha – gasnący miraż zostaje w pozie dasha,
                // zamiast przejmować pozę idle, w którą postać już wróciła.
                if (dash.IsDashing)
                {
                    sr.sprite = source.sprite;
                    sr.flipX = source.flipX;
                }
                Color ghostColor = colors.Length > 0 ? colors[Mathf.Min(i, colors.Length - 1)] : Color.white;
                Color color;
                if (materialMixesColor)
                {
                    // Shader miesza piksele z kolorem (_FlashAmount), a vertex color niesie tylko przezroczystość.
                    sr.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetColor(FlashColorId, ghostColor);
                    propertyBlock.SetFloat(FlashAmountId, colorStrength);
                    sr.SetPropertyBlock(propertyBlock);
                    color = source.color;
                }
                else
                {
                    color = source.color * ghostColor; // zwykły materiał: kolor tylko mnoży
                }
                color.a *= alpha * visibility;
                sr.color = color;
            }
            alpha *= alphaFalloff; // stopniowanie: kolejny miraż słabszy
        }
    }
}
