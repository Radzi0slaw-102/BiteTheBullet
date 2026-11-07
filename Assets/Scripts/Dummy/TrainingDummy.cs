using System.Collections;
using UnityEngine;

/// <summary>
/// Kukła treningowa. Sama w sobie jest bardzo "głupia":
///  - przyjmuje obrażenia przez IDamageable (nie obchodzi jej, czym ją trafiono),
///  - przekazuje je do komponentu Health (który ma włączone odnawianie HP po wyzerowaniu),
///  - reaguje na siłę ciosu: słaby – stoi, średni – przesuwa się, mocny – odlatuje dalej i się przewraca,
///  - odgrywa reakcję: zmienia sprite / obraca ciało i błyska (SpriteFlash).
///
/// Budowa obiektu:
///   TrainingDummy  (ten skrypt, Health, Rigidbody2D, BodyStateTracker)
///   ├─ Body        (SpriteRenderer, CapsuleCollider2D, SpriteFlash) – tylko to się obraca przy powaleniu
///   └─ HealthBar   – zostaje prosto, bo nie jest dzieckiem Body
/// </summary>
[RequireComponent(typeof(Health), typeof(Rigidbody2D), typeof(BodyStateTracker))]
public class TrainingDummy : MonoBehaviour, IDamageable
{
    [Header("Body")]
    [Tooltip("Dziecko z grafiką i colliderem – obracane przy powaleniu.")]
    [SerializeField] private Transform body;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private SpriteFlash bodyFlash;

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

    [Header("Hit strength (DamageInfo.Force)")]
    [Tooltip("Siła poniżej tej wartości = słaby cios: dummy zostaje w miejscu.")]
    [SerializeField, Min(0f)] private float pushThreshold = 3f;
    [Tooltip("Siła od tej wartości = mocny cios: dummy jest odsuwany dalej i powalany.")]
    [SerializeField, Min(0f)] private float knockDownThreshold = 8f;
    [Tooltip("Prędkość pozioma (jednostki/s) nadawana przy średnim ciosie. Wyhamowuje ją tarcie o podłogę.")]
    [SerializeField, Min(0f)] private float pushSpeed = 1.2f;
    [Tooltip("Prędkość pozioma nadawana przy mocnym ciosie (powalającym).")]
    [SerializeField, Min(0f)] private float knockDownPushSpeed = 2f;

    [Header("Knock down")]
    [SerializeField, Min(0.1f)] private float knockDownDuration = 1.5f;
    [Tooltip("Faza 1: jak długo dummy stoi przechylony w stronę upadku.")]
    [SerializeField, Min(0.01f)] private float fallTiltDuration = 0.12f;
    [Tooltip("Faza 2: jak długo leży z przechyleniem w drugą stronę (\"próbuje wstać\").")]
    [SerializeField, Min(0.01f)] private float reboundDuration = 0.15f;
    [Tooltip("O ile podnieść leżące ciało, żeby nie wchodziło w podłogę (ok. połowa grubości sprite'a).")]
    [SerializeField] private float lyingHeightOffset = 0.1f;

    [Header("Debug")]
    [SerializeField] private float debugDamage = 10f;
    [SerializeField] private float debugLightForce = 1f;
    [SerializeField] private float debugMediumForce = 5f;
    [SerializeField] private float debugHeavyForce = 10f;
    [SerializeField] private float debugLaunchSpeed = 3f;

    private Health health;
    private Rigidbody2D rb;
    private BodyStateTracker bodyState;
    private Coroutine poseRoutine;
    private bool depletedThisHit;
    private int fallSign = 1; // +1 = upadek w prawo, -1 = w lewo

    private void Awake()
    {
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody2D>();
        bodyState = GetComponent<BodyStateTracker>();

        if (idleSprite == null)
            idleSprite = bodyRenderer.sprite;
    }

    private void OnEnable()
    {
        health.Depleted += OnDepleted;
        bodyState.StateChanged += OnBodyStateChanged;
    }

    private void OnDisable()
    {
        health.Depleted -= OnDepleted;
        bodyState.StateChanged -= OnBodyStateChanged;
    }

    // ---- IDamageable ----------------------------------------------------------------------

    public void TakeDamage(DamageInfo info)
    {
        depletedThisHit = false;
        health.ApplyDamage(info.Amount);   // może synchronicznie wywołać OnDepleted
        bodyFlash.Flash(depletedThisHit ? depletedFlashColor : hitFlashColor);

        bool fromLeft = HitCameFromLeft(info);
        int pushSign = fromLeft ? 1 : -1;  // cios z lewej pcha w prawo

        if (info.Force >= knockDownThreshold)
        {
            // Mocny cios: odsuń dalej i przewróć w stronę, w którą pcha cios.
            Push(pushSign, knockDownPushSpeed);
            fallSign = pushSign;
            bodyState.KnockDown(knockDownDuration); // -> StateChanged -> animacja upadku
            return;
        }

        if (info.Force >= pushThreshold)
            Push(pushSign, pushSpeed);    // średni cios: tylko przesunięcie

        // Leżący dummy nie przyjmuje "stojącej" pozy po ciosie – tylko błyska.
        if (!bodyState.IsKnockedDown)
            PlayHitPose(fromLeft);
    }

    // ---- Reakcja --------------------------------------------------------------------------

    private void OnDepleted()
    {
        depletedThisHit = true;
        // Tu można dodać np. dźwięk/efekt "rozbicia" – Health sam odnowi HP do maksimum.
    }

    private void OnBodyStateChanged(BodyState oldState, BodyState newState)
    {
        if (newState == BodyState.KnockedDown)
            StartPose(KnockDownRoutine(fallSign));
        else if (oldState == BodyState.KnockedDown)
            ResetPose(); // wstał
    }

    private void Push(int sign, float speed)
    {
        // Impuls = masa * zmiana prędkości, więc mnożymy przez masę: efekt nie zależy od ustawionej masy.
        rb.AddForce(new Vector2(sign * speed * rb.mass, 0f), ForceMode2D.Impulse);
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

    // ---- Pozy (sprite + obrót ciała) ------------------------------------------------------

    private void PlayHitPose(bool fromLeft)
    {
        StartPose(HitPoseRoutine(fromLeft ? hitFromLeftSprite : hitFromRightSprite));
    }

    private IEnumerator HitPoseRoutine(Sprite hitSprite)
    {
        SetPose(hitSprite, 0f, 0f);
        yield return new WaitForSeconds(hitPoseDuration);
        SetPose(idleSprite, 0f, 0f);
        poseRoutine = null;
    }

    private IEnumerator KnockDownRoutine(int sign)
    {
        // Upadek w prawo = obrót zgodnie z ruchem wskazówek zegara, czyli ujemny kąt w osi Z.
        float lyingAngle = -90f * sign;
        Sprite tiltTowardFall = sign > 0 ? hitFromLeftSprite : hitFromRightSprite;   // odchylony w stronę upadku
        Sprite tiltAwayFromFall = sign > 0 ? hitFromRightSprite : hitFromLeftSprite; // odchylony w drugą stronę

        // Faza 1: jeszcze stoi, ale przechyla się w stronę upadku.
        SetPose(tiltTowardFall, 0f, 0f);
        yield return new WaitForSeconds(fallTiltDuration);

        // Faza 2: leży (90°), a przechylenie w drugą stronę wygląda jak próba wstania – "góra" ciała unosi się.
        SetPose(tiltAwayFromFall, lyingAngle, lyingHeightOffset);
        yield return new WaitForSeconds(reboundDuration);

        // Faza 3: opada płasko i leży, aż BodyStateTracker zakończy powalenie.
        SetPose(idleSprite, lyingAngle, lyingHeightOffset);
        poseRoutine = null;
    }

    private void ResetPose()
    {
        if (poseRoutine != null)
            StopCoroutine(poseRoutine);
        poseRoutine = null;
        SetPose(idleSprite, 0f, 0f);
    }

    private void StartPose(IEnumerator routine)
    {
        // Nowa poza zawsze przerywa poprzednią (np. powalenie w trakcie pozy po ciosie).
        if (poseRoutine != null)
            StopCoroutine(poseRoutine);
        poseRoutine = StartCoroutine(routine);
    }

    private void SetPose(Sprite sprite, float angle, float height)
    {
        if (sprite != null)
            bodyRenderer.sprite = sprite;
        // Pivot sprite'a jest przy stopach, więc obrót kładzie ciało "na bok" wokół podstawy.
        body.localRotation = Quaternion.Euler(0f, 0f, angle);
        body.localPosition = new Vector3(0f, height, 0f);
    }

    // ---- Narzędzia do testów (prawy klik na komponencie w Inspectorze, w trybie Play) ------

    [ContextMenu("Debug/Light Hit From Left")]   private void DebugLightLeft()   => DebugHit(debugLightForce, true);
    [ContextMenu("Debug/Light Hit From Right")]  private void DebugLightRight()  => DebugHit(debugLightForce, false);
    [ContextMenu("Debug/Medium Hit From Left")]  private void DebugMediumLeft()  => DebugHit(debugMediumForce, true);
    [ContextMenu("Debug/Medium Hit From Right")] private void DebugMediumRight() => DebugHit(debugMediumForce, false);
    [ContextMenu("Debug/Heavy Hit From Left")]   private void DebugHeavyLeft()   => DebugHit(debugHeavyForce, true);
    [ContextMenu("Debug/Heavy Hit From Right")]  private void DebugHeavyRight()  => DebugHit(debugHeavyForce, false);

    [ContextMenu("Debug/Launch Up")]
    private void DebugLaunchUp() => rb.AddForce(Vector2.up * debugLaunchSpeed * rb.mass, ForceMode2D.Impulse);

    [ContextMenu("Debug/Knock Down")]
    private void DebugKnockDown() => bodyState.KnockDown(knockDownDuration);

    [ContextMenu("Debug/Get Up")]
    private void DebugGetUp() => bodyState.GetUp();

    private void DebugHit(float force, bool fromLeft) =>
        TakeDamage(new DamageInfo(debugDamage, transform.position, fromLeft ? Vector2.right : Vector2.left, null, force));
}
