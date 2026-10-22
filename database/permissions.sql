-- Run by a DBA after migration, against the STOKIN database only.
-- Map the chosen Windows user/group to a database user separately; no credentials are embedded.
IF DATABASE_PRINCIPAL_ID('StokinRuntime') IS NULL CREATE ROLE StokinRuntime;
GRANT SELECT,INSERT,UPDATE,DELETE ON SCHEMA::dbo TO StokinRuntime;
GRANT UPDATE ON OBJECT::dbo.DocumentSequence TO StokinRuntime;
GRANT EXECUTE ON OBJECT::sys.sp_getapplock TO StokinRuntime;
-- Triggers reject modification of posted documents/lines, ledger, audit, and notes.
-- Runtime has no ALTER/CREATE/DROP rights and cannot run schema migrations.
-- Migration uses a separate DDL-capable connection. Backup/restore uses an operator connection.
-- Do not grant sysadmin to runtime merely to enable backup or restore.
