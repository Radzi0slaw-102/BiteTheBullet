using UnityEngine;

/// <summary>
/// Paczka danych opisująca pojedyncze trafienie.
/// Dzięki niej cel nie musi wiedzieć NIC o broni – dostaje tylko "ile, gdzie, z której strony, kto".
/// </summary>
public readonly struct DamageInfo
{
    /// <summary>Ile punktów życia odebrać.</summary>
    public readonly float Amount;

    /// <summary>Punkt trafienia w przestrzeni świata.</summary>
    public readonly Vector2 Point;

    /// <summary>Znormalizowany kierunek ciosu (od atakującego w stronę celu). Może być Vector2.zero.</summary>
    public readonly Vector2 Direction;

    /// <summary>Obiekt, który zadał obrażenia (broń, pocisk...). Może być null.</summary>
    public readonly GameObject Source;

    public DamageInfo(float amount, Vector2 point, Vector2 direction, GameObject source = null)
    {
        Amount = amount;
        Point = point;
        Direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.zero;
        Source = source;
    }
}
