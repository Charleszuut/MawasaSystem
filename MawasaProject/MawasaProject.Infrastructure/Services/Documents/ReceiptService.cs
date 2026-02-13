using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.DTOs;
using MawasaProject.Domain.Enums;
using MawasaProject.Infrastructure.Data.SQLite;

namespace MawasaProject.Infrastructure.Services.Documents;

public sealed class ReceiptService(
    ReceiptNumberGenerator numberGenerator,
    TemplateEngine templateEngine,
    LayoutRenderer layoutRenderer,
    PdfGenerator pdfGenerator,
    ISqliteConnectionManager connectionManager,
    IAuditService auditService,
    IPrinterService printerService) : IReceiptService
{
    public async Task<string> GenerateReceiptAsync(ReceiptDto receipt, CancellationToken cancellationToken = default)
    {
        var receiptNumber = numberGenerator.Next();
        var template = templateEngine.RenderReceipt(receipt, receiptNumber);
        var content = layoutRenderer.Render("Payment Receipt", template, branding: "Mawasa Project");

        var dbDirectory = Path.GetDirectoryName(connectionManager.DatabasePath)!;
        var outputDirectory = Path.Combine(dbDirectory, "documents", "receipts");
        var filePath = await pdfGenerator.SaveAsPdfAsync(outputDirectory, receiptNumber + ".pdf", content, cancellationToken);

        var connection = await connectionManager.GetOpenConnectionAsync(cancellationToken);
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO Receipts (Id, ReceiptNumber, PaymentId, FilePath, CreatedAtUtc, UpdatedAtUtc) VALUES ($Id, $ReceiptNumber, $PaymentId, $FilePath, $CreatedAtUtc, $UpdatedAtUtc);";
            command.Parameters.AddWithValue("$Id", Guid.NewGuid().ToString());
            command.Parameters.AddWithValue("$ReceiptNumber", receiptNumber);
            command.Parameters.AddWithValue("$PaymentId", Guid.Empty.ToString());
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
            "Receipt",
            receiptNumber,
            oldValuesJson: null,
            newValuesJson: $"{{\"FilePath\":\"{filePath}\"}}",
            context: "Receipt generated",
            username: null,
            cancellationToken);

        await printerService.EnqueueAsync(new PrintRequest
        {
            TemplateName = "Receipt",
            Content = content,
            RetryCount = 2
        }, cancellationToken);

        return filePath;
    }
}
