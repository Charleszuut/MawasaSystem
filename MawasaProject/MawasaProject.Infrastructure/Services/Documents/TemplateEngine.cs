using MawasaProject.Domain.DTOs;

namespace MawasaProject.Infrastructure.Services.Documents;

public sealed class TemplateEngine
{
    public string RenderReceipt(ReceiptDto model, string receiptNumber)
    {
        return $"Receipt No: {receiptNumber}\n" +
               $"Customer: {model.CustomerName}\n" +
               $"Bill: {model.BillNumber}\n" +
               $"Paid: {model.PaidAmount:F2}\n" +
               $"Date: {model.PaymentDateUtc:yyyy-MM-dd HH:mm:ss} UTC\n";
    }

    public string RenderInvoice(InvoiceDto model, string invoiceNumber)
    {
        return $"Invoice No: {invoiceNumber}\n" +
               $"Customer: {model.CustomerName}\n" +
               $"Bill: {model.BillNumber}\n" +
               $"Total: {model.TotalAmount:F2}\n" +
               $"Due Date: {model.DueDateUtc:yyyy-MM-dd}\n";
    }
}
