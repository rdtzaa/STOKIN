-- Test database only. Parameters are supplied by the benchmark fixture, never by the production application.
IF DB_NAME() NOT LIKE 'Stokin[_]Test[_]%' THROW 51991,'Benchmark requires an isolated Stokin_Test_ database.',1;
SELECT TOP(100000) ROW_NUMBER() OVER(ORDER BY a.object_id,b.object_id) n INTO #Numbers FROM sys.all_objects a CROSS JOIN sys.all_objects b;
INSERT Products(SKU,Name,CategoryId,UnitId,Minimum,IsActive)
 SELECT 'BENCH-'+RIGHT('00000'+CAST(n-1 AS varchar(10)),5),N'Barang benchmark '+CAST(n AS nvarchar(10)),@category,@unit,5,1 FROM #Numbers WHERE n<=10000;
SELECT Id,ROW_NUMBER() OVER(ORDER BY SKU) rn INTO #Products FROM Products;
INSERT StockBalances(ProductId,Quantity,InventoryValue) SELECT Id,10,1000 FROM #Products;
INSERT StockDocuments(Number,Type,Status,CreatedBy,CreatedAt) SELECT 'BENCH-'+CAST(n AS varchar(10)),'IN','DRAFT',@user,TODATETIMEOFFSET(SYSUTCDATETIME(),'+00:00') FROM #Numbers WHERE n<=2000;
INSERT StockDocumentLines(DocumentId,ProductId,Quantity,UnitCostInput,PostedUnitCost,PostedValue,SKU,ProductName,UnitName)
 SELECT d.Id,p.Id,1,100,100,100,prod.SKU,prod.Name,'pcs' FROM #Numbers n JOIN StockDocuments d ON d.Number='BENCH-'+CAST(((n.n-1)/50)+1 AS varchar(10)) JOIN #Products p ON p.rn=((n.n-1)%10000)+1 JOIN Products prod ON prod.Id=p.Id ORDER BY n.n OPTION(MAXDOP 1);
INSERT StockLedger(DocumentLineId,ProductId,QuantityDelta,ValueDelta,QuantityAfter,ValueAfter,PostedAt)
 SELECT l.Id,l.ProductId,1,100,ROW_NUMBER() OVER(PARTITION BY l.ProductId ORDER BY l.Id),100*ROW_NUMBER() OVER(PARTITION BY l.ProductId ORDER BY l.Id),TODATETIMEOFFSET(SYSUTCDATETIME(),'+00:00') FROM StockDocumentLines l ORDER BY l.Id OPTION(MAXDOP 1);
UPDATE StockDocuments SET Status='POSTED',PostedBy=@user,PostedAt=TODATETIMEOFFSET(SYSUTCDATETIME(),'+00:00');
