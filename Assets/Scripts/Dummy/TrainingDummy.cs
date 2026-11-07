using System.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Kukła treningowa. Sama w sobie jest bardzo "głupia":
///  - przyjmuje obrażenia przez IDamageable (nie obchodzi jej, czym ją trafiono),
///  - przekazuje je do komponentu Health (który ma włączone odnawianie HP po wyzerowaniu),
///  - reaguje na siłę ciosu: słaby – stoi, średni – przesuwa się, mocny – odlatuje dalej i się przewraca,
///  - odgrywa animacje z klatek (idle, bujanie po ciosie, upadek, wstawanie, poza w powietrzu) i błyska.
///
/// Klatki są narysowane dla jednego kierunku (cios z lewej, upadek w prawo).
/// Drugi kierunek to lustrzane odbicie sprite'a (SpriteRenderer.flipX).
///
/// Budowa obiektu:
///   TrainingDummy  (ten skrypt, Health, Rigidbody2D, BodyStateTracker)
///   ├─ Body        (SpriteRenderer, CapsuleCollider2D, SpriteFlash)
///   └─ HealthBar
/// </summary>
[RequireComponent(typeof(Health), typeof(Rigidbody2D), typeof(BodyStateTracker))]
public class TrainingDummy : MonoBehaviour, IDamageable
{
    private enum Pose { Idle, Hit, Airborne, Falling, GettingUp }

    [Header("Body")]
    [Tooltip("Dziecko z grafiką i colliderem.")]
    [SerializeField] private Transform body;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private SpriteFlash bodyFlash;
    [SerializeField] private CapsuleCollider2D bodyCollider;

    [Header("Animation – idle")]
    [Tooltip("Klatki pozy stojącej, odtwarzane w pętli.")]
    [SerializeField] private Sprite[] idleFrames;
    [SerializeField, Min(0.1f)] private float idleFps = 2f;

    [Header("Animation – hit")]
    [Tooltip("Bujanie po ciosie, narysowane dla ciosu z LEWEJ (cios z prawej = odbicie lustrzane).")]
    [SerializeField] private Sprite[] hitFrames;
    [SerializeField, Min(1f)] private float hitFps = 14f;

    [Header("Animation – knock down")]
    [Tooltip("Upadek, narysowany jako przewracanie się w PRAWO (w lewo = odbicie lustrzane). Ostatnia klatka = leżenie.")]
    [SerializeField] private Sprite[] fallFrames;
    [SerializeField, Min(1f)] private float fallFps = 12f;
    [Tooltip("Wstawanie = klatki upadku od tyłu.")]
    [SerializeField, Min(1f)] private float getUpFps = 10f;

    [Header("Animation – airborne")]
    [SerializeField] private Sprite airborneSprite;
    [Tooltip("O ile unieść grafikę (i collider), gdy dummy jest w powietrzu.")]
    [SerializeField] private float airborneLift = 0.06f;

    [Header("Collider")]
    [SerializeField] private Vector2 standingColliderSize = new(0.4f, 0.85f);
    [SerializeField] private Vector2 standingColliderOffset = new(0f, 0.425f);
    [Tooltip("Kształt leżącego dummy'ego. Offset X jest dla upadku w prawo – przy upadku w lewo jest odwracany.")]
    [SerializeField] private Vector2 lyingColliderSize = new(0.95f, 0.3f);
    [SerializeField] private Vector2 lyingColliderOffset = new(0.03f, 0.15f);
    [Tooltip("Od której klatki upadku (licząc od 0) collider jest już \"leżący\".")]
    [SerializeField, Min(0)] private int lyingColliderFromFrame = 3;

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
    [Tooltip("Jak długo (s) od powalenia dummy leży, zanim zacznie wstawać.")]
    [SerializeField, Min(0.1f)] private float knockDownDuration = 1.5f;

    [Header("Particles")]
    [SerializeField] private ParticleSystem bloodParticles;

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
    private Pose pose;
    private bool depletedThisHit;
    private int fallSign = 1; // +1 = upadek w prawo, -1 = w lewo

    private void Awake()
    {
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody2D>();
        bodyState = GetComponent<BodyStateTracker>();
    }

    private void OnEnable()
    {
        health.Depleted += OnDepleted;
        bodyState.StateChanged += OnBodyStateChanged;
        bodyState.KnockDownStarted += OnKnockDownStarted;
        bodyState.KnockDownTimeElapsed += OnKnockDownTimeElapsed;
        PlayIdle();
    }

    private void OnDisable()
    {
        health.Depleted -= OnDepleted;
        bodyState.StateChanged -= OnBodyStateChanged;
        bodyState.KnockDownStarted -= OnKnockDownStarted;
        bodyState.KnockDownTimeElapsed -= OnKnockDownTimeElapsed;
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
            Push(pushSign, knockDownPushSpeed, info.UpForce);
            fallSign = pushSign;
            bodyState.KnockDown(knockDownDuration); // -> KnockDownStarted -> animacja upadku
            return;
        }

        if (info.Force >= pushThreshold)
            Push(pushSign, pushSpeed, info.UpForce);    // średni cios: tylko przesunięcie

        // Bujanie tylko, gdy dummy stoi. Leżący / wstający / lecący tylko błyska.
        if (pose == Pose.Idle || pose == Pose.Hit)
            PlayHit(fromLeft);

        //Emitowanie particli krwi w zależności od kierunku uderzenia
        
        bloodParticles.transform.localScale = new Vector3(pushSign,1, 1);
        bloodParticles.Emit(25);
    }

    // ---- Reakcje na zdarzenia -------------------------------------------------------------

    private void OnDepleted()
    {
        depletedThisHit = true;
    }

    private void OnKnockDownStarted()
    {
        // Wywoływane przy każdym powaleniu. Jeśli dummy już leży (lub właśnie upada) – zostaje,
        // jeśli w trakcie wstawania – przewraca się ponownie.
        if (pose != Pose.Falling)
            StartPose(Pose.Falling, FallRoutine(fallSign));
    }

    private void OnKnockDownTimeElapsed()
    {
        // Czas leżenia minął – wstajemy animacją. Stan nadal jest KnockedDown aż do jej końca.
        StartPose(Pose.GettingUp, GetUpRoutine());
    }

    private void OnBodyStateChanged(BodyState oldState, BodyState newState)
    {
        // Powaleniem i wstawaniem zajmują się zdarzenia KnockDown*; tu tylko wybicie w górę i lądowanie.
        if (newState == BodyState.Airborne)
            ShowAirborne();
        else if (newState == BodyState.Standing && pose == Pose.Airborne)
            PlayIdle();
    }

    private void Push(int sign, float speedX, float speedY)
    {
        // Impuls = masa * zmiana prędkości, więc mnożymy przez masę: efekt nie zależy od ustawionej masy.
        rb.AddForce(new Vector2(sign * speedX * rb.mass, speedY * rb.mass), ForceMode2D.Impulse);
    }

    private bool HitCameFromLeft(DamageInfo info)
    {   
        // 1) Kierunek ciosu ma wyraźną składową poziomą -> on decyduje (cios w prawo = przyszedł z lewej).
        if (Mathf.Abs(info.Direction.x) > 0.2f)
            return info.Direction.x > 0f;

        // 2) Cios prawie pionowy -> patrzymy, po której stronie środka trafił.
        if (!Mathf.Approximately(info.Point.x, transform.position.x))
            return info.Point.x < transform.position.x;

        // 3) Idealnie w środek – losujemy.
        return UnityEngine.Random.value < 0.5f;
    }

    // ---- Pozy i animacje ------------------------------------------------------------------

    private void PlayIdle() => StartPose(Pose.Idle, IdleRoutine());

    private void PlayHit(bool fromLeft) => StartPose(Pose.Hit, HitRoutine(fromLeft));

    private void ShowAirborne()
    {
        StopPose();
        pose = Pose.Airborne;
        ShowFrame(airborneSprite, airborneLift, lying: false);
    }

    private IEnumerator IdleRoutine()
    {
        // Pętla: 0_4, 0_5, 0_4, ... Kierunek (flipX) zostaje taki, jaki był – dummy się nie "przekręca".
        if (idleFrames == null || idleFrames.Length == 0)
            yield break;
        for (int i = 0; ; i = (i + 1) % idleFrames.Length)
        {
            ShowFrame(idleFrames[i], 0f, lying: false);
            yield return new WaitForSeconds(1f / idleFps);
        }
    }

    private IEnumerator HitRoutine(bool fromLeft)
    {
        bodyRenderer.flipX = !fromLeft; // klatki są dla ciosu z lewej
        for (int i = 0; i < hitFrames.Length; i++)
        {
            ShowFrame(hitFrames[i], 0f, lying: false);
            yield return new WaitForSeconds(1f / hitFps);
        }

        poseRoutine = null;
        // W trakcie bujania dummy mógł zostać podrzucony.
        if (bodyState.CurrentBodyState == BodyState.Airborne)
            ShowAirborne();
        else
            PlayIdle();
    }

    private IEnumerator FallRoutine(int sign)
    {
        bodyRenderer.flipX = sign < 0; // klatki są dla upadku w prawo
        for (int i = 0; i < fallFrames.Length; i++)
        {
            ShowFrame(fallFrames[i], 0f, lying: i >= lyingColliderFromFrame);
            yield return new WaitForSeconds(1f / fallFps);
        }
        // Ostatnia klatka (leżenie) zostaje na ekranie, aż BodyStateTracker ogłosi koniec czasu.
        poseRoutine = null;
    }

    private IEnumerator GetUpRoutine()
    {
        // Klatki upadku od tyłu: z leżenia do pionu.
        for (int i = fallFrames.Length - 1; i >= 0; i--)
        {
            ShowFrame(fallFrames[i], 0f, lying: i >= lyingColliderFromFrame);
            yield return new WaitForSeconds(1f / getUpFps);
        }

        poseRoutine = null;
        PlayIdle();
        bodyState.GetUp();  // dopiero teraz stan przestaje być KnockedDown (stojący albo w powietrzu)
    }

    private void StartPose(Pose newPose, IEnumerator routine)
    {
        // Nowa poza zawsze przerywa poprzednią (np. powalenie w trakcie bujania).
        StopPose();
        pose = newPose;
        poseRoutine = StartCoroutine(routine);
    }

    private void StopPose()
    {
        if (poseRoutine != null)
            StopCoroutine(poseRoutine);
        poseRoutine = null;
    }

    private void ShowFrame(Sprite sprite, float lift, bool lying)
    {
        if (sprite != null)
            bodyRenderer.sprite = sprite;
        body.localPosition = new Vector3(0f, lift, 0f);

        // Collider dopasowany do kształtu na klatce. Offset X odwracamy razem z grafiką.
        float side = bodyRenderer.flipX ? -1f : 1f;
        if (lying)
        {
            bodyCollider.direction = CapsuleDirection2D.Horizontal;
            bodyCollider.size = lyingColliderSize;
            bodyCollider.offset = new Vector2(lyingColliderOffset.x * side, lyingColliderOffset.y);
        }
        else
        {
            bodyCollider.direction = CapsuleDirection2D.Vertical;
            bodyCollider.size = standingColliderSize;
            bodyCollider.offset = new Vector2(standingColliderOffset.x * side, standingColliderOffset.y);
        }
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
    private void DebugGetUp()
    {
        if (bodyState.IsKnockedDown && pose != Pose.GettingUp)
            StartPose(Pose.GettingUp, GetUpRoutine());
    }

    private void DebugHit(float force, bool fromLeft) =>
        TakeDamage(new DamageInfo(debugDamage, transform.position, fromLeft ? Vector2.right : Vector2.left, null, force));
}
