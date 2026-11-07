using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "Coś, co zadaje obrażenia przy kontakcie". Wrzucasz na hitbox miecza, pocisk, kolce, testową piłkę...
///
/// Działa z oboma rodzajami kontaktu w fizyce 2D:
///  - trigger (collider z zaznaczonym Is Trigger) -> OnTriggerEnter2D, typowe dla hitboxów broni,
///  - zwykła kolizja (fizyczne odbicie)            -> OnCollisionEnter2D, np. piłka.
/// Uwaga Unity: żeby zdarzenia fizyki w ogóle przyszły, przynajmniej jeden z dwóch obiektów
/// musi mieć Rigidbody2D.
///
/// Komponent sam szuka na trafionym obiekcie IDamageable – dlatego cel (np. dummy)
/// nie musi nic wiedzieć o konkretnej broni.
/// </summary>
public class DamageSource : MonoBehaviour
{
    [SerializeField, Min(0f)] private float damage = 10f;

    [Tooltip("Minimalny odstęp (s) między kolejnymi trafieniami TEGO SAMEGO celu. Chroni przed wielokrotnym trafieniem jednym ciosem.")]
    [SerializeField, Min(0f)] private float rehitCooldown = 0.25f;

    [Tooltip("Dla zwykłych kolizji: ignoruj kontakty słabsze niż ta prędkość względna (np. piłka tylko leżąca na celu).")]
    [SerializeField, Min(0f)] private float minImpactSpeed = 0f;

    [Tooltip("Zniszcz ten obiekt po pierwszym trafieniu (np. pocisk).")]
    [SerializeField] private bool destroyOnHit = false;

    private readonly Dictionary<IDamageable, float> lastHitTime = new();
    private Rigidbody2D body;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHit(other, other.ClosestPoint(transform.position));
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.relativeVelocity.magnitude < minImpactSpeed)
            return;

        TryHit(collision.collider, collision.GetContact(0).point);
    }

    private void TryHit(Collider2D other, Vector2 hitPoint)
    {
        // Szukamy w rodzicach, bo collider może siedzieć na dziecku (np. osobny obiekt "Hurtbox").
        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target == null)
            return;

        // Nie bij samego siebie (np. hitbox broni będący dzieckiem postaci, która też jest IDamageable).
        if (target is Component targetComponent && transform.IsChildOf(targetComponent.transform))
            return;

        if (lastHitTime.TryGetValue(target, out float last) && Time.time - last < rehitCooldown)
            return;
        lastHitTime[target] = Time.time;

        target.TakeDamage(new DamageInfo(damage, hitPoint, GetHitDirection(other), gameObject));

        if (destroyOnHit)
            Destroy(gameObject);
    }

    private Vector2 GetHitDirection(Collider2D other)
    {
        // Jeśli lecimy (pocisk, piłka) – kierunek ruchu najlepiej opisuje cios.
        if (body != null && body.linearVelocity.sqrMagnitude > 0.01f)
            return body.linearVelocity;

        // W przeciwnym razie (np. nieruchomy hitbox miecza) – od nas w stronę celu.
        return (Vector2)(other.bounds.center - transform.position);
    }
}
