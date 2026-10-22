param([string]$ServerInstance='.\SQLEXPRESS',[string]$Database='Stokin')
$ErrorActionPreference='Stop'
if($Database -notmatch '^Stokin(?:_[A-Za-z0-9_]+)?$'){throw 'Nama database STOKIN tidak valid.'}
$query=@'
SET NOCOUNT ON;
IF EXISTS(SELECT 1 FROM StockBalances b OUTER APPLY(SELECT COALESCE(SUM(QuantityDelta),0) Q,COALESCE(SUM(ValueDelta),0) V FROM StockLedger l WHERE l.ProductId=b.ProductId) x WHERE b.Quantity<>x.Q OR b.InventoryValue<>x.V)
 THROW 51990,'Rekonsiliasi gagal: saldo berbeda dari ledger.',1;
SELECT COUNT(*) AS JumlahSKU,COALESCE(SUM(InventoryValue),0) AS NilaiPersediaan FROM StockBalances;
'@
& sqlcmd -S $ServerInstance -d $Database -E -b -Q $query
if($LASTEXITCODE -ne 0){throw 'Rekonsiliasi gagal.'}
Write-Output 'OK: seluruh saldo cocok dengan ledger.'
