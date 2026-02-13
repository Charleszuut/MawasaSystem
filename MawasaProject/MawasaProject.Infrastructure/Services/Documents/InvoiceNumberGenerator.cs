namespace MawasaProject.Infrastructure.Services.Documents;

public sealed class InvoiceNumberGenerator
{
    private int _sequence;

    public string Next()
    {
        var index = Interlocked.Increment(ref _sequence);
        return $"INV-{DateTime.UtcNow:yyyyMMdd}-{index:D5}";
    }
}
