using System.Text.Json;
using MawasaProject.Application.Abstractions.Services;
using MawasaProject.Domain.DTOs;
using MawasaProject.Domain.Enums;
using MawasaProject.Infrastructure.Data.SQLite;

namespace MawasaProject.Infrastructure.Services.Documents;

public sealed class InvoiceService(
    InvoiceNumberGenerator numberGenerator,
    TemplateEngine templateEngine,
    LayoutRenderer layoutRenderer,
    DocumentArtifactWriter artifactWriter,
    ISqliteConnectionManager connectionManager,
    IAuditService auditService,
    IPrinterService printerService) : IInvoiceService
{
    public async Task<DocumentGenerationResult> GenerateInvoiceAsync(InvoiceDto invoice, CancellationToken cancellationToken = default)
    {
        Validate(invoice);

        var invoiceNumber = string.IsNullOrWhiteSpace(invoice.InvoiceNumber)
            ? await numberGenerator.NextAsync(cancellationToken)
            : invoice.InvoiceNumber.Trim();

        var template = templateEngine.RenderInvoice(invoice, invoiceNumber);
        var content = layoutRenderer.Render("Invoice", template, branding: invoice.Branding);

        var dbDirectory = Path.GetDirectoryName(connectionManager.DatabasePath)!;
        var outputDirectory = Path.Combine(dbDirectory, "documents", "invoices");
        var artifacts = await artifactWriter.WriteAsync(
            outputDirectory,
            invoiceNumber,
            content,
            new Dictionary<string, string?>
            {
                ["DocumentNumber"] = invoiceNumber,
                ["CustomerName"] = invoice.CustomerName,
                ["BillNumber"] = invoice.BillNumber,
                ["TotalAmount"] = invoice.TotalAmount.ToString("F2"),
                ["DueDateUtc"] = invoice.DueDateUtc.ToString("O"),
                ["TemplateName"] = invoice.TemplateName,
                ["Branding"] = invoice.Branding
            },
            cancellationToken);

        var billId = invoice.BillId ?? Guid.Empty;
        var connection = await connectionManager.GetOpenConnectionAsync(cancellationToken);
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO Invoices
                    (Id, InvoiceNumber, BillId, FilePath, TemplateName, LayoutPath, ImagePath, CsvReferencePath, CreatedAtUtc, UpdatedAtUtc)
                VALUES
                    ($Id, $InvoiceNumber, $BillId, $FilePath, $TemplateName, $LayoutPath, $ImagePath, $CsvReferencePath, $CreatedAtUtc, $UpdatedAtUtc);
                """;
            command.Parameters.AddWithValue("$Id", Guid.NewGuid().ToString());
            command.Parameters.AddWithValue("$InvoiceNumber", invoiceNumber);
            command.Parameters.AddWithValue("$BillId", billId.ToString());
            command.Parameters.AddWithValue("$FilePath", artifacts.PdfPath);
            command.Parameters.AddWithValue("$TemplateName", invoice.TemplateName);
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
        if (invoice.AutoPrint)
        {
            printJobId = await printerService.EnqueueAsync(new PrintRequest
            {
                TemplateName = "Invoice-" + invoice.TemplateName,
                Content = content,
                MaxRetries = 2,
                PaperSize = "A4"
            }, cancellationToken);
        }

        await auditService.LogAsync(
            AuditActionType.Export,
            "Invoice",
            invoiceNumber,
            oldValuesJson: null,
            newValuesJson: JsonSerializer.Serialize(new
            {
                PdfPath = artifacts.PdfPath,
                artifacts.LayoutPath,
                artifacts.ImagePath,
                artifacts.CsvReferencePath,
                printJobId,
                invoice.TemplateName
            }),
            context: "Invoice generated",
            username: null,
            cancellationToken);

        return new DocumentGenerationResult
        {
            DocumentNumber = invoiceNumber,
            PdfPath = artifacts.PdfPath,
            LayoutPath = artifacts.LayoutPath,
            ImagePath = artifacts.ImagePath,
            CsvReferencePath = artifacts.CsvReferencePath,
            PrintJobId = printJobId
        };
    }

    private static void Validate(InvoiceDto invoice)
    {
        if (string.IsNullOrWhiteSpace(invoice.CustomerName))
        {
            throw new InvalidOperationException("Customer name is required.");
        }

        if (string.IsNullOrWhiteSpace(invoice.BillNumber))
        {
            throw new InvalidOperationException("Bill number is required.");
        }

        if (invoice.TotalAmount <= 0m)
        {
            throw new InvalidOperationException("Total amount must be greater than zero.");
        }
    }
}
