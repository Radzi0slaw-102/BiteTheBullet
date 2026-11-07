using UnityEngine;

/// <summary>
/// Paczka danych opisująca pojedyncze trafienie.
/// Dzięki niej cel nie musi wiedzieć NIC o broni – dostaje tylko "ile, jak mocno, gdzie, z której strony, kto".
/// </summary>
public readonly struct DamageInfo
{
    /// <summary>Ile punktów życia odebrać.</summary>
    public readonly float Amount;

    /// <summary>
    /// Siła uderzenia (odrzut). Cel sam decyduje, co z nią zrobić – dummy np. przy średniej sile się przesuwa,
    /// a przy dużej dodatkowo przewraca. 0 = cios bez odrzutu.
    /// </summary>
    public readonly float Force;

    /// <summary>Punkt trafienia w przestrzeni świata.</summary>
    public readonly Vector2 Point;

    /// <summary>Znormalizowany kierunek ciosu (od atakującego w stronę celu). Może być Vector2.zero.</summary>
    public readonly Vector2 Direction;

    /// <summary>Obiekt, który zadał obrażenia (broń, pocisk...). Może być null.</summary>
    public readonly GameObject Source;

    public DamageInfo(float amount, Vector2 point, Vector2 direction, GameObject source = null, float force = 0f)
    {
        Amount = amount;
        Force = Mathf.Max(0f, force);
        Point = point;
        Direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.zero;
        Source = source;
    }
}
