PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS BackupHistory (
    Id TEXT PRIMARY KEY,
    FileName TEXT NOT NULL,
    FilePath TEXT NOT NULL,
    Hash TEXT NOT NULL,
    SizeBytes INTEGER NOT NULL,
    Version TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NULL
);

CREATE TABLE IF NOT EXISTS PrintJobs (
    Id TEXT PRIMARY KEY,
    TemplateName TEXT NOT NULL,
    PrinterName TEXT NOT NULL,
    Payload TEXT NOT NULL,
    RetryCount INTEGER NOT NULL DEFAULT 0,
    Status TEXT NOT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NULL
);

CREATE TABLE IF NOT EXISTS PrinterProfiles (
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    DeviceName TEXT NOT NULL,
    PaperSize TEXT NOT NULL,
    IsDefault INTEGER NOT NULL DEFAULT 0,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NULL
);

CREATE TABLE IF NOT EXISTS PrintLogs (
    Id TEXT PRIMARY KEY,
    PrintJobId TEXT NOT NULL,
    Status TEXT NOT NULL,
    Error TEXT NULL,
    LoggedAtUtc TEXT NOT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NULL,
    CONSTRAINT FK_PrintLogs_PrintJobs FOREIGN KEY (PrintJobId) REFERENCES PrintJobs(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS Receipts (
    Id TEXT PRIMARY KEY,
    ReceiptNumber TEXT NOT NULL UNIQUE,
    PaymentId TEXT NOT NULL,
    FilePath TEXT NOT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NULL
);

CREATE TABLE IF NOT EXISTS Invoices (
    Id TEXT PRIMARY KEY,
    InvoiceNumber TEXT NOT NULL UNIQUE,
    BillId TEXT NOT NULL,
    FilePath TEXT NOT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NULL
);

CREATE INDEX IF NOT EXISTS IX_BackupHistory_CreatedAtUtc ON BackupHistory(CreatedAtUtc);
CREATE INDEX IF NOT EXISTS IX_PrintJobs_Status ON PrintJobs(Status);
CREATE INDEX IF NOT EXISTS IX_PrintLogs_PrintJobId ON PrintLogs(PrintJobId);
CREATE INDEX IF NOT EXISTS IX_Receipts_ReceiptNumber ON Receipts(ReceiptNumber);
CREATE INDEX IF NOT EXISTS IX_Invoices_InvoiceNumber ON Invoices(InvoiceNumber);
