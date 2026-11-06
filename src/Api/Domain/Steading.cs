namespace Api.Domain;

// Stores raw values only; EffectiveValues computes what debilities do to them.
public sealed class Steading
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public string Name { get; private set; } = "";

    public int Size { get; private set; }

    public int Fortunes { get; private set; }

    public int Surplus { get; private set; }

    public int Prosperity { get; private set; }

    public int Defenses { get; private set; }

    public int Population { get; private set; }

    public bool Diminished { get; private set; }

    public bool Lacking { get; private set; }

    public bool Malcontent { get; private set; }

    public uint Version { get; private set; }

    public SteadingStats Stats => new(Size, Fortunes, Surplus, Prosperity, Defenses, Population);

    public IReadOnlySet<Debility> Debilities
    {
        get
        {
            var active = new HashSet<Debility>();
            if (Diminished) active.Add(Debility.Diminished);
            if (Lacking) active.Add(Debility.Lacking);
            if (Malcontent) active.Add(Debility.Malcontent);
            return active;
        }
    }

    private Steading()
    {
    }

    public static Steading CreateStonetop() => new()
    {
        Name = "Stonetop",
        Size = 0,
        Fortunes = 1,
        Surplus = 1,
        Prosperity = -1,
        Defenses = -1,
        Population = 0,
    };

    // Returns the log entry the caller must save with this change, or null if the value didn't change.
    public LogEntry? ChangeStat(SteadingStat stat, int newValue, DateTimeOffset changedAt)
    {
        if (!StatRules.IsValid(stat, newValue))
        {
            throw new ArgumentOutOfRangeException(nameof(newValue), newValue, $"Invalid value for {stat}.");
        }

        var oldValue = GetStat(stat);
        if (oldValue == newValue)
        {
            return null;
        }

        SetStat(stat, newValue);
        return new LogEntry(Id, stat, oldValue, newValue, changedAt);
    }

    private int GetStat(SteadingStat stat) => stat switch
    {
        SteadingStat.Size => Size,
        SteadingStat.Fortunes => Fortunes,
        SteadingStat.Surplus => Surplus,
        SteadingStat.Prosperity => Prosperity,
        SteadingStat.Defenses => Defenses,
        SteadingStat.Population => Population,
        _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, null),
    };

    private void SetStat(SteadingStat stat, int value)
    {
        switch (stat)
        {
            case SteadingStat.Size: Size = value; break;
            case SteadingStat.Fortunes: Fortunes = value; break;
            case SteadingStat.Surplus: Surplus = value; break;
            case SteadingStat.Prosperity: Prosperity = value; break;
            case SteadingStat.Defenses: Defenses = value; break;
            case SteadingStat.Population: Population = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(stat), stat, null);
        }
    }
}
