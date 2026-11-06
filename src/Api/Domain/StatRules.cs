namespace Api.Domain;

// Ranges and labels from the Steading mechanics reference in CLAUDE.md.
public static class StatRules
{
    private sealed record Rule(int Min, int? Max, IReadOnlyDictionary<int, string>? Labels = null);

    private static readonly Dictionary<SteadingStat, Rule> Rules = new()
    {
        [SteadingStat.Size] = new(-1, 2, new Dictionary<int, string>
        {
            [-1] = "Hamlet",
            [0] = "Village",
            [1] = "Town",
            [2] = "City",
        }),
        [SteadingStat.Fortunes] = new(-3, 3),
        [SteadingStat.Surplus] = new(0, null),
        [SteadingStat.Prosperity] = new(-2, 2, new Dictionary<int, string>
        {
            [-2] = "Dirt",
            [-1] = "Poor",
            [0] = "Moderate",
            [1] = "Wealthy",
            [2] = "Rich",
        }),
        [SteadingStat.Defenses] = new(-2, 3, new Dictionary<int, string>
        {
            [-2] = "None",
            [-1] = "Militia",
            [0] = "Watch",
            [1] = "Guard",
            [2] = "Garrison",
            [3] = "Battalion",
        }),
        [SteadingStat.Population] = new(-2, 2, new Dictionary<int, string>
        {
            [-2] = "Exodus",
            [-1] = "Shrinking",
            [0] = "Steady",
            [1] = "Growing",
            [2] = "Booming",
        }),
    };

    public static int Min(SteadingStat stat) => Rules[stat].Min;

    public static bool IsValid(SteadingStat stat, int value)
    {
        var rule = Rules[stat];
        return value >= rule.Min && (rule.Max is null || value <= rule.Max);
    }

    public static string? Label(SteadingStat stat, int value) =>
        Rules[stat].Labels?.GetValueOrDefault(value);
}
