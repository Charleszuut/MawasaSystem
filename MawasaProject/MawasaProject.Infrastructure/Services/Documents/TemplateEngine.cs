using MawasaProject.Domain.DTOs;

namespace MawasaProject.Infrastructure.Services.Documents;

public sealed class TemplateEngine
{
    public string RenderReceipt(ReceiptDto model, string receiptNumber)
    {
        var lines = new List<string>
        {
            $"Template: {model.TemplateName}",
            $"Receipt No: {receiptNumber}",
            $"Customer: {model.CustomerName}",
            $"Bill: {model.BillNumber}",
            $"Paid: {model.PaidAmount:F2}",
            $"Date: {model.PaymentDateUtc:yyyy-MM-dd HH:mm:ss} UTC"
        };

        if (string.Equals(model.TemplateName, "compact", StringComparison.OrdinalIgnoreCase))
        {
            lines.Add("Format: compact");
        }
        else
        {
            lines.Add("Format: standard");
            lines.Add("Thank you for your payment.");
        }

        if (!string.IsNullOrWhiteSpace(model.QrPayload))
        {
            lines.Add($"QR: {model.QrPayload}");
        }

        lines.Add($"BARCODE: {receiptNumber}");
        return string.Join('\n', lines) + '\n';
    }

    public string RenderInvoice(InvoiceDto model, string invoiceNumber)
    {
        var lines = new List<string>
        {
            $"Template: {model.TemplateName}",
            $"Invoice No: {invoiceNumber}",
            $"Customer: {model.CustomerName}",
            $"Bill: {model.BillNumber}",
            $"Total: {model.TotalAmount:F2}",
            $"Due Date: {model.DueDateUtc:yyyy-MM-dd}"
        };

        if (string.Equals(model.TemplateName, "compact", StringComparison.OrdinalIgnoreCase))
        {
            lines.Add("Format: compact");
        }
        else
        {
            lines.Add("Format: standard");
            lines.Add("Payment terms: due by listed due date.");
        }

        if (!string.IsNullOrWhiteSpace(model.QrPayload))
        {
            lines.Add($"QR: {model.QrPayload}");
        }

        lines.Add($"BARCODE: {invoiceNumber}");
        return string.Join('\n', lines) + '\n';
    }
}
