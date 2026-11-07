using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Dash gracza: szybki, poziomy zryw na zadaną odległość w zadanym czasie,
/// opcjonalnie zadający obrażenia wszystkiemu (IDamageable), przez co przeleci.
///
/// Wszystko ustawia się w Inspectorze. Prędkość nie jest ustawiana bezpośrednio –
/// wynika z długości i czasu (prędkość = długość / czas), więc zmieniając jedno, drugie zostaje zachowane.
/// Gdy obrażenia = 0, dash w ogóle nie wykonuje zapytań o trafienia.
///
/// PlayerScript na czas dasha wstrzymuje swój ruch i skok (patrz IsDashing).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerDash : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Ile jednostek świata pokonuje dash (jeśli nic go nie zatrzyma, np. ściana).")]
    [SerializeField, Min(0.01f)] private float dashDistance = 1.5f;

    [Tooltip("Ile sekund trwa dash.")]
    [SerializeField, Min(0.01f)] private float dashDuration = 0.15f;

    [Tooltip("Ile sekund po zakończeniu dasha trzeba odczekać na kolejny.")]
    [SerializeField, Min(0f)] private float dashCooldown = 0.5f;

    [Tooltip("Czy można dashować w powietrzu.")]
    [SerializeField] private bool allowAirDash = true;

    [Tooltip("Wyłącz grawitację na czas dasha – lot idealnie poziomy.")]
    [SerializeField] private bool suspendGravity = true;

    [Tooltip("Kierunek z wciśniętego ruchu (lewo/prawo); bez wciśniętego kierunku – strona, w którą patrzy postać.")]
    [SerializeField] private bool useMoveInputDirection = true;

    [Tooltip("W trakcie dasha postać przenika przez obiekty z warstw Target Layers zamiast się od nich odbijać.")]
    [SerializeField] private bool passThroughTargets = true;

    [Header("Damage")]
    [Tooltip("Obrażenia zadawane każdemu trafionemu celowi (raz na dash). 0 = dash bez obrażeń i bez sprawdzania trafień.")]
    [SerializeField, Min(0f)] private float damage = 0f;

    [Tooltip("Siła uderzenia przekazywana celowi (DamageInfo.Force).")]
    [SerializeField, Min(0f)] private float knockbackForce = 0f;

    [Tooltip("Warstwy, na których szukamy celów (domyślnie Enemy).")]
    [SerializeField] private LayerMask targetLayers;

    [Tooltip("Rozmiar prostokąta trafień (szerokość, wysokość).")]
    [SerializeField] private Vector2 hitboxSize = new(0.25f, 0.3f);

    [Tooltip("Przesunięcie prostokąta trafień względem postaci. X jest odwracane, gdy dash idzie w lewo.")]
    [SerializeField] private Vector2 hitboxOffset = Vector2.zero;

    [Header("Ground check (dla Allow Air Dash)")]
    [SerializeField, Min(0.01f)] private float groundCheckLength = 0.3f;

    [Header("Input")]
    [SerializeField] private string dashActionName = "Dash";
    [SerializeField] private string moveActionName = "Move";

    /// <summary>Czy dash właśnie trwa. PlayerScript sprawdza to, żeby nie nadpisywać prędkości.</summary>
    public bool IsDashing { get; private set; }

    /// <summary>Kierunek aktualnego (lub ostatniego) dasha: (1,0) w prawo, (-1,0) w lewo.</summary>
    public Vector2 Direction { get; private set; } = Vector2.right;

    /// <summary>Zdarzenia na przyszłość, np. do odpalenia animacji, dźwięku czy smugi.</summary>
    public event Action DashStarted;
    public event Action DashEnded;

    private Rigidbody2D rb;
    private InputAction dashAction;
    private InputAction moveAction;

    private bool dashRequested;
    private float dashTimeLeft;
    private float cooldownLeft;

    private float savedGravityScale;
    private LayerMask savedExcludeLayers;

    private ContactFilter2D targetFilter;
    private Vector2 lastHitCheckPosition;
    private readonly HashSet<IDamageable> hitThisDash = new();
    private readonly List<Collider2D> overlapResults = new();
    private readonly List<RaycastHit2D> castResults = new();

    private float DashSpeed => dashDistance / dashDuration;

    // Reset wywołuje edytor przy dodaniu komponentu – ustawiamy sensowną domyślną warstwę celów.
    private void Reset()
    {
        targetLayers = LayerMask.GetMask("Enemy");
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        dashAction = InputSystem.actions.FindAction(dashActionName);
        moveAction = InputSystem.actions.FindAction(moveActionName);
        if (dashAction == null)
            Debug.LogWarning($"PlayerDash: nie znaleziono akcji wejścia \"{dashActionName}\".", this);
    }

    private void Update()
    {
        // Wciśnięcia czytamy co klatkę (Update), bo w FixedUpdate można je przegapić.
        // Sam dash wykonujemy w FixedUpdate, razem z fizyką.
        if (dashAction != null && dashAction.WasPressedThisFrame())
            dashRequested = true;
    }

    private void FixedUpdate()
    {
        if (cooldownLeft > 0f)
            cooldownLeft -= Time.fixedDeltaTime;

        if (dashRequested)
        {
            dashRequested = false;
            TryStartDash();
        }

        if (!IsDashing)
            return;

        // Najpierw sprawdzamy drogę przebytą w poprzednim kroku fizyki, potem ustawiamy kolejny krok.
        if (damage > 0f)
            CheckHits();

        if (dashTimeLeft <= 0f)
        {
            EndDash();
            return;
        }

        // Ostatni krok bywa krótszy niż fixedDeltaTime (np. 0.15 s nie dzieli się równo na kroki po 0.02 s).
        // Skalujemy wtedy prędkość, żeby łączna droga wyniosła dokładnie dashDistance.
        float step = Mathf.Min(Time.fixedDeltaTime, dashTimeLeft);
        rb.linearVelocity = Direction * (DashSpeed * step / Time.fixedDeltaTime);
        dashTimeLeft -= step;
    }

    private void OnDisable()
    {
        // Wyłączenie komponentu w trakcie dasha nie może zostawić gracza bez grawitacji.
        if (IsDashing)
            EndDash();
    }

    /// <summary>Poproś o dash (np. z innego skryptu albo AI). Wykona się w najbliższym kroku fizyki.</summary>
    public void RequestDash() => dashRequested = true;

    private void TryStartDash()
    {
        if (IsDashing || cooldownLeft > 0f)
            return;
        if (!allowAirDash && !IsGrounded())
            return;

        Direction = ChooseDirection();
        IsDashing = true;
        dashTimeLeft = dashDuration;

        if (suspendGravity)
        {
            savedGravityScale = rb.gravityScale;
            rb.gravityScale = 0f;
        }

        if (passThroughTargets)
        {
            // excludeLayers: warstwy, z którymi ten Rigidbody2D chwilowo nie koliduje fizycznie.
            savedExcludeLayers = rb.excludeLayers;
            rb.excludeLayers = rb.excludeLayers | targetLayers;
        }

        if (damage > 0f)
        {
            hitThisDash.Clear();
            targetFilter = new ContactFilter2D();
            targetFilter.SetLayerMask(targetLayers);
            targetFilter.useTriggers = true; // hurtboxy przeciwników mogą być triggerami
            lastHitCheckPosition = HitboxCenter();
        }

        DashStarted?.Invoke();
    }

    private void EndDash()
    {
        IsDashing = false;
        cooldownLeft = dashCooldown;

        if (suspendGravity)
            rb.gravityScale = savedGravityScale;
        if (passThroughTargets)
            rb.excludeLayers = savedExcludeLayers;

        // Wyhamuj – dalej ruchem steruje już PlayerScript.
        rb.linearVelocity = Vector2.zero;

        DashEnded?.Invoke();
    }

    private Vector2 ChooseDirection()
    {
        if (useMoveInputDirection && moveAction != null)
        {
            float x = moveAction.ReadValue<Vector2>().x;
            if (Mathf.Abs(x) > 0.1f)
                return new Vector2(Mathf.Sign(x), 0f);
        }

        // PlayerScript odwraca postać przez ujemną skalę X – z jej znaku wiemy, w którą stronę patrzy.
        return new Vector2(Mathf.Sign(transform.localScale.x), 0f);
    }

    // ---- Trafienia ------------------------------------------------------------------------

    private void CheckHits()
    {
        Vector2 from = lastHitCheckPosition;
        Vector2 to = HitboxCenter();
        Vector2 path = to - from;
        float distance = path.magnitude;

        // 1) Co jest w prostokącie teraz.
        overlapResults.Clear();
        Physics2D.OverlapBox(to, hitboxSize, 0f, targetFilter, overlapResults);
        foreach (Collider2D hit in overlapResults)
            TryDamage(hit);

        // 2) Co minęliśmy po drodze. Przy dużej prędkości postać w jednym kroku fizyki przeskakuje
        //    spory kawałek – bez tego "przesunięcia prostokąta" cienki przeciwnik mógłby zostać pominięty.
        if (distance > 0.0001f)
        {
            castResults.Clear();
            Physics2D.BoxCast(from, hitboxSize, 0f, path / distance, targetFilter, castResults, distance);
            foreach (RaycastHit2D hit in castResults)
                TryDamage(hit.collider);
        }

        lastHitCheckPosition = to;
    }

    private void TryDamage(Collider2D other)
    {
        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target == null)
            return;

        // Nie bij samego siebie.
        if (target is Component targetComponent && transform.IsChildOf(targetComponent.transform))
            return;

        // HashSet.Add zwraca false, jeśli cel już był – każdy cel obrywa najwyżej raz na dash.
        if (!hitThisDash.Add(target))
            return;

        Vector2 hitPoint = other.ClosestPoint(rb.position);
        target.TakeDamage(new DamageInfo(damage, hitPoint, Direction, gameObject, knockbackForce));
    }

    private Vector2 HitboxCenter()
    {
        float side = Mathf.Sign(Direction.x);
        return rb.position + new Vector2(hitboxOffset.x * side, hitboxOffset.y);
    }

    private bool IsGrounded()
    {
        return Physics2D.Raycast(transform.position, Vector2.down, groundCheckLength, LayerMask.GetMask("Ground"));
    }

    private void OnDrawGizmosSelected()
    {
        // Podgląd w edytorze: czerwony prostokąt trafień i niebieska linia zasięgu dasha.
        Vector2 position = Application.isPlaying && rb != null ? rb.position : (Vector2)transform.position;
        float side = Application.isPlaying ? Mathf.Sign(Direction.x) : Mathf.Sign(transform.localScale.x);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(position, position + new Vector2(dashDistance * side, 0f));

        if (damage <= 0f)
            return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(position + new Vector2(hitboxOffset.x * side, hitboxOffset.y), hitboxSize);
    }
}
