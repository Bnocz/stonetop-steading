namespace Api.Domain;

public static class EffectiveValues
{
    // Only Lacking changes a number. Diminished and Malcontent affect moves, which the app doesn't roll.
    // Lacking can't take Prosperity below the bottom of its table (Dirt).
    public static SteadingStats Compute(SteadingStats raw, IReadOnlySet<Debility> debilities) =>
        debilities.Contains(Debility.Lacking)
            ? raw with { Prosperity = Math.Max(raw.Prosperity - 1, StatRules.Min(SteadingStat.Prosperity)) }
            : raw;
}
