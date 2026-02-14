using System.Text.Json;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.DTOs;
using MawasaProject.Domain.Enums;
using MawasaProject.Infrastructure.Data.SQLite;

namespace MawasaProject.Infrastructure.Services.Documents;

public sealed class ReceiptService(
    ReceiptNumberGenerator numberGenerator,
    TemplateEngine templateEngine,
    LayoutRenderer layoutRenderer,
    DocumentArtifactWriter artifactWriter,
    ISqliteConnectionManager connectionManager,
    IAuditService auditService,
    IPrinterService printerService) : IReceiptService
{
    public async Task<DocumentGenerationResult> GenerateReceiptAsync(ReceiptDto receipt, CancellationToken cancellationToken = default)
    {
        Validate(receipt);

        var receiptNumber = string.IsNullOrWhiteSpace(receipt.ReceiptNumber)
            ? await numberGenerator.NextAsync(cancellationToken)
            : receipt.ReceiptNumber.Trim();

        var template = templateEngine.RenderReceipt(receipt, receiptNumber);
        var content = layoutRenderer.Render("Payment Receipt", template, branding: receipt.Branding);

        var dbDirectory = Path.GetDirectoryName(connectionManager.DatabasePath)!;
        var outputDirectory = Path.Combine(dbDirectory, "documents", "receipts");
        var artifacts = await artifactWriter.WriteAsync(
            outputDirectory,
            receiptNumber,
            content,
            new Dictionary<string, string?>
            {
                ["DocumentNumber"] = receiptNumber,
                ["CustomerName"] = receipt.CustomerName,
                ["BillNumber"] = receipt.BillNumber,
                ["PaidAmount"] = receipt.PaidAmount.ToString("F2"),
                ["PaymentDateUtc"] = receipt.PaymentDateUtc.ToString("O"),
                ["TemplateName"] = receipt.TemplateName,
                ["Branding"] = receipt.Branding
            },
            cancellationToken);

        var paymentId = receipt.PaymentId ?? Guid.Empty;
        var connection = await connectionManager.GetOpenConnectionAsync(cancellationToken);
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO Receipts
                    (Id, ReceiptNumber, PaymentId, FilePath, TemplateName, LayoutPath, ImagePath, CsvReferencePath, CreatedAtUtc, UpdatedAtUtc)
                VALUES
                    ($Id, $ReceiptNumber, $PaymentId, $FilePath, $TemplateName, $LayoutPath, $ImagePath, $CsvReferencePath, $CreatedAtUtc, $UpdatedAtUtc);
                """;
            command.Parameters.AddWithValue("$Id", Guid.NewGuid().ToString());
            command.Parameters.AddWithValue("$ReceiptNumber", receiptNumber);
            command.Parameters.AddWithValue("$PaymentId", paymentId.ToString());
            command.Parameters.AddWithValue("$FilePath", artifacts.PdfPath);
            command.Parameters.AddWithValue("$TemplateName", receipt.TemplateName);
            command.Parameters.AddWithValue("$LayoutPath", artifacts.LayoutPath);
            command.Parameters.AddWithValue("$ImagePath", artifacts.ImagePath);
            command.Parameters.AddWithValue("$CsvReferencePath", artifacts.CsvReferencePath);
            command.Parameters.AddWithValue("$CreatedAtUtc", DateTime.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$UpdatedAtUtc", DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await connectionManager.DisposeConnectionIfNeededAsync(connection);
        }

        Guid? printJobId = null;
        if (receipt.AutoPrint)
        {
            printJobId = await printerService.EnqueueAsync(new PrintRequest
            {
                TemplateName = "Receipt-" + receipt.TemplateName,
                Content = content,
                MaxRetries = 2,
                PaperSize = "A4"
            }, cancellationToken);
        }

        await auditService.LogAsync(
            AuditActionType.Export,
            "Receipt",
            receiptNumber,
            oldValuesJson: null,
            newValuesJson: JsonSerializer.Serialize(new
            {
                PdfPath = artifacts.PdfPath,
                artifacts.LayoutPath,
                artifacts.ImagePath,
                artifacts.CsvReferencePath,
                printJobId,
                receipt.TemplateName
            }),
            context: "Receipt generated",
            username: null,
            cancellationToken);

        return new DocumentGenerationResult
        {
            DocumentNumber = receiptNumber,
            PdfPath = artifacts.PdfPath,
            LayoutPath = artifacts.LayoutPath,
            ImagePath = artifacts.ImagePath,
            CsvReferencePath = artifacts.CsvReferencePath,
            PrintJobId = printJobId
        };
    }

    private static void Validate(ReceiptDto receipt)
    {
        if (string.IsNullOrWhiteSpace(receipt.CustomerName))
        {
            throw new InvalidOperationException("Customer name is required.");
        }

        if (string.IsNullOrWhiteSpace(receipt.BillNumber))
        {
            throw new InvalidOperationException("Bill number is required.");
        }

        if (receipt.PaidAmount <= 0m)
        {
            throw new InvalidOperationException("Paid amount must be greater than zero.");
        }
    }
}
