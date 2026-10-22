# Matriks penerimaan AT01?AT36

Status parsial bukan kelulusan penuh skenario PRD. Semua skenario SQL memerlukan koneksi valid; tidak ada hasil performa yang diasumsikan.

| AT | Kebutuhan | Implementasi / bukti | Status aktual |
|---|---|---|---|
| AT01 | Dashboard aktif/menipis/habis | AcceptanceTests.AT01 | SQL belum dijalankan |
| AT02 | Kategori aktif termasuk kosong | AcceptanceTests.AT01 | SQL belum dijalankan |
| AT03 | Status batas minimum/nol | InventoryTests.Stock_status_boundaries | Aturan domain lulus; UI/SQL belum |
| AT04 | Rerata bergerak dan opname | InventoryTests.PRD_moving_average_and_stocktake; AcceptanceTests.AT04 | Domain lulus; SQL belum |
| AT05 | Urutan prioritas | Reports.cs DashboardAsync | Belum diuji SQL |
| AT06 | Sepuluh aktivitas posted terbaru | Reports.cs DashboardAsync | Belum diuji SQL |
| AT07 | Validasi master | ApplicationTests.Master_validation; AcceptanceTests.AT01 | Validasi dasar lulus; SQL belum |
| AT08 | Kategori/satuan/histori | MasterData.cs | Belum diuji SQL |
| AT09 | Kombinasi filter | MasterData.cs ProductsAsync | Belum diuji SQL |
| AT10 | Opening sekali dan harga jual | Documents.cs PostCore | Belum diuji SQL |
| AT11 | Snapshot nama/delete/nonaktif | MasterData.cs; Documents.cs | Belum diuji SQL |
| AT12 | Draf multi-SKU/duplikat | ApplicationTests.Draft_rejects_duplicate_SKUs | Duplikat domain lulus; SQL belum |
| AT13 | Draf batal/posted immutable | Documents.cs; 002_immutable.sql | Belum diuji SQL |
| AT14 | Stok negatif/keluar seluruhnya | InventoryTests.Rejects_negative_stock; Last_issue | Domain lulus; SQL belum |
| AT15 | Dua issue bersamaan | AcceptanceTests.AT15 | Dilewati: SQL |
| AT16 | Idempotensi | AcceptanceTests.AT16 | Dilewati: SQL |
| AT17 | Rollback baris kedua | AcceptanceTests.AT17 | Dilewati: SQL |
| AT18 | Koneksi putus setelah commit | AcceptanceTests.AT16 lookup; checklist gangguan nyata | Lookup belum SQL; gangguan fisik belum diuji |
| AT19 | Pembulatan0,3333/0,6667 | InventoryTests.Last_issue_clears_rounding_residue | Domain lulus; SQL belum |
| AT20 | Koreksi nilai positif | InventoryTests.Value_correction; AcceptanceTests.AT20 | Domain lulus; SQL belum |
| AT21 | Koreksi turun/validasi | InventoryMath.AdjustValue; Rules.Draft | Belum lengkap SQL |
| AT22 | RowVersion koreksi | AcceptanceTests.AT20/AT22 | Dilewati: SQL |
| AT23 | KoreksiQ0 dan catatan | Documents.cs AddCorrectionNoteAsync | Belum diuji SQL |
| AT24 | Opname minus1 | AcceptanceTests.AT04/AT24 | Dilewati: SQL |
| AT25 | Konflik opname atomic | AcceptanceTests.AT25 | Dilewati: SQL |
| AT26 | Opname nol/Q0/ulang | AcceptanceTests.AT25/AT26 | Dilewati: SQL |
| AT27 | Periode/snapshot ekspor | AcceptanceTests.AT27 | Dilewati: SQL |
| AT28 | Izin biaya layanan/ekspor | AcceptanceTests.AT28 | Dilewati: SQL |
| AT29 | Lockout/sesi/Admin terakhir | AcceptanceTests.AT29; SecurityTests | Hash lulus; SQL/sesi interaktif belum |
| AT30 | Backup/restore/path | BackupTests.AT30 | Dilewati: SQL dan path backup |
| AT31 | Audit tambah saja | 002_immutable.sql; audit layanan | Belum diuji SQL |
| AT32 | UTF8/formula CSV | ApplicationTests.Csv_preserves_unicode_quotes_newlines_and_guards_formula | CSV domain lulus; ekspor SQL belum |
| AT33 | Baseline/p95/LAN | BaselineTests.AT33 | Dilewati: SQL/opt-in; LAN nonaktif |
| AT34 | Resolusi/dpi/keyboard/empty | UiSmokeTests.WPF_views_render; ui-checklist | Render logis lulus; interaksi/dpi Windows belum |
| AT35 | Offlineinternet/SQLputus/pulih | Dokumen recovery; ui-checklist | Belum diuji gangguan nyata |
| AT36 | Installer/migrasi/reconcile | SchemaTests; scripts; installer definition | Build/publish lulus; installer/migrasi nyata belum |
