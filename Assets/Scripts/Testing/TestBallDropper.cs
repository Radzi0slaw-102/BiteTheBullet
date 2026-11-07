using UnityEngine;

/// <summary>
/// Narzędzie testowe: co określony czas zrzuca piłkę (prefab z Rigidbody2D + DamageSource)
/// z lekkim losowym przesunięciem w poziomie – żeby trafiała raz z lewej, raz z prawej strony dummy'ego.
/// Do usunięcia, gdy gracz będzie miał działającą broń.
/// </summary>
public class TestBallDropper : MonoBehaviour
{
    [SerializeField] private GameObject ballPrefab;
    [SerializeField, Min(0.1f)] private float interval = 1.5f;
    [SerializeField, Min(0f)] private float horizontalJitter = 0.08f;
    [SerializeField, Min(0.1f)] private float ballLifetime = 4f;

    private float timer;

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
        GameObject ball = Instantiate(ballPrefab, transform.position + offset, Quaternion.identity);
        Destroy(ball, ballLifetime); // sprzątanie, żeby piłki nie zalegały na scenie
    }

    private void OnDrawGizmos()
    {
        // Podgląd w edytorze: odcinek, z którego spadają piłki.
        Gizmos.color = Color.yellow;
        Vector3 p = transform.position;
        Gizmos.DrawLine(p + Vector3.left * horizontalJitter, p + Vector3.right * horizontalJitter);
        Gizmos.DrawWireSphere(p, 0.05f);
    }
}
