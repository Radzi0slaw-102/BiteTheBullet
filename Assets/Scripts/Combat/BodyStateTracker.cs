using System;
using UnityEngine;

/// <summary>
/// Śledzi stan ciała: w powietrzu / stojący / powalony.
///
/// "W powietrzu" vs "stojący" wykrywa tak samo jak gracz – raycastem w dół na warstwę "Ground"
/// (GroundedCheck to prawie kopia PlayerScript.GroundedCheck). "Powalony" jest ustawiany z zewnątrz
/// przez KnockDown(czas) i dopóki trwa, raycast nie nadpisuje stanu.
/// </summary>
public class BodyStateTracker : MonoBehaviour, IHasBodyState
{
    [Tooltip("Długość promienia sprawdzającego podłoże (odpowiednik rayLength gracza).")]
    [SerializeField, Min(0.01f)] private float rayLength = 0.1f;

    [Tooltip("Promień startuje tyle nad pozycją obiektu. Pivot dummy'ego jest przy stopach (u gracza na środku), " +
             "więc bez przesunięcia promień zaczynałby się na krawędzi podłogi.")]
    [SerializeField, Min(0f)] private float rayOriginOffset = 0.05f;

    public BodyState CurrentBodyState { get; private set; } = BodyState.Standing;
    public bool IsKnockedDown => CurrentBodyState == BodyState.KnockedDown;

    /// <summary>Wywoływane przy każdej zmianie stanu. Parametry: (poprzedni stan, nowy stan).</summary>
    public event Action<BodyState, BodyState> StateChanged;

    private float knockDownTimeLeft;

    private void FixedUpdate()
    {
        if (IsKnockedDown)
        {
            knockDownTimeLeft -= Time.fixedDeltaTime;
            if (knockDownTimeLeft <= 0f)
                GetUp();
            return;
        }

        GroundedCheck(rayLength);
    }

    /// <summary>Powala na co najmniej <paramref name="duration"/> sekund. Ponowne powalenie leżącego wydłuża czas.</summary>
    public void KnockDown(float duration)
    {
        knockDownTimeLeft = Mathf.Max(knockDownTimeLeft, duration);
        SetState(BodyState.KnockedDown);
    }

    /// <summary>Kończy powalenie od razu. Nowy stan (stojący / w powietrzu) ustala raycast.</summary>
    public void GetUp()
    {
        if (!IsKnockedDown)
            return;

        knockDownTimeLeft = 0f;
        GroundedCheck(rayLength);
    }

    // Prawie kopia PlayerScript.GroundedCheck: ten sam raycast i te same linie debugowe,
    // ale zamiast canJump ustawia stan, a promień startuje odrobinę wyżej (rayOriginOffset).
    private void GroundedCheck(float length)
    {
        Vector2 origin = (Vector2)transform.position + Vector2.up * rayOriginOffset;
        Vector2 direction = Vector2.down;
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, length, LayerMask.GetMask("Ground"));
        if (hit)
        {
            Debug.DrawRay(origin, Vector2.down * length, Color.green);
            SetState(BodyState.Standing);
        }
        else
        {
            Debug.DrawRay(origin, Vector2.down * length, Color.red);
            SetState(BodyState.Airborne);
        }
    }

    private void SetState(BodyState newState)
    {
        if (newState == CurrentBodyState)
            return;

        BodyState oldState = CurrentBodyState;
        CurrentBodyState = newState;
        StateChanged?.Invoke(oldState, newState);
    }
}
