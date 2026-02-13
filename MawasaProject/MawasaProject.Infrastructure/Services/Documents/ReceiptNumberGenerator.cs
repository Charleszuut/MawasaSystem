namespace MawasaProject.Infrastructure.Services.Documents;

public sealed class ReceiptNumberGenerator
{
    private int _sequence;

    public string Next()
    {
        var index = Interlocked.Increment(ref _sequence);
        return $"RCPT-{DateTime.UtcNow:yyyyMMdd}-{index:D5}";
    }
}
