using Api.Domain;

namespace Api.Steadings;

// Everything players can see. These records list each field on purpose, so new columns
// on Steading or LogEntry can't reach players unless they're added here.
public sealed record PlayerStatResponse(SteadingStat Stat, int Value, string? Label);

public sealed record PlayerSteadingResponse(
    string Name,
    IReadOnlyList<PlayerStatResponse> Stats,
    IReadOnlyList<Debility> Debilities)
{
    public static PlayerSteadingResponse From(Steading steading)
    {
        var effective = EffectiveValues.Compute(steading.Stats, steading.Debilities);
        var stats = Enum.GetValues<SteadingStat>()
            .Select(stat => new PlayerStatResponse(
                stat,
                effective.Get(stat),
                StatRules.Label(stat, effective.Get(stat))))
            .ToList();
        return new PlayerSteadingResponse(steading.Name, stats, steading.Debilities.Order().ToList());
    }
}

public sealed record PlayerLogEntryResponse(SteadingStat Stat, int OldValue, int NewValue, DateTimeOffset ChangedAt)
{
    public static PlayerLogEntryResponse From(LogEntryResponse entry) =>
        new(entry.Stat, entry.OldValue, entry.NewValue, entry.ChangedAt);
}
