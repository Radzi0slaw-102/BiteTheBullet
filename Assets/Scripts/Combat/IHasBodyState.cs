/// <summary>
/// Kontrakt "da się odczytać mój stan ciała". Broń przy kolizji robi:
///
///     var target = other.GetComponentInParent&lt;IHasBodyState&gt;();
///     if (target != null &amp;&amp; target.CurrentBodyState == BodyState.Airborne) { ... }
///
/// i nie musi wiedzieć, czy trafiła w dummy'ego, czy w innego przeciwnika.
/// </summary>
public interface IHasBodyState
{
    BodyState CurrentBodyState { get; }
}
