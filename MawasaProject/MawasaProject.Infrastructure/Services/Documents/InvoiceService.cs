using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.DTOs;
using MawasaProject.Domain.Enums;
using MawasaProject.Infrastructure.Data.SQLite;

namespace MawasaProject.Infrastructure.Services.Documents;

public sealed class InvoiceService(
    InvoiceNumberGenerator numberGenerator,
    TemplateEngine templateEngine,
    LayoutRenderer layoutRenderer,
    PdfGenerator pdfGenerator,
    ISqliteConnectionManager connectionManager,
    IAuditService auditService,
    IPrinterService printerService) : IInvoiceService
{
    public async Task<string> GenerateInvoiceAsync(InvoiceDto invoice, CancellationToken cancellationToken = default)
    {
        var invoiceNumber = numberGenerator.Next();
        var template = templateEngine.RenderInvoice(invoice, invoiceNumber);
        var content = layoutRenderer.Render("Invoice", template, branding: "Mawasa Project");

        var dbDirectory = Path.GetDirectoryName(connectionManager.DatabasePath)!;
        var outputDirectory = Path.Combine(dbDirectory, "documents", "invoices");
        var filePath = await pdfGenerator.SaveAsPdfAsync(outputDirectory, invoiceNumber + ".pdf", content, cancellationToken);

        var connection = await connectionManager.GetOpenConnectionAsync(cancellationToken);
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO Invoices (Id, InvoiceNumber, BillId, FilePath, CreatedAtUtc, UpdatedAtUtc) VALUES ($Id, $InvoiceNumber, $BillId, $FilePath, $CreatedAtUtc, $UpdatedAtUtc);";
            command.Parameters.AddWithValue("$Id", Guid.NewGuid().ToString());
            command.Parameters.AddWithValue("$InvoiceNumber", invoiceNumber);
            command.Parameters.AddWithValue("$BillId", Guid.Empty.ToString());
            command.Parameters.AddWithValue("$FilePath", filePath);
            command.Parameters.AddWithValue("$CreatedAtUtc", DateTime.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$UpdatedAtUtc", DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await connectionManager.DisposeConnectionIfNeededAsync(connection);
        }

        await auditService.LogAsync(
            AuditActionType.Export,
            "Invoice",
            invoiceNumber,
            oldValuesJson: null,
            newValuesJson: $"{{\"FilePath\":\"{filePath}\"}}",
            context: "Invoice generated",
            username: null,
            cancellationToken);

        await printerService.EnqueueAsync(new PrintRequest
        {
            TemplateName = "Invoice",
            Content = content,
            RetryCount = 2
        }, cancellationToken);

        return filePath;
    }
}
