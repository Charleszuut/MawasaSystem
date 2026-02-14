-- Phase 6 audit hardening migration

CREATE INDEX IF NOT EXISTS IX_AuditLogs_EntityName ON AuditLogs(EntityName);
CREATE INDEX IF NOT EXISTS IX_AuditLogs_EntityId ON AuditLogs(EntityId);
CREATE INDEX IF NOT EXISTS IX_AuditLogs_Username ON AuditLogs(Username);
CREATE INDEX IF NOT EXISTS IX_AuditLogs_EntityName_TimestampUtc ON AuditLogs(EntityName, TimestampUtc);
