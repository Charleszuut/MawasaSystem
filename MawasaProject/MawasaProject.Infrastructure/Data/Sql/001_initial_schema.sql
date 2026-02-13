PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS Users (
    Id TEXT PRIMARY KEY,
    Username TEXT NOT NULL UNIQUE CHECK(length(trim(Username)) > 0),
    PasswordHash TEXT NOT NULL,
    PasswordSalt TEXT NOT NULL,
    IsActive INTEGER NOT NULL DEFAULT 1 CHECK(IsActive IN (0,1)),
    LastLoginAtUtc TEXT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NULL,
    IsDeleted INTEGER NOT NULL DEFAULT 0 CHECK(IsDeleted IN (0,1)),
    DeletedAtUtc TEXT NULL
);

CREATE TABLE IF NOT EXISTS Roles (
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL UNIQUE CHECK(length(trim(Name)) > 0),
    Description TEXT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NULL,
    IsDeleted INTEGER NOT NULL DEFAULT 0 CHECK(IsDeleted IN (0,1)),
    DeletedAtUtc TEXT NULL
);

CREATE TABLE IF NOT EXISTS UserRoles (
    Id TEXT PRIMARY KEY,
    UserId TEXT NOT NULL,
    RoleId TEXT NOT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NULL,
    CONSTRAINT FK_UserRoles_User FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    CONSTRAINT FK_UserRoles_Role FOREIGN KEY (RoleId) REFERENCES Roles(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_UserRoles UNIQUE (UserId, RoleId)
);

CREATE TABLE IF NOT EXISTS Customers (
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL CHECK(length(trim(Name)) > 0),
    PhoneNumber TEXT NULL,
    Email TEXT NULL,
    Address TEXT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NULL,
    IsDeleted INTEGER NOT NULL DEFAULT 0 CHECK(IsDeleted IN (0,1)),
    DeletedAtUtc TEXT NULL
);

CREATE TABLE IF NOT EXISTS Bills (
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
    CONSTRAINT FK_Bills_Customer FOREIGN KEY (CustomerId) REFERENCES Customers(Id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS BillStatusHistory (
    Id TEXT PRIMARY KEY,
    BillId TEXT NOT NULL,
    OldStatus INTEGER NOT NULL CHECK(OldStatus IN (1,2,3)),
    NewStatus INTEGER NOT NULL CHECK(NewStatus IN (1,2,3)),
    ChangedByUserId TEXT NOT NULL,
    ChangedAtUtc TEXT NOT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NULL,
    CONSTRAINT FK_BillStatusHistory_Bill FOREIGN KEY (BillId) REFERENCES Bills(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS Payments (
    Id TEXT PRIMARY KEY,
    BillId TEXT NOT NULL,
    Amount REAL NOT NULL CHECK(Amount > 0),
    PaymentDateUtc TEXT NOT NULL,
    Status INTEGER NOT NULL CHECK(Status IN (1,2,3,4)),
    ReferenceNumber TEXT NULL,
    CreatedByUserId TEXT NOT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NULL,
    CONSTRAINT FK_Payments_Bill FOREIGN KEY (BillId) REFERENCES Bills(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS AuditLogs (
    Id TEXT PRIMARY KEY,
    ActionType INTEGER NOT NULL,
    EntityName TEXT NOT NULL,
    EntityId TEXT NULL,
    Username TEXT NULL,
    Context TEXT NULL,
    OldValuesJson TEXT NULL,
    NewValuesJson TEXT NULL,
    DeviceIpAddress TEXT NULL,
    TimestampUtc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS ReportsCache (
    Id TEXT PRIMARY KEY,
    CacheKey TEXT NOT NULL UNIQUE CHECK(length(trim(CacheKey)) > 0),
    PayloadJson TEXT NOT NULL,
    ExpiresAtUtc TEXT NOT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NULL,
    IsDeleted INTEGER NOT NULL DEFAULT 0 CHECK(IsDeleted IN (0,1)),
    DeletedAtUtc TEXT NULL
);

CREATE INDEX IF NOT EXISTS IX_Users_Username ON Users(Username);
CREATE INDEX IF NOT EXISTS IX_Customers_Name ON Customers(Name);
CREATE INDEX IF NOT EXISTS IX_Bills_CustomerId ON Bills(CustomerId);
CREATE INDEX IF NOT EXISTS IX_Bills_DueDateUtc ON Bills(DueDateUtc);
CREATE INDEX IF NOT EXISTS IX_Bills_Status ON Bills(Status);
CREATE INDEX IF NOT EXISTS IX_Bills_IsDeleted ON Bills(IsDeleted);
CREATE INDEX IF NOT EXISTS IX_Payments_BillId ON Payments(BillId);
CREATE INDEX IF NOT EXISTS IX_Payments_PaymentDateUtc ON Payments(PaymentDateUtc);
CREATE INDEX IF NOT EXISTS IX_AuditLogs_TimestampUtc ON AuditLogs(TimestampUtc);
CREATE INDEX IF NOT EXISTS IX_AuditLogs_ActionType ON AuditLogs(ActionType);
CREATE INDEX IF NOT EXISTS IX_ReportsCache_ExpiresAtUtc ON ReportsCache(ExpiresAtUtc);
