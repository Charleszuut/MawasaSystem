namespace MawasaProject.Infrastructure.Services.Documents;

public sealed class ReceiptNumberGenerator(DocumentNumberService numberService)
{
    public Task<string> NextAsync(CancellationToken cancellationToken = default)
    {
        return numberService.NextAsync("Receipt", "RCPT", cancellationToken);
    }
}
