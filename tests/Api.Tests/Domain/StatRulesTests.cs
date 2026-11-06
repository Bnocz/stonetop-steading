using Api.Domain;

namespace Api.Tests.Domain;

public sealed class StatRulesTests
{
    [Theory]
    [InlineData(SteadingStat.Size, -1)]
    [InlineData(SteadingStat.Size, 2)]
    [InlineData(SteadingStat.Fortunes, -3)]
    [InlineData(SteadingStat.Fortunes, 3)]
    [InlineData(SteadingStat.Surplus, 0)]
    [InlineData(SteadingStat.Surplus, 1000)]
    [InlineData(SteadingStat.Prosperity, -2)]
    [InlineData(SteadingStat.Prosperity, 2)]
    [InlineData(SteadingStat.Defenses, -2)]
    [InlineData(SteadingStat.Defenses, 3)]
    [InlineData(SteadingStat.Population, -2)]
    [InlineData(SteadingStat.Population, 2)]
    public void Values_at_the_bounds_are_valid(SteadingStat stat, int value)
    {
        Assert.True(StatRules.IsValid(stat, value));
    }

    [Theory]
    [InlineData(SteadingStat.Size, -2)]
    [InlineData(SteadingStat.Size, 3)]
    [InlineData(SteadingStat.Fortunes, -4)]
    [InlineData(SteadingStat.Fortunes, 4)]
    [InlineData(SteadingStat.Surplus, -1)]
    [InlineData(SteadingStat.Prosperity, -3)]
    [InlineData(SteadingStat.Prosperity, 3)]
    [InlineData(SteadingStat.Defenses, -3)]
    [InlineData(SteadingStat.Defenses, 4)]
    [InlineData(SteadingStat.Population, -3)]
    [InlineData(SteadingStat.Population, 3)]
    public void Values_one_step_outside_the_bounds_are_invalid(SteadingStat stat, int value)
    {
        Assert.False(StatRules.IsValid(stat, value));
    }

    [Theory]
    [InlineData(SteadingStat.Size, -1, "Hamlet")]
    [InlineData(SteadingStat.Size, 0, "Village")]
    [InlineData(SteadingStat.Size, 1, "Town")]
    [InlineData(SteadingStat.Size, 2, "City")]
    [InlineData(SteadingStat.Prosperity, -2, "Dirt")]
    [InlineData(SteadingStat.Prosperity, -1, "Poor")]
    [InlineData(SteadingStat.Prosperity, 0, "Moderate")]
    [InlineData(SteadingStat.Prosperity, 1, "Wealthy")]
    [InlineData(SteadingStat.Prosperity, 2, "Rich")]
    [InlineData(SteadingStat.Defenses, -2, "None")]
    [InlineData(SteadingStat.Defenses, -1, "Militia")]
    [InlineData(SteadingStat.Defenses, 0, "Watch")]
    [InlineData(SteadingStat.Defenses, 1, "Guard")]
    [InlineData(SteadingStat.Defenses, 2, "Garrison")]
    [InlineData(SteadingStat.Defenses, 3, "Battalion")]
    [InlineData(SteadingStat.Population, -2, "Exodus")]
    [InlineData(SteadingStat.Population, -1, "Shrinking")]
    [InlineData(SteadingStat.Population, 0, "Steady")]
    [InlineData(SteadingStat.Population, 1, "Growing")]
    [InlineData(SteadingStat.Population, 2, "Booming")]
    public void Each_value_has_its_label(SteadingStat stat, int value, string expected)
    {
        Assert.Equal(expected, StatRules.Label(stat, value));
    }

    [Theory]
    [InlineData(SteadingStat.Fortunes, 1)]
    [InlineData(SteadingStat.Surplus, 1)]
    public void Stats_without_a_label_table_have_no_label(SteadingStat stat, int value)
    {
        Assert.Null(StatRules.Label(stat, value));
    }

    [Fact]
    public void Value_outside_a_label_table_has_no_label()
    {
        Assert.Null(StatRules.Label(SteadingStat.Prosperity, 3));
    }
}
