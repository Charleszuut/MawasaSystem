using MawasaProject.Domain.DTOs;

namespace MawasaProject.Application.Abstractions.Services;

public interface IReceiptService
{
    Task<string> GenerateReceiptAsync(ReceiptDto receipt, CancellationToken cancellationToken = default);
}

public interface IInvoiceService
{
    Task<string> GenerateInvoiceAsync(InvoiceDto invoice, CancellationToken cancellationToken = default);
}
