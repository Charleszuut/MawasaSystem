-- Phase 15: Relax Billing mirror table Balance constraint
-- SQLite does not support ALTER TABLE to modify CHECK constraints,
-- so we must recreate the table with the relaxed constraint.

-- Step 1: Drop existing triggers
DROP TRIGGER IF EXISTS trg_Bills_Insert_Billing;
DROP TRIGGER IF EXISTS trg_Bills_Update_Billing;
DROP TRIGGER IF EXISTS trg_Bills_Delete_Billing;

-- Step 2: Rename old table
ALTER TABLE Billing RENAME TO Billing_old;

-- Step 3: Create new table with relaxed constraint
CREATE TABLE Billing (
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
    CONSTRAINT FK_Billing_Customer FOREIGN KEY (CustomerId) REFERENCES Customers(Id) ON DELETE RESTRICT
);

-- Step 4: Copy data from old table
INSERT INTO Billing (Id, CustomerId, BillNumber, Amount, Balance, DueDateUtc, PaidAtUtc, Status, CreatedByUserId, CreatedAtUtc, UpdatedAtUtc, IsDeleted, DeletedAtUtc)
SELECT Id, CustomerId, BillNumber, Amount, Balance, DueDateUtc, PaidAtUtc, Status, CreatedByUserId, CreatedAtUtc, UpdatedAtUtc, IsDeleted, DeletedAtUtc
FROM Billing_old;

-- Step 5: Drop old table
DROP TABLE Billing_old;

-- Step 6: Recreate triggers
CREATE TRIGGER IF NOT EXISTS trg_Bills_Insert_Billing
AFTER INSERT ON Bills
BEGIN
    INSERT OR REPLACE INTO Billing (Id, CustomerId, BillNumber, Amount, Balance, DueDateUtc, PaidAtUtc, Status, CreatedByUserId, CreatedAtUtc, UpdatedAtUtc, IsDeleted, DeletedAtUtc)
    VALUES (NEW.Id, NEW.CustomerId, NEW.BillNumber, NEW.Amount, NEW.Balance, NEW.DueDateUtc, NEW.PaidAtUtc, NEW.Status, NEW.CreatedByUserId, NEW.CreatedAtUtc, NEW.UpdatedAtUtc, NEW.IsDeleted, NEW.DeletedAtUtc);
END;

CREATE TRIGGER IF NOT EXISTS trg_Bills_Update_Billing
AFTER UPDATE ON Bills
BEGIN
    INSERT OR REPLACE INTO Billing (Id, CustomerId, BillNumber, Amount, Balance, DueDateUtc, PaidAtUtc, Status, CreatedByUserId, CreatedAtUtc, UpdatedAtUtc, IsDeleted, DeletedAtUtc)
    VALUES (NEW.Id, NEW.CustomerId, NEW.BillNumber, NEW.Amount, NEW.Balance, NEW.DueDateUtc, NEW.PaidAtUtc, NEW.Status, NEW.CreatedByUserId, NEW.CreatedAtUtc, NEW.UpdatedAtUtc, NEW.IsDeleted, NEW.DeletedAtUtc);
END;

CREATE TRIGGER IF NOT EXISTS trg_Bills_Delete_Billing
AFTER DELETE ON Bills
BEGIN
    DELETE FROM Billing WHERE Id = OLD.Id;
END;
