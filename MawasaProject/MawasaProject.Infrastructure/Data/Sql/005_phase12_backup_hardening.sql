-- Phase 12 backup and restore hardening migration

ALTER TABLE BackupHistory ADD COLUMN IsAutomatic INTEGER NOT NULL DEFAULT 0;
ALTER TABLE BackupHistory ADD COLUMN IsEncrypted INTEGER NOT NULL DEFAULT 0;
ALTER TABLE BackupHistory ADD COLUMN IntegrityVerifiedAtUtc TEXT NULL;
ALTER TABLE BackupHistory ADD COLUMN Notes TEXT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS IX_BackupHistory_FilePath ON BackupHistory(FilePath);
CREATE INDEX IF NOT EXISTS IX_BackupHistory_IsAutomatic ON BackupHistory(IsAutomatic);
