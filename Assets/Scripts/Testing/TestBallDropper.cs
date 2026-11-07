using UnityEngine;

/// <summary>
/// Narzędzie testowe: co określony czas zrzuca piłkę (prefab z Rigidbody2D + DamageSource)
/// z lekkim losowym przesunięciem w poziomie – żeby trafiała raz z lewej, raz z prawej strony dummy'ego.
/// Kolejne piłki dostają kolejne siły z forceSequence (słaba / średnia / mocna), a zrzutnia
/// podąża w poziomie za celem, bo przesuwany dummy szybko by spod niej uciekł.
/// Do usunięcia, gdy gracz będzie miał działającą broń.
/// </summary>
public class TestBallDropper : MonoBehaviour
{
    [SerializeField] private GameObject ballPrefab;
    [SerializeField, Min(0.1f)] private float interval = 1.5f;
    [SerializeField, Min(0f)] private float horizontalJitter = 0.08f;
    [SerializeField, Min(0.1f)] private float ballLifetime = 4f;

    [Tooltip("Puste = zrzucaj zawsze z pozycji tego obiektu. Ustawione = zrzucaj nad celem (oś X celu, wysokość tego obiektu).")]
    [SerializeField] private Transform followTarget;

    [Tooltip("Siły kolejnych piłek (DamageSource.KnockbackForce), w kółko. Puste = siła z prefabu.")]
    [SerializeField] private float[] forceSequence = { 1f, 5f, 10f };

    private float timer;
    private int nextForceIndex;

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f)
            return;

        timer = interval;
        DropBall();
    }

    [ContextMenu("Drop Ball")]
    public void DropBall()
    {
        if (ballPrefab == null)
            return;

        Vector3 offset = new(Random.Range(-horizontalJitter, horizontalJitter), 0f, 0f);
        GameObject ball = Instantiate(ballPrefab, DropOrigin() + offset, Quaternion.identity);

        if (forceSequence.Length > 0 && ball.TryGetComponent(out DamageSource damageSource))
        {
            damageSource.KnockbackForce = forceSequence[nextForceIndex];
            nextForceIndex = (nextForceIndex + 1) % forceSequence.Length;
        }

        Destroy(ball, ballLifetime); // sprzątanie, żeby piłki nie zalegały na scenie
    }

    private Vector3 DropOrigin()
    {
        Vector3 origin = transform.position;
        if (followTarget != null)
            origin.x = followTarget.position.x;
        return origin;
    }

    private void OnDrawGizmos()
    {
        // Podgląd w edytorze: odcinek, z którego spadają piłki.
        Gizmos.color = Color.yellow;
        Vector3 p = DropOrigin();
        Gizmos.DrawLine(p + Vector3.left * horizontalJitter, p + Vector3.right * horizontalJitter);
        Gizmos.DrawWireSphere(p, 0.05f);
    }
}
