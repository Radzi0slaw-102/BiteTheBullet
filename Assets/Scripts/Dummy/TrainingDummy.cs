using System.Collections;
using UnityEngine;

/// <summary>
/// Kukła treningowa. Sama w sobie jest bardzo "głupia":
///  - przyjmuje obrażenia przez IDamageable (nie obchodzi jej, czym ją trafiono),
///  - przekazuje je do komponentu Health (który ma włączone odnawianie HP po wyzerowaniu),
///  - odgrywa reakcję: na chwilę zmienia sprite na przechylony i błyska (SpriteFlash).
/// </summary>
[RequireComponent(typeof(Health), typeof(SpriteRenderer), typeof(SpriteFlash))]
public class TrainingDummy : MonoBehaviour, IDamageable
{
    [Header("Sprites")]
    [SerializeField] private Sprite idleSprite;
    [Tooltip("Poza po ciosie z lewej strony (kukła odchyla się w prawo).")]
    [SerializeField] private Sprite hitFromLeftSprite;
    [Tooltip("Poza po ciosie z prawej strony (kukła odchyla się w lewo).")]
    [SerializeField] private Sprite hitFromRightSprite;
    [SerializeField, Min(0.01f)] private float hitPoseDuration = 0.25f;

    [Header("Flash")]
    [Tooltip("Kolor błysku po trafieniu: biały = rozjaśnienie, czerwony = zaczerwienienie.")]
    [SerializeField] private Color hitFlashColor = Color.white;
    [Tooltip("Kolor błysku, gdy trafienie wyzerowało HP (zaraz potem HP wraca do maksimum).")]
    [SerializeField] private Color depletedFlashColor = new(1f, 0.25f, 0.25f);

    [Header("Debug")]
    [SerializeField] private float debugDamage = 10f;

    private Health health;
    private SpriteRenderer spriteRenderer;
    private SpriteFlash spriteFlash;
    private Coroutine hitPoseRoutine;
    private bool depletedThisHit;

    private void Awake()
    {
        health = GetComponent<Health>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteFlash = GetComponent<SpriteFlash>();

        if (idleSprite == null)
            idleSprite = spriteRenderer.sprite;
    }

    private void OnEnable() => health.Depleted += OnDepleted;
    private void OnDisable() => health.Depleted -= OnDepleted;

    // ---- IDamageable ----------------------------------------------------------------------

    public void TakeDamage(DamageInfo info)
    {
        depletedThisHit = false;
        health.ApplyDamage(info.Amount);   // może synchronicznie wywołać OnDepleted

        spriteFlash.Flash(depletedThisHit ? depletedFlashColor : hitFlashColor);
        PlayHitPose(HitCameFromLeft(info));
    }

    // ---- Reakcja --------------------------------------------------------------------------

    private void OnDepleted()
    {
        depletedThisHit = true;
        // Tu można dodać np. dźwięk/efekt "rozbicia" – Health sam odnowi HP do maksimum.
    }

    private bool HitCameFromLeft(DamageInfo info)
    {
        // 1) Kierunek ciosu ma wyraźną składową poziomą -> on decyduje (cios w prawo = przyszedł z lewej).
        if (Mathf.Abs(info.Direction.x) > 0.2f)
            return info.Direction.x > 0f;

        // 2) Cios prawie pionowy (np. piłka spadająca z góry) -> patrzymy, po której stronie środka trafił.
        if (!Mathf.Approximately(info.Point.x, transform.position.x))
            return info.Point.x < transform.position.x;

        // 3) Idealnie w środek – losujemy.
        return Random.value < 0.5f;
    }

    private void PlayHitPose(bool fromLeft)
    {
        if (hitPoseRoutine != null)
            StopCoroutine(hitPoseRoutine);
        hitPoseRoutine = StartCoroutine(HitPoseRoutine(fromLeft ? hitFromLeftSprite : hitFromRightSprite));
    }

    private IEnumerator HitPoseRoutine(Sprite hitSprite)
    {
        if (hitSprite != null)
            spriteRenderer.sprite = hitSprite;

        yield return new WaitForSeconds(hitPoseDuration);

        spriteRenderer.sprite = idleSprite;
        hitPoseRoutine = null;
    }

    // ---- Narzędzia do testów (prawy klik na komponencie w Inspectorze, w trybie Play) ------

    [ContextMenu("Debug/Hit From Left")]
    private void DebugHitFromLeft() =>
        TakeDamage(new DamageInfo(debugDamage, transform.position, Vector2.right));

    [ContextMenu("Debug/Hit From Right")]
    private void DebugHitFromRight() =>
        TakeDamage(new DamageInfo(debugDamage, transform.position, Vector2.left));
}
