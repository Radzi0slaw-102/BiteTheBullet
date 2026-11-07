/// <summary>
/// Kontrakt "da się mnie trafić". Implementuje go wszystko, co może przyjmować obrażenia
/// (dummy, przeciwnicy, gracz, skrzynki...).
///
/// Dowolna broń wystarczy, że znajdzie na trafionym obiekcie IDamageable i wywoła TakeDamage –
/// nie musi wiedzieć, czy trafiła w dummy'ego, czy w coś innego.
/// </summary>
public interface IDamageable
{
    void TakeDamage(DamageInfo info);
}
