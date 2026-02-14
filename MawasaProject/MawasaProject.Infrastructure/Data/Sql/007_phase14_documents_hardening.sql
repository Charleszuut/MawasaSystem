-- Phase 14 receipt and invoice hardening migration

CREATE TABLE IF NOT EXISTS DocumentSequences (
    Name TEXT PRIMARY KEY,
    CurrentValue INTEGER NOT NULL,
    UpdatedAtUtc TEXT NOT NULL
);

ALTER TABLE Receipts ADD COLUMN TemplateName TEXT NULL;
ALTER TABLE Receipts ADD COLUMN LayoutPath TEXT NULL;
ALTER TABLE Receipts ADD COLUMN ImagePath TEXT NULL;
ALTER TABLE Receipts ADD COLUMN CsvReferencePath TEXT NULL;

ALTER TABLE Invoices ADD COLUMN TemplateName TEXT NULL;
ALTER TABLE Invoices ADD COLUMN LayoutPath TEXT NULL;
ALTER TABLE Invoices ADD COLUMN ImagePath TEXT NULL;
ALTER TABLE Invoices ADD COLUMN CsvReferencePath TEXT NULL;

CREATE INDEX IF NOT EXISTS IX_DocumentSequences_UpdatedAtUtc ON DocumentSequences(UpdatedAtUtc);
CREATE INDEX IF NOT EXISTS IX_Receipts_PaymentId ON Receipts(PaymentId);
CREATE INDEX IF NOT EXISTS IX_Invoices_BillId ON Invoices(BillId);
