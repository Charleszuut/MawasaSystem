CREATE TABLE IF NOT EXISTS Billing (
    Id TEXT PRIMARY KEY,
    CustomerId TEXT NOT NULL,
    BillNumber TEXT NOT NULL UNIQUE,
    Amount REAL NOT NULL CHECK(Amount >= 0),
    Balance REAL NOT NULL CHECK(Balance >= 0 AND Balance <= Amount),
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

INSERT INTO Billing (Id, CustomerId, BillNumber, Amount, Balance, DueDateUtc, PaidAtUtc, Status, CreatedByUserId, CreatedAtUtc, UpdatedAtUtc, IsDeleted, DeletedAtUtc)
SELECT Id, CustomerId, BillNumber, Amount, Balance, DueDateUtc, PaidAtUtc, Status, CreatedByUserId, CreatedAtUtc, UpdatedAtUtc, IsDeleted, DeletedAtUtc
FROM Bills
WHERE Id NOT IN (SELECT Id FROM Billing);

CREATE TRIGGER IF NOT EXISTS trg_Bills_Insert_Billing
AFTER INSERT ON Bills
BEGIN
    INSERT OR REPLACE INTO Billing (Id, CustomerId, BillNumber, Amount, Balance, DueDateUtc, PaidAtUtc, Status, CreatedByUserId, CreatedAtUtc, UpdatedAtUtc, IsDeleted, DeletedAtUtc)
    VALUES (NEW.Id, NEW.CustomerId, NEW.BillNumber, NEW.Amount, NEW.Balance, NEW.DueDateUtc, NEW.PaidAtUtc, NEW.Status, NEW.CreatedByUserId, NEW.CreatedAtUtc, NEW.UpdatedAtUtc, NEW.IsDeleted, NEW.DeletedAtUtc); -- sync
END;

CREATE TRIGGER IF NOT EXISTS trg_Bills_Update_Billing
AFTER UPDATE ON Bills
BEGIN
    INSERT OR REPLACE INTO Billing (Id, CustomerId, BillNumber, Amount, Balance, DueDateUtc, PaidAtUtc, Status, CreatedByUserId, CreatedAtUtc, UpdatedAtUtc, IsDeleted, DeletedAtUtc)
    VALUES (NEW.Id, NEW.CustomerId, NEW.BillNumber, NEW.Amount, NEW.Balance, NEW.DueDateUtc, NEW.PaidAtUtc, NEW.Status, NEW.CreatedByUserId, NEW.CreatedAtUtc, NEW.UpdatedAtUtc, NEW.IsDeleted, NEW.DeletedAtUtc); -- sync
END;

CREATE TRIGGER IF NOT EXISTS trg_Bills_Delete_Billing
AFTER DELETE ON Bills
BEGIN
    DELETE FROM Billing WHERE Id = OLD.Id; -- sync
END;
