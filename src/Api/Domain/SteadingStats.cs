namespace Api.Domain;

public sealed record SteadingStats(
    int Size,
    int Fortunes,
    int Surplus,
    int Prosperity,
    int Defenses,
    int Population);
