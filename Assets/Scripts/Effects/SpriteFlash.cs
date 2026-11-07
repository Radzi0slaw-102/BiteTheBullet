using System.Collections;
using UnityEngine;

/// <summary>
/// Krótki błysk całego sprite'a (wszystkie piksele naraz) – np. na biało albo na czerwono po trafieniu.
///
/// Dwa tryby, wybierane automatycznie:
///  1. Materiał z shaderem "BiteTheBullet/Sprite Flash" (ma właściwość _FlashAmount):
///     kolor pikseli jest mieszany z kolorem błysku -> można ROZJAŚNIĆ do bieli.
///  2. Dowolny inny materiał (np. domyślny Sprite-Lit): fallback na SpriteRenderer.color.
///     To tylko MNOŻY kolory, więc da się zaczerwienić/przyciemnić, ale nie rozjaśnić.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFlash : MonoBehaviour
{
    // Shader.PropertyToID liczymy raz – szybsze niż przekazywanie stringa przy każdej klatce.
    private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
    private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

    [SerializeField] private Color flashColor = Color.white;
    [SerializeField, Min(0.01f)] private float duration = 0.12f;

    private SpriteRenderer spriteRenderer;
    // MaterialPropertyBlock pozwala zmienić parametry shadera tylko dla TEGO renderera,
    // bez tworzenia kopii materiału (inne dummy z tym samym materiałem nie błysną).
    private MaterialPropertyBlock propertyBlock;
    private bool materialSupportsFlash;
    private Color baseColor;
    private Coroutine flashRoutine;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        materialSupportsFlash = spriteRenderer.sharedMaterial != null
                                && spriteRenderer.sharedMaterial.HasProperty(FlashAmountId);
        baseColor = spriteRenderer.color;
    }

    private void OnDisable()
    {
        // Gdy obiekt zostanie wyłączony w trakcie błysku, korutyna ginie – sprzątamy efekt ręcznie.
        flashRoutine = null;
        ApplyFlash(flashColor, 0f);
    }

    public void Flash() => Flash(flashColor);

    public void Flash(Color color)
    {
        // Kolejne trafienie w trakcie błysku -> zaczynamy błysk od nowa.
        if (flashRoutine != null)
            StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine(color));
    }

    private IEnumerator FlashRoutine(Color color)
    {
        // Pełny błysk na starcie, potem liniowe wygaszanie do zera.
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            ApplyFlash(color, 1f - t / duration);
            yield return null; // czekaj do następnej klatki
        }

        ApplyFlash(color, 0f);
        flashRoutine = null;
    }

    private void ApplyFlash(Color color, float amount)
    {
        if (materialSupportsFlash)
        {
            // MaterialPropertyBlock nie jest serializowany – po przeładowaniu skryptów w trakcie Play Mode
            // byłby null (Awake nie wykonuje się ponownie), więc tworzymy go w razie potrzeby.
            propertyBlock ??= new MaterialPropertyBlock();

            // Get -> modyfikuj -> Set, żeby nie skasować danych, które SpriteRenderer sam trzyma w bloku.
            spriteRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(FlashColorId, color);
            propertyBlock.SetFloat(FlashAmountId, amount);
            spriteRenderer.SetPropertyBlock(propertyBlock);
        }
        else
        {
            spriteRenderer.color = Color.Lerp(baseColor, baseColor * color, amount);
        }
    }
}
