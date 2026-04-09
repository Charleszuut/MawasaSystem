-- Migration 011: Add IsPrinted / PrintedAtUtc columns to Bills and Billing tables.
-- Also fixes the overly strict CHECK(Balance <= Amount) constraint on the Bills table
-- (the Billing mirror already had this fixed in migration 009).
--
-- SQLite cannot ALTER TABLE to drop/modify CHECK constraints, so we must recreate the Bills table.

PRAGMA foreign_keys = OFF;

-- -----------------------------------------------------------------------
-- Step 1: Drop triggers that depend on the Bills table
-- -----------------------------------------------------------------------
DROP TRIGGER IF EXISTS trg_Bills_Insert_Billing;
DROP TRIGGER IF EXISTS trg_Bills_Update_Billing;
DROP TRIGGER IF EXISTS trg_Bills_Delete_Billing;

-- -----------------------------------------------------------------------
-- Step 2: Rename existing Bills table
-- -----------------------------------------------------------------------
ALTER TABLE Bills RENAME TO Bills_old;

-- -----------------------------------------------------------------------
-- Step 3: Recreate Bills with relaxed Balance constraint + new columns
-- -----------------------------------------------------------------------
CREATE TABLE Bills (
    Id TEXT PRIMARY KEY,
    CustomerId TEXT NOT NULL,
    BillNumber TEXT NOT NULL UNIQUE,
    Amount REAL NOT NULL CHECK(Amount >= 0),
    Balance REAL NOT NULL CHECK(Balance >= 0),
    DueDateUtc TEXT NOT NULL,
    PaidAtUtc TEXT NULL,
    Status INTEGER NOT NULL CHECK(Status IN (1,2,3)),
    CreatedByUserId TEXT NOT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NULL,
    IsDeleted INTEGER NOT NULL DEFAULT 0 CHECK(IsDeleted IN (0,1)),
    DeletedAtUtc TEXT NULL,
    IsPrinted INTEGER NOT NULL DEFAULT 0 CHECK(IsPrinted IN (0,1)),
    PrintedAtUtc TEXT NULL,
    CONSTRAINT FK_Bills_Customer FOREIGN KEY (CustomerId) REFERENCES Customers(Id) ON DELETE RESTRICT
);

-- -----------------------------------------------------------------------
-- Step 4: Copy existing data
-- -----------------------------------------------------------------------
INSERT INTO Bills (Id, CustomerId, BillNumber, Amount, Balance, DueDateUtc, PaidAtUtc, Status, CreatedByUserId, CreatedAtUtc, UpdatedAtUtc, IsDeleted, DeletedAtUtc, IsPrinted, PrintedAtUtc)
SELECT Id, CustomerId, BillNumber, Amount, Balance, DueDateUtc, PaidAtUtc, Status, CreatedByUserId, CreatedAtUtc, UpdatedAtUtc, IsDeleted, DeletedAtUtc, 0, NULL
FROM Bills_old;

-- -----------------------------------------------------------------------
-- Step 5: Recreate child-table FK constraints (SQLite handles via child CREATE)
-- BillStatusHistory and Payments already have their own FK references to Bills(Id).
-- SQLite resolves them by name so no DDL change is needed for child tables.
-- -----------------------------------------------------------------------

-- -----------------------------------------------------------------------
-- Step 6: Drop old Bills table
-- -----------------------------------------------------------------------
DROP TABLE Bills_old;

-- -----------------------------------------------------------------------
-- Step 7: Recreate indexes on the new Bills table
-- -----------------------------------------------------------------------
CREATE INDEX IF NOT EXISTS IX_Bills_CustomerId ON Bills(CustomerId);
CREATE INDEX IF NOT EXISTS IX_Bills_DueDateUtc ON Bills(DueDateUtc);
CREATE INDEX IF NOT EXISTS IX_Bills_Status ON Bills(Status);
CREATE INDEX IF NOT EXISTS IX_Bills_IsDeleted ON Bills(IsDeleted);
CREATE INDEX IF NOT EXISTS IX_Bills_IsPrinted ON Bills(IsPrinted);

-- -----------------------------------------------------------------------
-- Step 8: Add IsPrinted / PrintedAtUtc to the Billing mirror table
-- (use ALTER TABLE — no constraint change needed on Billing since 009 already relaxed it)
-- -----------------------------------------------------------------------
ALTER TABLE Billing ADD COLUMN IsPrinted INTEGER NOT NULL DEFAULT 0 CHECK(IsPrinted IN (0,1));
ALTER TABLE Billing ADD COLUMN PrintedAtUtc TEXT NULL;

-- Back-fill mirror: mark as printed if Bill was already Paid (backward compat)
UPDATE Billing SET IsPrinted = 1 WHERE Status = 2 AND IsPrinted = 0;

-- -----------------------------------------------------------------------
-- Step 9: Recreate synchronisation triggers (including new columns)
-- -----------------------------------------------------------------------
CREATE TRIGGER IF NOT EXISTS trg_Bills_Insert_Billing
AFTER INSERT ON Bills
BEGIN
    INSERT OR REPLACE INTO Billing (Id, CustomerId, BillNumber, Amount, Balance, DueDateUtc, PaidAtUtc, Status, CreatedByUserId, CreatedAtUtc, UpdatedAtUtc, IsDeleted, DeletedAtUtc, IsPrinted, PrintedAtUtc)
    VALUES (NEW.Id, NEW.CustomerId, NEW.BillNumber, NEW.Amount, NEW.Balance, NEW.DueDateUtc, NEW.PaidAtUtc, NEW.Status, NEW.CreatedByUserId, NEW.CreatedAtUtc, NEW.UpdatedAtUtc, NEW.IsDeleted, NEW.DeletedAtUtc, NEW.IsPrinted, NEW.PrintedAtUtc);
END;

CREATE TRIGGER IF NOT EXISTS trg_Bills_Update_Billing
AFTER UPDATE ON Bills
BEGIN
    INSERT OR REPLACE INTO Billing (Id, CustomerId, BillNumber, Amount, Balance, DueDateUtc, PaidAtUtc, Status, CreatedByUserId, CreatedAtUtc, UpdatedAtUtc, IsDeleted, DeletedAtUtc, IsPrinted, PrintedAtUtc)
    VALUES (NEW.Id, NEW.CustomerId, NEW.BillNumber, NEW.Amount, NEW.Balance, NEW.DueDateUtc, NEW.PaidAtUtc, NEW.Status, NEW.CreatedByUserId, NEW.CreatedAtUtc, NEW.UpdatedAtUtc, NEW.IsDeleted, NEW.DeletedAtUtc, NEW.IsPrinted, NEW.PrintedAtUtc);
END;

CREATE TRIGGER IF NOT EXISTS trg_Bills_Delete_Billing
AFTER DELETE ON Bills
BEGIN
    DELETE FROM Billing WHERE Id = OLD.Id;
END;

PRAGMA foreign_keys = ON;
