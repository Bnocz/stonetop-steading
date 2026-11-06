namespace Api.Domain;

public sealed class LogEntry
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid SteadingId { get; private set; }

    public SteadingStat Stat { get; private set; }

    public int OldValue { get; private set; }

    public int NewValue { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    private LogEntry()
    {
    }

    public LogEntry(Guid steadingId, SteadingStat stat, int oldValue, int newValue, DateTimeOffset changedAt)
    {
        SteadingId = steadingId;
        Stat = stat;
        OldValue = oldValue;
        NewValue = newValue;
        ChangedAt = changedAt;
    }
}
