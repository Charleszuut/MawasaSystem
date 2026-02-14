namespace MawasaProject.Infrastructure.Services.Documents;

public sealed class InvoiceNumberGenerator(DocumentNumberService numberService)
{
    public Task<string> NextAsync(CancellationToken cancellationToken = default)
    {
        return numberService.NextAsync("Invoice", "INV", cancellationToken);
    }
}
