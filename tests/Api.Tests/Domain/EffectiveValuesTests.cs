using Api.Domain;

namespace Api.Tests.Domain;

public sealed class EffectiveValuesTests
{
    private static readonly SteadingStats Raw = new(
        Size: 1,
        Fortunes: 1,
        Surplus: 1,
        Prosperity: 1,
        Defenses: 1,
        Population: 1);

    [Fact]
    public void No_debilities_leaves_values_unchanged()
    {
        var effective = EffectiveValues.Compute(Raw, new HashSet<Debility>());

        Assert.Equal(Raw, effective);
    }

    [Fact]
    public void Lacking_lowers_prosperity_by_one_and_leaves_other_stats_alone()
    {
        var effective = EffectiveValues.Compute(Raw, new HashSet<Debility> { Debility.Lacking });

        Assert.Equal(Raw with { Prosperity = 0 }, effective);
    }

    [Fact]
    public void Lacking_at_dirt_stays_at_dirt()
    {
        var dirt = Raw with { Prosperity = -2 };

        var effective = EffectiveValues.Compute(dirt, new HashSet<Debility> { Debility.Lacking });

        Assert.Equal(dirt, effective);
    }

    [Theory]
    [InlineData(Debility.Diminished)]
    [InlineData(Debility.Malcontent)]
    public void Debilities_without_a_numeric_effect_change_nothing(Debility debility)
    {
        var effective = EffectiveValues.Compute(Raw, new HashSet<Debility> { debility });

        Assert.Equal(Raw, effective);
    }
}
