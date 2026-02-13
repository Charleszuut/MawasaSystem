namespace MawasaProject.Domain.ValueObjects;

public readonly record struct DateRange
{
    public DateTime StartUtc { get; }
    public DateTime EndUtc { get; }

    public DateRange(DateTime startUtc, DateTime endUtc)
    {
        if (endUtc < startUtc)
        {
            throw new ArgumentException("End date cannot be before start date.", nameof(endUtc));
        }

        StartUtc = startUtc;
        EndUtc = endUtc;
    }

    public bool Contains(DateTime value)
    {
        return value >= StartUtc && value <= EndUtc;
    }
}
