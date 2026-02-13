PRAGMA foreign_keys = ON;

-- Phase 1 hardening migration
-- 1) Add query-optimized partial indexes for active (not deleted) records.
CREATE INDEX IF NOT EXISTS IX_Users_Active_NotDeleted ON Users(IsActive) WHERE IsDeleted = 0;
CREATE INDEX IF NOT EXISTS IX_Users_LastLoginAtUtc ON Users(LastLoginAtUtc);
CREATE INDEX IF NOT EXISTS IX_Roles_Name_NotDeleted ON Roles(Name) WHERE IsDeleted = 0;
CREATE INDEX IF NOT EXISTS IX_Customers_Email_NotDeleted ON Customers(Email) WHERE IsDeleted = 0 AND Email IS NOT NULL;
CREATE INDEX IF NOT EXISTS IX_Bills_Status_DueDate_NotDeleted ON Bills(Status, DueDateUtc) WHERE IsDeleted = 0;
CREATE INDEX IF NOT EXISTS IX_Bills_Customer_Status_NotDeleted ON Bills(CustomerId, Status) WHERE IsDeleted = 0;
CREATE INDEX IF NOT EXISTS IX_Payments_Status_PaymentDateUtc ON Payments(Status, PaymentDateUtc);
CREATE INDEX IF NOT EXISTS IX_UserRoles_UserId ON UserRoles(UserId);
CREATE INDEX IF NOT EXISTS IX_UserRoles_RoleId ON UserRoles(RoleId);

-- 2) Data normalization safety updates.
UPDATE Users SET IsDeleted = 0 WHERE IsDeleted NOT IN (0, 1) OR IsDeleted IS NULL;
UPDATE Roles SET IsDeleted = 0 WHERE IsDeleted NOT IN (0, 1) OR IsDeleted IS NULL;
UPDATE Customers SET IsDeleted = 0 WHERE IsDeleted NOT IN (0, 1) OR IsDeleted IS NULL;
UPDATE Bills SET IsDeleted = 0 WHERE IsDeleted NOT IN (0, 1) OR IsDeleted IS NULL;
UPDATE ReportsCache SET IsDeleted = 0 WHERE IsDeleted NOT IN (0, 1) OR IsDeleted IS NULL;

-- 3) Soft-delete consistency guards.
CREATE TRIGGER IF NOT EXISTS TRG_Users_SoftDelete_Consistency
BEFORE UPDATE OF IsDeleted, DeletedAtUtc ON Users
FOR EACH ROW
WHEN NEW.IsDeleted = 1 AND NEW.DeletedAtUtc IS NULL
BEGIN
    SELECT RAISE(ABORT, 'Users.DeletedAtUtc is required when IsDeleted=1');
END;

CREATE TRIGGER IF NOT EXISTS TRG_Roles_SoftDelete_Consistency
BEFORE UPDATE OF IsDeleted, DeletedAtUtc ON Roles
FOR EACH ROW
WHEN NEW.IsDeleted = 1 AND NEW.DeletedAtUtc IS NULL
BEGIN
    SELECT RAISE(ABORT, 'Roles.DeletedAtUtc is required when IsDeleted=1');
END;

CREATE TRIGGER IF NOT EXISTS TRG_Customers_SoftDelete_Consistency
BEFORE UPDATE OF IsDeleted, DeletedAtUtc ON Customers
FOR EACH ROW
WHEN NEW.IsDeleted = 1 AND NEW.DeletedAtUtc IS NULL
BEGIN
    SELECT RAISE(ABORT, 'Customers.DeletedAtUtc is required when IsDeleted=1');
END;

CREATE TRIGGER IF NOT EXISTS TRG_Bills_SoftDelete_Consistency
BEFORE UPDATE OF IsDeleted, DeletedAtUtc ON Bills
FOR EACH ROW
WHEN NEW.IsDeleted = 1 AND NEW.DeletedAtUtc IS NULL
BEGIN
    SELECT RAISE(ABORT, 'Bills.DeletedAtUtc is required when IsDeleted=1');
END;

CREATE TRIGGER IF NOT EXISTS TRG_ReportsCache_SoftDelete_Consistency
BEFORE UPDATE OF IsDeleted, DeletedAtUtc ON ReportsCache
FOR EACH ROW
WHEN NEW.IsDeleted = 1 AND NEW.DeletedAtUtc IS NULL
BEGIN
    SELECT RAISE(ABORT, 'ReportsCache.DeletedAtUtc is required when IsDeleted=1');
END;

-- 4) Domain integrity guards for billing/payment lifecycle.
CREATE TRIGGER IF NOT EXISTS TRG_Bills_PaidStatus_RequiresZeroBalance
BEFORE UPDATE OF Status, Balance ON Bills
FOR EACH ROW
WHEN NEW.Status = 2 AND NEW.Balance <> 0
BEGIN
    SELECT RAISE(ABORT, 'Paid bill must have zero balance');
END;

CREATE TRIGGER IF NOT EXISTS TRG_Bills_NonPaidStatus_CannotHaveNegativeBalance
BEFORE UPDATE OF Status, Balance ON Bills
FOR EACH ROW
WHEN NEW.Status <> 2 AND NEW.Balance < 0
BEGIN
    SELECT RAISE(ABORT, 'Bill balance cannot be negative');
END;

CREATE TRIGGER IF NOT EXISTS TRG_Payments_Completed_MustHaveReference
BEFORE INSERT ON Payments
FOR EACH ROW
WHEN NEW.Status = 2 AND (NEW.ReferenceNumber IS NULL OR length(trim(NEW.ReferenceNumber)) = 0)
BEGIN
    SELECT RAISE(ABORT, 'Completed payment requires a reference number');
END;

CREATE TRIGGER IF NOT EXISTS TRG_Payments_Completed_MustHaveReference_OnUpdate
BEFORE UPDATE OF Status, ReferenceNumber ON Payments
FOR EACH ROW
WHEN NEW.Status = 2 AND (NEW.ReferenceNumber IS NULL OR length(trim(NEW.ReferenceNumber)) = 0)
BEGIN
    SELECT RAISE(ABORT, 'Completed payment requires a reference number');
END;
