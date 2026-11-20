namespace Api.Domain;

// A session of play, with the route the party travelled on the region map.
// Hidden from players (GmOnly) until the GM shares it.
public sealed class PlaySession
{
    public const int MinNumber = 1;
    public const int MaxNumber = 999;
    public const int TitleMaxLength = 100;
    public const int MaxWaypoints = 200;

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid SteadingId { get; private set; }

    public int Number { get; private set; }

    public string Title { get; private set; } = "";

    public bool GmOnly { get; private set; } = true;

    // In travel order. The party ends the session at the last one.
    public List<Waypoint> Waypoints { get; private set; } = [];

    private PlaySession()
    {
    }

    public static PlaySession Create(Guid steadingId, int? number, string? title, bool gmOnly)
    {
        var session = new PlaySession { SteadingId = steadingId };
        session.Update(number, title, gmOnly);
        return session;
    }

    // Validates before changing anything. A null gmOnly keeps the current visibility.
    public void Update(int? number, string? title, bool? gmOnly)
    {
        if (number is not (>= MinNumber and <= MaxNumber))
        {
            throw new ArgumentException($"The session number must be from {MinNumber} to {MaxNumber}.", nameof(number));
        }

        var trimmedTitle = title?.Trim() ?? "";
        if (trimmedTitle.Length > TitleMaxLength)
        {
            throw new ArgumentException($"The title can be at most {TitleMaxLength} characters.", nameof(title));
        }

        Number = number.Value;
        Title = trimmedTitle;
        GmOnly = gmOnly ?? GmOnly;
    }

    // Replaces the whole route; nothing changes if any waypoint is invalid.
    public void ReplaceRoute(IReadOnlyList<Waypoint> waypoints)
    {
        if (waypoints.Count > MaxWaypoints)
        {
            throw new ArgumentException($"A route can have at most {MaxWaypoints} waypoints.", nameof(waypoints));
        }

        Waypoints = waypoints.ToList();
    }
}

// A point on the map as a fraction of the image's width (X) and height (Y), so it
// stays in place at any display size.
public sealed class Waypoint
{
    public const int LabelMaxLength = 100;

    public double X { get; private set; }

    public double Y { get; private set; }

    public string Label { get; private set; } = "";

    private Waypoint()
    {
    }

    public static Waypoint Create(double? x, double? y, string? label)
    {
        if (x is not { } validX || !IsFraction(validX) || y is not { } validY || !IsFraction(validY))
        {
            throw new ArgumentException("Each waypoint needs an x and y from 0 to 1.", nameof(x));
        }

        var trimmedLabel = label?.Trim() ?? "";
        if (trimmedLabel.Length > LabelMaxLength)
        {
            throw new ArgumentException($"A waypoint label can be at most {LabelMaxLength} characters.", nameof(label));
        }

        return new Waypoint { X = validX, Y = validY, Label = trimmedLabel };
    }

    private static bool IsFraction(double value) => double.IsFinite(value) && value is >= 0 and <= 1;
}
