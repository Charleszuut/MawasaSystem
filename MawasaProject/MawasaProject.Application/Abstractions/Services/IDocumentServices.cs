using MawasaProject.Domain.DTOs;

namespace MawasaProject.Application.Abstractions.Services;

public interface IReceiptService
{
    Task<DocumentGenerationResult> GenerateReceiptAsync(ReceiptDto receipt, CancellationToken cancellationToken = default);
}

public interface IInvoiceService
{
    Task<DocumentGenerationResult> GenerateInvoiceAsync(InvoiceDto invoice, CancellationToken cancellationToken = default);
}
