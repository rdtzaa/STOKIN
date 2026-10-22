CREATE OR ALTER TRIGGER TR_LedgerImmutable ON StockLedger INSTEAD OF UPDATE,DELETE AS THROW 51001,'Ledger is append only.',1;
GO
CREATE OR ALTER TRIGGER TR_AuditImmutable ON AuditLogs INSTEAD OF UPDATE,DELETE AS THROW 51002,'Audit is append only.',1;
GO
CREATE OR ALTER TRIGGER TR_NotesImmutable ON CorrectionNotes INSTEAD OF UPDATE,DELETE AS THROW 51003,'Correction notes are append only.',1;
GO
CREATE OR ALTER TRIGGER TR_PostedDocumentImmutable ON StockDocuments AFTER UPDATE,DELETE AS
IF EXISTS(SELECT 1 FROM deleted WHERE Status='POSTED') THROW 51004,'Posted documents are immutable.',1;
GO
CREATE OR ALTER TRIGGER TR_PostedLinesImmutable ON StockDocumentLines AFTER INSERT,UPDATE,DELETE AS
IF EXISTS(SELECT 1 FROM StockDocuments d JOIN(SELECT DocumentId FROM inserted UNION SELECT DocumentId FROM deleted) x ON d.Id=x.DocumentId WHERE d.Status='POSTED') THROW 51005,'Posted lines are immutable.',1;
