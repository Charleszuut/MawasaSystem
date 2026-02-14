-- Phase 13 printer integration hardening migration

ALTER TABLE PrintJobs ADD COLUMN ProfileName TEXT NULL;
ALTER TABLE PrintJobs ADD COLUMN PaperSize TEXT NOT NULL DEFAULT 'A4';
ALTER TABLE PrintJobs ADD COLUMN MaxRetries INTEGER NOT NULL DEFAULT 3;
ALTER TABLE PrintJobs ADD COLUMN LastError TEXT NULL;
ALTER TABLE PrintJobs ADD COLUMN LastTriedAtUtc TEXT NULL;

ALTER TABLE PrinterProfiles ADD COLUMN IsActive INTEGER NOT NULL DEFAULT 1;

CREATE INDEX IF NOT EXISTS IX_PrintJobs_Status_CreatedAtUtc ON PrintJobs(Status, CreatedAtUtc);
CREATE INDEX IF NOT EXISTS IX_PrintJobs_PrinterName ON PrintJobs(PrinterName);
CREATE INDEX IF NOT EXISTS IX_PrintLogs_LoggedAtUtc ON PrintLogs(LoggedAtUtc);
CREATE INDEX IF NOT EXISTS IX_PrinterProfiles_IsDefault ON PrinterProfiles(IsDefault);
