using Api.Domain;

namespace Api.Steadings;

public sealed record StatResponse(SteadingStat Stat, int Raw, int Effective, string? Label, int Min, int? Max);

public sealed record SteadingResponse(
    string Name,
    IReadOnlyList<StatResponse> Stats,
    IReadOnlyList<Debility> Debilities)
{
    public static SteadingResponse From(Steading steading)
    {
        var effective = EffectiveValues.Compute(steading.Stats, steading.Debilities);
        var stats = Enum.GetValues<SteadingStat>()
            .Select(stat => new StatResponse(
                stat,
                steading.Stats.Get(stat),
                effective.Get(stat),
                StatRules.Label(stat, effective.Get(stat)),
                StatRules.Min(stat),
                StatRules.Max(stat)))
            .ToList();
        return new SteadingResponse(steading.Name, stats, steading.Debilities.Order().ToList());
    }
}

public sealed record ChangeStatRequest(int? Value);

public sealed record ShareLinkResponse(string Token);

public sealed record LogEntryResponse(SteadingStat Stat, int OldValue, int NewValue, DateTimeOffset ChangedAt);
