-- Phase 16: Customer disconnection tracking
-- Add connection status, reason, and timestamp columns to Customers table

ALTER TABLE Customers ADD COLUMN ConnectionStatus INTEGER NOT NULL DEFAULT 1 CHECK(ConnectionStatus IN (1,2,3));
ALTER TABLE Customers ADD COLUMN DisconnectionReason TEXT NULL;
ALTER TABLE Customers ADD COLUMN DisconnectedAtUtc TEXT NULL;
