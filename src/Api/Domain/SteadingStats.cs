namespace Api.Domain;

public sealed record SteadingStats(
    int Size,
    int Fortunes,
    int Surplus,
    int Prosperity,
    int Defenses,
    int Population)
{
    public int Get(SteadingStat stat) => stat switch
    {
        SteadingStat.Size => Size,
        SteadingStat.Fortunes => Fortunes,
        SteadingStat.Surplus => Surplus,
        SteadingStat.Prosperity => Prosperity,
        SteadingStat.Defenses => Defenses,
        SteadingStat.Population => Population,
        _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, null),
    };
}
