using System;
using UnityEngine;

/// <summary>
/// Uniwersalny komponent punktów życia. Nie wie nic o grafice ani o tym, kto go używa –
/// tylko liczy HP i ogłasza zmiany przez zdarzenia (events).
/// Inne komponenty (pasek życia, dummy, w przyszłości gracz/przeciwnik) subskrybują te zdarzenia.
/// </summary>
public class Health : MonoBehaviour
{
    [SerializeField, Min(1f)] private float maxHealth = 50f;

    [Tooltip("Gdy HP spadnie do zera, natychmiast wraca do maksimum (\"nieskończone\" życie dummy'ego).")]
    [SerializeField] private bool refillWhenDepleted = false;

    public float Max => maxHealth;
    public float Current { get; private set; }
    public float Normalized => Current / maxHealth;

    /// <summary>Wywoływane po każdej zmianie HP. Parametry: (aktualne HP, maksymalne HP).</summary>
    public event Action<float, float> Changed;

    /// <summary>Wywoływane w momencie, gdy HP osiągnie zero (przed ewentualnym odnowieniem).</summary>
    public event Action Depleted;

    private void Awake()
    {
        Current = maxHealth;
    }

    public void ApplyDamage(float amount)
    {
        if (amount <= 0f || Current <= 0f)
            return;

        Current = Mathf.Max(0f, Current - amount);
        Changed?.Invoke(Current, maxHealth);

        if (Current > 0f)
            return;

        Depleted?.Invoke();

        if (refillWhenDepleted)
            ResetToMax();
    }

    public void ResetToMax()
    {
        Current = maxHealth;
        Changed?.Invoke(Current, maxHealth);
    }
}
