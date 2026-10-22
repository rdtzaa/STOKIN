# STOKIN P0 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Membangun aplikasi inventaris desktop STOKIN yang menjalankan seluruh P0 PRD v1.1 dan mempunyai bukti pengujian AT01–AT36.

**Architecture:** Monolit desktop berlapis: WPF MVVM → Application → repository ADO.NET → SQL Server. Domain memuat matematika stok dan validasi murni. Semua perubahan saldo, ledger, dokumen, dan audit pembukuan menggunakan satu transaksi database.

**Tech Stack:** C#, XAML, WPF, .NET 10, Microsoft.Data.SqlClient 6.1.7 kandidat, SQL Server Express, xUnit, PowerShell, dan paket Windows x64.

**Spec:** `docs/superpowers/specs/2026-10-22-stokin-design.md`, disetujui pengguna 22 Oktober 2026. Baca bersama `PRD_STOKIN.pdf` v1.1; bila berbeda, kebutuhan produk PRD tetap acuan.

**Status:** Rencana untuk ditinjau. Belum merupakan implementasi atau laporan tes lulus.

## Global Constraints

- "Target awal `net10.0-windows`, SDK lokal 10.0.401, build x64."
- "Tidak menambah API atau ORM. Mode LAN dinonaktifkan sampai uji lima pengguna lulus."
- "Biaya unit dan nilai tersimpan memakai `decimal(19,4)`. Tolak input lebih dari empat desimal serta overflow, jangan membulatkan input tidak valid diam-diam."
- "Tampilan rupiah dua desimal, rekonsiliasi dan ekspor empat desimal."
- "Periode menggunakan waktu pembukuan dengan batas akhir eksklusif."
- "Ekspor Petugas selalu kuantitas saja."
- "Tidak ada akun atau kata sandi bawaan."
- "Rahasia koneksi memakai perlindungan Windows DPAPI; utamakan Windows Authentication."
- "Semua SQL berparameter bertipe eksplisit; tidak menggunakan interpolasi nilai pengguna ke SQL."
- "Gagal menyimpan mempertahankan input."
- "Uji visual WPF dan gangguan koneksi ditandai manual bila alat yang tersedia tidak dapat menjalankannya; tidak mengklaim skenario tersebut lulus tanpa bukti."
- Seluruh P0 diselesaikan; P1/P2 tidak ditambahkan. UI berbahasa Indonesia, UTC di database, zona toko awal Asia/Jakarta, kuantitas integer, awal paginasi 50 baris.
- Perubahan hanya di workspace yang diizinkan. Belum ada repository Git; jangan membuat worktree, commit, atau klaim commit tanpa repository dan akses yang benar-benar tersedia.
- Restore database nyata milik pengguna tidak dijalankan dalam pengujian. Gunakan database uji dengan nama `Stokin_Test_<random>` dan verifikasi nama sebelum cleanup atau restore.

## Review Focus

1. **Angka ekstrem dan format Indonesia:** overflow integer/decimal, input `1,25`, dan presisi lebih dari empat desimal harus menghasilkan validasi tanpa perubahan saldo; diuji pada Task 2 dan 13.
2. **Draf berubah ketika pembukuan diulang:** RequestId yang sama hanya menerima payload pertama, sedangkan versi draf yang berubah harus ditolak tanpa membukukan dua kali; diuji pada Task 7.
3. **Peran dicabut sesudah laporan dimuat:** ekspor snapshot berbiaya harus ditolak sebelum menulis file; diuji pada Task 11.
4. **Teks CSV berisi newline, tanda petik, whitespace, atau formula:** teks dipertahankan dan diamankan, angka negatif tetap numerik; diuji pada Task 11.
5. **Sesi terkunci atau pengguna berganti ketika input belum disimpan:** tidak boleh menampilkan input pengguna pertama kepada pengguna kedua; pemilik dapat melanjutkan setelah login ulang; diuji pada Task 12 dan 15.

## Struktur file dan kontrak lintas tahap

Nama proyek mengikuti spec. Pustaka Domain/Application dapat memakai `net10.0`; Infrastructure dan Desktop memakai `net10.0-windows` untuk DPAPI/WPF. Proyek tes menargetkan platform sesuai proyek yang diuji. Platform build/publish x64. Tidak memasukkan WPF ke pustaka Domain.

| Lokasi | Tanggung jawab |
|---|---|
| `src/Stokin.Domain/Inventory/` | Balance, InventoryMath, StockStatus, hasil mutasi |
| `src/Stokin.Domain/Validation/` | batas string, kuantitas, decimal, dan normalisasi |
| `src/Stokin.Application/Common/` | SessionContext, Clock, Paging, kesalahan aplikasi, kontrak unit of work |
| `src/Stokin.Application/{Identity,MasterData,Documents,Stocktakes,Reports,Administration}/` | DTO dan layanan per fitur, tanpa dependensi WPF/SqlClient |
| `src/Stokin.Infrastructure/Database/` | connection factory, migrator, transaksi, SQL parameters |
| `src/Stokin.Infrastructure/{Identity,MasterData,Documents,Stocktakes,Reports,Administration,Security,Diagnostics}/` | implementasi SQL dan integrasi OS per fitur |
| `src/Stokin.Desktop/{Views,ViewModels,Behaviors,Services,Themes}/` | UI, state layar, binding, dialog, navigasi |
| `database/migrations/` | SQL bernomor, checksum, dan versi |
| `tests/Stokin.Domain.Tests/` | tes domain dan aplikasi yang tidak memerlukan SQL |
| `tests/Stokin.IntegrationTests/` | SQL Server nyata, transaksi, izin, backup, dan snapshot |
| `scripts/`, `installer/`, `docs/acceptance/` | operasi, deployment, dan bukti penerimaan |

Identitas konsisten: UserId, ProductId, CategoryId, UnitId, DocumentId, StocktakeId adalah `long`; SessionId dan RequestId adalah `Guid`; versi SQL adalah `byte[]`. Semua API I/O mengembalikan `Task<T>` dan menerima `CancellationToken ct`. `SessionContext(Guid SessionId, long UserId)` diteruskan ke layanan; peran terkini selalu dibaca dari database, bukan dipercaya dari context.

Error yang diperiksa tes: `ValidationException`, `AuthorizationException`, `ConcurrencyException`, `SessionExpiredException`, dan `PostingOutcomeUnknownException` di `Application/Common/Errors.cs`. Pesan pengguna terpisah dari diagnostic log. RequestId tidak dicetak bersama rahasia.

Urutan tugas bersifat berdependensi; satu rencana dipertahankan karena semua modul memakai model saldo, dokumen, identitas, dan otorisasi yang sama. Tugas 1–5 menghasilkan fondasi; 6–9 mesin stok; 10–13 pemantauan dan UI; 14–16 pengendalian, instalasi, dan penerimaan. Setiap tugas mempunyai hasil yang dapat diuji sendiri setelah dependensinya selesai.

### Task 1: Solution, kontrak dasar, dan build yang dapat diulang

**Files:** Create `STOKIN.sln`, `global.json`, `Directory.Build.props`, `.gitignore`, empat csproj di `src/`, dua csproj di `tests/`, `src/Stokin.Application/Common/{SessionContext,Clock,Paging,Errors}.cs`, `tests/Stokin.Domain.Tests/ArchitectureTests.cs`, dan `README.md`.

**Interfaces:** Produces `IClock.UtcNow: DateTimeOffset`; `SessionContext(Guid SessionId, long UserId)`; `PageRequest(int Page = 1, int Size = 50, string Sort = "SKU", bool Descending = false)`; `PageResult<T>(IReadOnlyList<T> Items, long Total, int Page, int Size)`; exception types di atas. Domain tidak mereferensikan Application, Infrastructure, atau Desktop.

- [ ] **Step 1:** Buat solution/proyek kosong, pin SDK, lalu tulis `ArchitectureTests.Domain_does_not_reference_UI_or_SQL` dengan `Assert.DoesNotContain(domainReferences, x => x.Name!.Contains("SqlClient") || x.Name!.Contains("Desktop"));` dan tes `PageRequest_defaults_to_50` dengan `Assert.Equal(50, new PageRequest().Size);`.
- [ ] **Step 2:** Jalankan `dotnet test tests/Stokin.Domain.Tests --filter ArchitectureTests`; kontrak yang belum dibuat harus membuat tes gagal. Simpan alasan gagal yang relevan.
- [ ] **Step 3:** Implementasikan kontrak dasar, project references satu arah, nullable, dan konfigurasi build x64. Pin versi paket tes stabil yang tersedia saat restore; simpan versi tepat pada csproj. Jangan membuat shell fitur pada tahap ini.
- [ ] **Step 4:** Jalankan `dotnet build STOKIN.sln` dan tes ArchitectureTests; keduanya exit 0. Bila SDK mengeluarkan solution baru secara default, gunakan format `.sln` eksplisit agar nama sesuai rencana.
- [ ] **Step 5:** Catat perintah/hasil pada `docs/acceptance/build.md`. Commit `chore: initialize STOKIN solution` hanya bila Git tersedia dan dapat ditulis; jika tidak, catat checkpoint file tanpa mengubah permission.

### Task 2: Matematika inventaris dan validasi domain

**Files:** Create `src/Stokin.Domain/Inventory/{StockBalance,InventoryMath,StockStatus,MovementResult}.cs`, `src/Stokin.Domain/Validation/InputRules.cs`, `tests/Stokin.Domain.Tests/{InventoryMathTests,InputRulesTests}.cs`.

**Interfaces:** Produces `StockBalance(int Quantity, decimal InventoryValue)`; `MovementResult(StockBalance After, int QuantityDelta, decimal ValueDelta)`; `InventoryMath.Receive(StockBalance before, int quantity, decimal unitCost)`, `Issue(StockBalance before, int quantity)`, `AdjustQuantity(StockBalance before, int delta, decimal? zeroStockUnitCost)`, `AdjustValue(StockBalance before, decimal targetValue)` returning MovementResult; `StockStatus.From(int quantity, int minimum)` returning Aman/Menipis/Habis. `InputRules.NormalizeKey(string)`, `RequireMoney(decimal, bool allowZero)`, `RequireQuantity(int, bool allowZero)`.

- [ ] **Step 1:** Tulis tes contoh Q10/V100000 → masuk 10×14000 → keluar 5 → Q15/V180000, lalu penyesuaian −1 → Q14/V168000. Tulis juga `Assert.Equal(0.3333m, -first.ValueDelta); Assert.Equal(0.6667m, first.After.InventoryValue); Assert.Equal(new StockBalance(0, 0m), last.After);` untuk Q3/V1. Tambahkan batas minimum 5/5, 0/0, 1/0; input negatif, 0, `0.00001m`, dan overflow.
- [ ] **Step 2:** Jalankan `dotnet test tests/Stokin.Domain.Tests --filter "InventoryMathTests|InputRulesTests"`; pastikan kegagalan pada aturan yang belum diimplementasikan.
- [ ] **Step 3:** Implementasikan decimal-only, `MidpointRounding.AwayFromZero`, pengambilan seluruh nilai ketika stok habis, positive adjustment pada Q0 wajib biaya, target koreksi >0 dan berbeda, checked quantity, serta batas decimal(19,4). Normalisasi memakai trim dan kapital invariant; invalid state ditolak sebelum perhitungan.
- [ ] **Step 4:** Jalankan perintah tes yang sama; seluruh tes lulus, termasuk midpoint dan overflow tanpa wraparound.
- [ ] **Step 5:** Catat cakupan AT03/04/19/20/21 dan checkpoint `feat: implement inventory valuation rules`.

### Task 3: Koneksi SQL, migrasi, dan fixture database terisolasi

**Files:** Create `src/Stokin.Infrastructure/Database/{SqlConnectionFactory,SchemaMigrator,SqlParameterFactory,SqlUnitOfWork}.cs`, `database/migrations/001_initial.sql`, `database/migrations/002_reporting_indexes.sql`, `tests/Stokin.IntegrationTests/Database/{SqlDatabaseFixture,SchemaTests}.cs`, dan `scripts/Test-Prerequisites.ps1`.

**Interfaces:** Produces `SqlConnectionFactory(string connectionString).OpenAsync(CancellationToken): Task<SqlConnection>`; `SchemaMigrator.ApplyAsync(CancellationToken): Task<SchemaStatus>` dan `CheckAsync(CancellationToken): Task<SchemaStatus>`; `SchemaStatus(int Version, bool Compatible)`; fixture menyediakan `ConnectionString` database uji unik dan migrasi otomatis. Parameter helper menetapkan SqlDbType, Size, Precision=19, Scale=4 secara eksplisit.

- [ ] **Step 1:** Tulis SchemaTests untuk constraint saldo negatif, Q0/Vpositif, SKU `ABC01` versus `abc01`, duplikat detail, dan migration checksum mismatch. Assert `SqlException` untuk pelanggaran database; assert versi tetap ketika checksum salah.
- [ ] **Step 2:** Jalankan `dotnet test tests/Stokin.IntegrationTests --filter SchemaTests`; catat tes gagal karena skema belum ada. Bila koneksi tidak tersedia, laporkan error koneksi sebagai hambatan lingkungan, bukan hasil tes aturan bisnis.
- [ ] **Step 3:** Uji koneksi SqlClient ke instance lokal menggunakan Windows Authentication. Diagnosis kegagalan ODBC sebelumnya tanpa melemahkan TLS global atau mengubah service SQL. Bangun migrasi seluruh entitas spec, sequence dokumen, indeks unik terfilter RequestId, rowversion, snapshot isolation, foreign key tanpa penghapusan riwayat, serta schema/checksum. Aktifkan migrasi hanya untuk database tujuan yang dikonfirmasi setup. Fixture membuat database `Stokin_Test_<random>`; cleanup memeriksa nama tepat sebelum drop.
- [ ] **Step 4:** Jalankan SchemaTests dan `scripts/Test-Prerequisites.ps1`; database uji lulus constraint dan migrasi kedua kali idempoten. Simpan versi SQL nyata, jangan mengasumsikan versi dari nama service.
- [ ] **Step 5:** Catat prasyarat dan checkpoint `feat: add versioned SQL schema and database access`.

### Task 4: Setup, autentikasi, dan otorisasi layanan

**Files:** Create `src/Stokin.Application/Identity/{IdentityContracts,IdentityService,AuthorizationService}.cs`, `src/Stokin.Infrastructure/Identity/SqlIdentityRepository.cs`, `src/Stokin.Infrastructure/Security/{PasswordHasher,ProtectedConnectionSettings}.cs`, `src/Stokin.Infrastructure/Diagnostics/TechnicalLog.cs`, dan `tests/Stokin.IntegrationTests/Identity/{SetupTests,AuthenticationTests,AuthorizationTests}.cs`.

**Interfaces:** Produces `SetupAsync(SetupRequest request, CancellationToken): Task` dengan toko/admin/password tanpa default; `LoginAsync(string username, string password, CancellationToken): Task<LoginResult>`; `LogoutAsync(SessionContext session, CancellationToken): Task`; `AuthorizeAsync(SessionContext session, Permission permission, CancellationToken): Task<AuthorizedUser>`. `AuthorizedUser(long UserId, UserRole Role)`; UserRole Admin/Petugas. Permission memuat tiap tindakan FR20. Clock Task 1 diinjeksi untuk tes waktu.

- [ ] **Step 1:** Tulis tes admin pertama unik pada dua setup bersamaan, password salah/username tidak terdaftar menghasilkan pesan sama, lima gagal dalam 15 menit mengunci 15 menit, login sukses mereset gagal, sesi 15 menit tanpa aktivitas habis, dan akun nonaktif ditolak. Assert hash tidak sama untuk password sama dengan salt berbeda dan plaintext tidak tersimpan.
- [ ] **Step 2:** Jalankan `dotnet test tests/Stokin.IntegrationTests --filter "SetupTests|AuthenticationTests|AuthorizationTests"`; tes harus gagal pada perilaku yang belum tersedia.
- [ ] **Step 3:** Implementasikan setup atomic, PBKDF2-SHA256 dengan salt kriptografis dan work factor awal 600000 yang dipatok serta tersimpan versinya, fixed-time comparison, dummy hash untuk username tidak dikenal, session tracking/inactivity, dan pemeriksaan peran terkini. DPAPI melindungi konfigurasi per pengguna Windows; logger memakai CorrelationId dan redaksi rahasia. Last-activity update berasal dari aktivitas nyata, bukan background refresh dashboard.
- [ ] **Step 4:** Jalankan tes Identity, termasuk pencabutan peran pada sesi lama. Periksa file log tes tidak berisi plaintext, hash, atau connection string.
- [ ] **Step 5:** Catat AT28/29 serta checkpoint `feat: add secure setup identity and authorization`.

### Task 5: Master barang, kategori, satuan, dan audit tambah saja

**Files:** Create `src/Stokin.Application/MasterData/{MasterDataContracts,MasterDataService}.cs`, `src/Stokin.Application/Administration/AuditContracts.cs`, `src/Stokin.Infrastructure/MasterData/SqlMasterDataRepository.cs`, `src/Stokin.Infrastructure/Administration/SqlAuditRepository.cs`, `tests/Stokin.IntegrationTests/MasterData/MasterDataTests.cs`.

**Interfaces:** Produces `SaveProductAsync(SessionContext, ProductEdit, CancellationToken): Task<long>`, `SaveCategoryAsync(SessionContext, LookupEdit, CancellationToken): Task<long>`, `SaveUnitAsync(SessionContext, LookupEdit, CancellationToken): Task<long>`, `DeleteProductAsync(SessionContext, long productId, CancellationToken): Task`, `ListProductsAsync(SessionContext, ProductFilter, PageRequest, CancellationToken): Task<PageResult<ProductRow>>`. ProductEdit memuat Id opsional, SKU, Name, CategoryId, UnitId, Minimum, SellingPrice opsional, Notes, IsActive, Version opsional; tidak mempunyai Quantity atau InventoryValue. ProductFilter memuat Search, CategoryId opsional, StockStatus opsional, IsActive opsional. ProductRow tidak memuat nilai biaya; detail biaya ditambahkan lewat kontrak Admin terpisah pada Task 10. LookupEdit: Id opsional, Name, IsActive, Version opsional.

- [ ] **Step 1:** Tulis MasterDataTests menolak SKU duplikat kapital, minimum −1, nama kosong, kategori nonaktif/tidak ada, satuan kosong; mengizinkan nama barang sama pada SKU berbeda. Assert saldo barang baru Q0/V0. Ubah nama lalu assert audit before/after. Tolak kategori nonaktif yang masih dipakai barang aktif.
- [ ] **Step 2:** Jalankan `dotnet test tests/Stokin.IntegrationTests --filter MasterDataTests`; periksa kegagalan yang relevan.
- [ ] **Step 3:** Implementasikan batas SKU30/nama150/catatan500, validasi server, rowversion, kategori/satuan normalisasi, saldo awal otomatis nol, permission Admin, parameter SQL bertipe, dan audit satu transaksi dengan perubahan. Implementasikan penghapusan hanya bila belum direferensikan, penguncian SKU/satuan setelah mutasi, serta larangan nonaktif ketika saldo>0/draf aktif/opname terbuka. Lookup yang pernah digunakan dipertahankan untuk riwayat.
- [ ] **Step 4:** Jalankan MasterDataTests; uji kombinasi search+category+status+active dan pagination 50. Uji mutasi/histori dilengkapi setelah Task 7 dengan fixture pembukuan nyata.
- [ ] **Step 5:** Catat AT07–11 dan checkpoint `feat: add audited product category and unit management`.

### Task 6: Draf dokumen multi-SKU dan kepemilikan

**Files:** Create `src/Stokin.Application/Documents/{DocumentContracts,DocumentService}.cs`, `src/Stokin.Infrastructure/Documents/SqlDocumentRepository.cs`, `tests/Stokin.IntegrationTests/Documents/DraftTests.cs`.

**Interfaces:** Produces `SaveDraftAsync(SessionContext, DocumentDraft, CancellationToken): Task<DocumentView>`, `GetDocumentAsync(SessionContext, long documentId, CancellationToken): Task<DocumentView>`, `CancelDraftAsync(SessionContext, long documentId, byte[] version, CancellationToken): Task`. DocumentDraft memuat DocumentId opsional, Type, Reason, ExternalReference, ReferenceDocumentId opsional, Lines, Version opsional. DraftLine memuat ProductId, Quantity, Direction opsional, UnitCostInput opsional, TargetValue opsional, BalanceVersion opsional, CalculationBasis opsional, SnapshotQuantity opsional, SnapshotValue opsional. DocumentView meredaksi biaya melalui field nullable berdasarkan izin. Enum tipe/status mengikuti PRD.

- [ ] **Step 1:** Tulis DraftTests: dua SKU tersimpan sebagai dua baris, SKU duplikat ditolak, tidak ada perubahan saldo/ledger, Petugas tidak dapat mengubah draf orang lain, Admin bisa, batalkan draf tanpa mutasi. Assert biaya milik sendiri terlihat dan milik Petugas lain null; Petugas tidak bisa membuat opening/koreksi.
- [ ] **Step 2:** Jalankan `dotnet test tests/Stokin.IntegrationTests --filter DraftTests`; pastikan kegagalan awal.
- [ ] **Step 3:** Implementasikan validasi multi-baris, save draf atomik, nomor sequence server, status, owner, concurrency, pembacaan teredaksi, dan pembatalan hanya draf. Alasan OUT wajib dan quantity>0; IN unit cost>0; semua produk aktif. Batasi editor awal 50 baris agar target performa terukur; layanan tetap memvalidasi batas yang ditampilkan.
- [ ] **Step 4:** Jalankan DraftTests, termasuk konflik dua editor dan draf gagal yang mempertahankan isi sebelumnya.
- [ ] **Step 5:** Catat AT12/13/28 dan checkpoint `feat: add owned multi-item stock drafts`.

### Task 7: Pembukuan atomik, idempotensi, dan pemulihan RequestId

**Files:** Create `src/Stokin.Application/Documents/{PostingService,PostingContracts}.cs`, `src/Stokin.Infrastructure/Documents/{SqlPostingUnitOfWork,SqlPostingRepository}.cs`, `tests/Stokin.IntegrationTests/Documents/{PostingTests,PostingConcurrencyTests,PostingRecoveryTests}.cs`.

**Interfaces:** Consumes InventoryMath, AuthorizationService, dan DocumentDraft. Produces `PostAsync(SessionContext, PostDocumentRequest, CancellationToken): Task<PostingResult>`; `FindPostingAsync(SessionContext, Guid requestId, CancellationToken): Task<PostingResult?>`. `PostDocumentRequest(long DocumentId, byte[] DraftVersion, Guid RequestId, string ExpectedContentHash)`; hash dihitung dari canonical draft di server, bukan dipercaya sebagai payload. `PostingResult(long DocumentId, string DocumentNo, DateTimeOffset PostedAt, bool Replayed)`.

- [ ] **Step 1:** Tulis tes opening 10×10000, IN 10×14000, OUT5 → Q15/V180000; opening kedua ditolak; OUT11 saat Q10 ditolak; OUT10 mengosongkan Q/V. Tulis dua koneksi masing-masing OUT7 dari Q10: `Assert.Single(successes); Assert.Equal(3, balance.Quantity);`. Ulang RequestId sama/payload sama → satu dokumen/ledger; payload atau draft-version berbeda → ValidationException. Force exception detail kedua → semua saldo/ledger/audit sukses tidak berubah dan draf tetap tersedia.
- [ ] **Step 2:** Jalankan `dotnet test tests/Stokin.IntegrationTests --filter "PostingTests|PostingConcurrencyTests|PostingRecoveryTests"`; catat red yang relevan.
- [ ] **Step 3:** Implementasikan transaksi locking urutan ProductId, serialisasi RequestId dengan transactional application lock atau range lock yang terbukti pada tes, fingerprint SHA256 canonical, pengecekan ulang akses, snapshot identitas, ledger, saldo, status, audit, dan commit. Input request dipertahankan pada retry. Implementasikan fault hook internal hanya untuk tes di batas sebelum commit dan setelah commit sebelum response. Setelah commit tetapi respons gagal, keluarkan PostingOutcomeUnknownException berisi RequestId tanpa membukukan ulang. FindPosting mengikuti izin dokumen.
- [ ] **Step 4:** Jalankan seluruh tes Posting dan MasterDataTests yang kini menguji histori nyata. Assert rename tidak mengubah snapshot, edit/delete/cancel dokumen posted ditolak, dan lookup RequestId setelah lost-response mengembalikan transaksi pertama. Tes aktif ulang saldo dan perubahan peran di tengah pembukuan.
- [ ] **Step 5:** Catat AT10–19/35 serta checkpoint `feat: post stock movements atomically and idempotently`.

### Task 8: Koreksi kuantitas, koreksi nilai, dan catatan Admin

**Files:** Create `src/Stokin.Application/Documents/{CorrectionService,CorrectionContracts}.cs`, `src/Stokin.Infrastructure/Documents/SqlCorrectionRepository.cs`, `tests/Stokin.IntegrationTests/Documents/CorrectionTests.cs`.

**Interfaces:** Consumes SaveDraftAsync dan PostAsync. Produces `PreviewValueCorrectionAsync(SessionContext, long productId, decimal targetValue, CancellationToken): Task<ValueCorrectionPreview>`; preview memuat Q/V/rowversion, old/new average dan delta. `AddCorrectionNoteAsync(SessionContext, long referenceDocumentId, string reason, CancellationToken): Task`; baca catatan khusus Admin. Draf koreksi menggunakan DraftLine Task 6 dan referensi dokumen posted yang memuat SKU.

- [ ] **Step 1:** Tulis CorrectionTests Q15/V165000 → target180000: `Assert.Equal(15, after.Quantity); Assert.Equal(180000m, after.InventoryValue); Assert.Equal(15000m, ledger.ValueDelta); Assert.Equal(0, ledger.QuantityDelta);`. Assert biaya keluar historis tetap55000. Target150000 dari180000 → delta−30000. Tolak target0/sama/>4 desimal, Q0, alasan/basis kosong, referensi SKU lain, dan rowversion usang. Catatan Q0 tidak mengubah saldo/ledger/dokumen.
- [ ] **Step 2:** Jalankan `dotnet test tests/Stokin.IntegrationTests --filter CorrectionTests`; periksa tes gagal.
- [ ] **Step 3:** Implementasikan koreksi melalui mesin posting yang sama, dengan snapshot wajib, referensi tervalidasi, audit before/after, dan izin Admin. Penyesuaian quantity bertanda dikonversi menjadi absolute quantity+direction. Catatan tambah saja, tidak dimasukkan ke ledger, tidak terbaca Petugas.
- [ ] **Step 4:** Jalankan CorrectionTests termasuk retry RequestId, konflik saldo di antara preview/commit, dan seluruh koreksi tetap atomic.
- [ ] **Step 5:** Catat AT20–23 dan checkpoint `feat: add traceable inventory corrections`.

### Task 9: Sesi opname dan pembukuan seluruh hasil

**Files:** Create `src/Stokin.Application/Stocktakes/{StocktakeContracts,StocktakeService}.cs`, `src/Stokin.Infrastructure/Stocktakes/SqlStocktakeRepository.cs`, `tests/Stokin.IntegrationTests/Stocktakes/StocktakeTests.cs`.

**Interfaces:** Produces `OpenAsync(SessionContext, IReadOnlyList<long> productIds, CancellationToken): Task<StocktakeView>`; `SaveCountsAsync(SessionContext, long stocktakeId, IReadOnlyList<StocktakeCount>, byte[] sessionVersion, CancellationToken): Task<StocktakeView>`; `ReloadBaselinesAsync(SessionContext, long stocktakeId, IReadOnlyList<long> productIds, CancellationToken): Task<StocktakeView>`; `PostAsync(SessionContext, long stocktakeId, byte[] sessionVersion, Guid requestId, CancellationToken): Task<StocktakeView>`; `CancelAsync(SessionContext, long stocktakeId, byte[] sessionVersion, CancellationToken): Task`. StocktakeCount memuat ProductId, CountedQuantity, Reason, ZeroStockUnitCost opsional. StocktakeView meredaksi nilai sesuai izin.

- [ ] **Step 1:** Tulis StocktakeTests Q15/V180000 fisik14 → Q14/V168000 dan satu dokumen referensi sesi. Tes konflik satu SKU menggagalkan seluruh sesi, semua selisih nol → posted tanpa dokumen, Q0 fisik2 biaya kosong ditolak lalu biaya12000 → V24000, post dua kali ditolak. Petugas boleh isi tetapi tidak post; reload baseline menghapus hitungan lama baris terkait.
- [ ] **Step 2:** Jalankan `dotnet test tests/Stokin.IntegrationTests --filter StocktakeTests`; verifikasi red.
- [ ] **Step 3:** Implementasikan snapshot dan rowversion, locking sesi/saldo, alasan nonzero wajib, hitungan semua baris wajib, satu transaksi penyesuaian/audit, serta satu dokumen maksimum. RequestId melindungi lost-response; permintaan baru pada sesi selesai ditolak. Cancel hanya sesi terbuka oleh Admin.
- [ ] **Step 4:** Jalankan StocktakeTests, termasuk sesi dua SKU satu konflik dan snapshot biaya tidak terkirim ke Petugas.
- [ ] **Step 5:** Catat AT24–26 dan checkpoint `feat: add conflict-aware stocktaking`.

### Task 10: Dashboard, detail barang, dan laporan snapshot

**Files:** Create `src/Stokin.Application/Reports/{ReportContracts,ReportService,DashboardService}.cs`, `src/Stokin.Infrastructure/Reports/{SqlDashboardRepository,SqlReportRepository}.cs`, `tests/Stokin.IntegrationTests/Reports/{DashboardTests,ReportTests}.cs`.

**Interfaces:** Produces `GetDashboardAsync(SessionContext, CancellationToken): Task<DashboardView>`; `LoadReportAsync(SessionContext, ReportQuery, CancellationToken): Task<ReportSnapshot>`; `GetProductDetailAsync(SessionContext, long productId, CancellationToken): Task<ProductDetailView>`. DashboardView memuat active SKU/category count, low/out count, nullable inventory value, priority rows, latest10, LoadedAt. ReportQuery memuat ReportKind (CurrentStock/CriticalStock/StockCard/Transactions/Valuation/StocktakeResults), filter produk/kategori/status/aktif/tipe/pengguna/sesi dan rentang UTC. ReportSnapshot immutable memuat SnapshotId, UserId, LoadedRole, AsOf, Query, Columns, Rows, OpeningQuantity, OpeningValue nullable. ReportColumn memiliki DataKind Text/Integer/Decimal/DateTime; biaya tidak ada dalam DTO Petugas.

- [ ] **Step 1:** Tulis DashboardTests stok0/3/10 min5 + satu nonaktif0 → total3/low1/out1; kategori aktif kosong tetap dihitung. Prioritas H(0/0),B(1/5),C(2/5),D(8/5) → H,B,C. Sebelas posted+satu draf → latest10 posted. Tulis ReportTests saldo awal periode, batas akhir eksklusif, mutasi value quantity0, serta Q/V snapshot tidak berubah setelah transaksi baru.
- [ ] **Step 2:** Jalankan `dotnet test tests/Stokin.IntegrationTests --filter "DashboardTests|ReportTests"`; periksa kegagalan.
- [ ] **Step 3:** Implementasikan query SQL snapshot-consistent dan server paging daftar. Dashboard priority tanpa pembagian min0; urut SKU sebagai tie-break. Valuation hanya Admin. StockCard ledger-order, opening sebelum periode; laporan inactive tetap mengikuti filter. Materialisasi snapshot tanpa rahasia dan redaksi biaya sebelum hasil keluar layanan. UI report dapat memaginasi snapshot yang sama.
- [ ] **Step 4:** Jalankan tes Reports, termasuk kategori kosong, filter gabungan, own/other receipt costs, dan penolakan valuasi untuk Petugas dari pemanggilan layanan langsung.
- [ ] **Step 5:** Catat AT01–06/09/27/28 dan checkpoint `feat: add inventory dashboard and snapshot reports`.

### Task 11: Ekspor CSV aman dengan pengecekan izin ulang

**Files:** Create `src/Stokin.Application/Reports/CsvExportService.cs`, `src/Stokin.Infrastructure/Reports/CsvWriter.cs`, `tests/Stokin.IntegrationTests/Reports/{CsvWriterTests,ExportAuthorizationTests}.cs`.

**Interfaces:** Produces `ExportAsync(SessionContext, ReportSnapshot snapshot, Stream destination, CancellationToken): Task<ExportMetadata>`; `ExportMetadata(string SuggestedFileName, DateTimeOffset ExportedAt, Guid SnapshotId, long RowCount)`. CsvWriter.WriteAsync menerima typed columns/rows dan tidak membaca database lagi. Tes CSV berada di integration project agar Domain.Tests tidak bergantung pada Infrastructure Windows.

- [ ] **Step 1:** Tulis tes UTF-8/BOM nama Indonesia, teks `=1+1`, ` +SUM(A1)`, `-cmd`, `@x`, koma, petik, CRLF; assert string formula diawali apostrof, escaping benar, numerik `-12.3456` tetap numerik tanpa apostrof/ribuan. Tes snapshot dibuat sebelum transaksi baru lalu ekspor tetap angka snapshot. Turunkan Admin menjadi Petugas setelah load: assert AuthorizationException dan stream Length0 untuk snapshot berbiaya.
- [ ] **Step 2:** Jalankan `dotnet test STOKIN.sln --filter "CsvWriterTests|ExportAuthorizationTests"`; periksa red.
- [ ] **Step 3:** Implementasikan write streaming ke file sementara, periksa izin/status/sesi/snapshot owner sebelum output dan sebelum finalisasi file. Snapshot berbiaya dari role lama ditolak; Petugas tidak dapat mengakali kolom. Metadata toko/waktu/filter terpelihara, waktu ISO8601 offset, nilai empat desimal invariant, escaping typed dan audit ekspor. UI memindahkan file sementara hanya setelah sukses, lalu cleanup pada gagal.
- [ ] **Step 4:** Jalankan tes yang sama serta Petugas mengekspor penerimaan sendiri tetap tanpa biaya. Round-trip CSV harus mempertahankan teks multiline.
- [ ] **Step 5:** Catat AT27/28/31/32 dan checkpoint `feat: export authorized report snapshots to safe CSV`.

### Task 12: Fondasi WPF, setup/login, navigasi, dan sesi terkunci

**Files:** Create `src/Stokin.Desktop/{App.xaml,App.xaml.cs,MainWindow.xaml,MainWindow.xaml.cs}`, serta `Themes/{Colors,Controls}.xaml`, `ViewModels/{ObservableObject,AsyncCommand,ShellViewModel,SetupViewModel,LoginViewModel}.cs`, `Views/{SetupView,LoginView,ShellView}.xaml` beserta code-behind tipis, dan `Services/{NavigationService,DialogService,SessionActivityService,DraftMemoryStore}.cs` di bawah proyek Desktop. Create `tests/Stokin.IntegrationTests/Desktop/SessionStateTests.cs`; tambahkan reference proyek Desktop ke IntegrationTests.

**Interfaces:** Produces `AsyncCommand(Func<CancellationToken,Task> execute, Func<bool>? canExecute)` dengan IsRunning; `NavigationService.NavigateAsync(string route, CancellationToken): Task<bool>` menanyakan dirty state; `DraftMemoryStore.Store(Guid sessionId, string route, object state)` dan `TryRestore(Guid sessionId, string route): object?`. SessionActivityService memakai IClock dan IdentityService, memisahkan user activity dari auto-refresh. Semua viewmodel mempunyai IsBusy, ErrorMessage, IsDirty.

- [ ] **Step 1:** Tulis tes state terkunci ketika 15 menit, input A tersembunyi dari B, A login ulang dapat melanjutkan input A, logout menutup data/ekspor, command async tidak berjalan dua kali. Assert background refresh tidak memperpanjang sesi.
- [ ] **Step 2:** Jalankan tes SessionStateTests; pastikan gagal karena perilaku belum dibuat.
- [ ] **Step 3:** Implementasikan composition root, resource theme terang/biru gelap, setup+login sebelum shell, role-based menu, keyboard, dirty confirmation, loading/error/empty, PasswordBox tanpa logging, sesi lock overlay, dan state in-memory terikat pemilik. Struktur view code-behind hanya event UI/akses PasswordBox; bisnis di layanan. Jangan menghubungkan UI ke repository langsung.
- [ ] **Step 4:** Jalankan tes state dan `dotnet build src/Stokin.Desktop`; jalankan aplikasi, verifikasi setup kosong dan login/logout/lock benar. Jika GUI tak dapat dikendalikan oleh tool tersedia, dokumentasikan build-only dan checklist visual belum dijalankan.
- [ ] **Step 5:** Catat AT29/34 dan checkpoint `feat: add WPF shell setup login and session locking`.

### Task 13: Layar operasional dan laporan terhubung layanan nyata

**Files:** Create pasangan ViewModel/View untuk Dashboard, Products, ProductEditor, ProductDetail, Categories, Units, Documents, DocumentEditor, Corrections, Stocktakes, Reports pada direktori Desktop; `Behaviors/{NumericInput,UnsavedChanges}.cs`; `Services/PostingRecoveryCoordinator.cs`; tes viewmodel di `tests/Stokin.IntegrationTests/Desktop/WorkflowTests.cs`; `docs/acceptance/ui-checklist.md`.

**Interfaces:** Consumes seluruh API Task 5–11. Produces `PostingRecoveryCoordinator.SubmitAsync(SessionContext, PostDocumentRequest, CancellationToken): Task<PostingResult>` dan `ResolveAsync(SessionContext, Guid requestId, CancellationToken): Task<PostingResult?>`. State Unknown mempertahankan RequestId/payload, mencegah request baru sampai lookup selesai. Dashboard card membawa ProductFilter; export memakai ReportSnapshot aktif.

- [ ] **Step 1:** Tulis WorkflowTests create barang→draf→post→dashboard refresh→kartu stok, duplicate SKU editor ditolak, navigasi dirty bisa dibatalkan, gagal SQL mempertahankan input. Tes NumericInput budaya id-ID `1,25` → decimal1.25 untuk nominal, quantity pecahan ditolak, >4 desimal ditolak. Lost response → state Unknown → Resolve menghasilkan dokumen lama dan tidak submit request baru.
- [ ] **Step 2:** Jalankan `dotnet test tests/Stokin.IntegrationTests --filter WorkflowTests`; periksa kegagalan awal.
- [ ] **Step 3:** Implementasikan tabel pencarian/filter/sort/pagination50, editor master tanpa editable saldo, form multi-SKU, jumlah per satuan, konfirmasi biaya/quantity, histori read-only, koreksi preview+version, opname count/reload/post, laporan filter/periode/snapshot/export. Biaya Petugas sesuai FR20. Dashboard menandai stale pada gagal refresh dan menyediakan waktu/muat ulang. Gunakan cancellable debounce pencarian dengan pencegahan hasil query lama menimpa query baru.
- [ ] **Step 4:** Jalankan WorkflowTests dan build. Periksa UI aktual 1366×768 skala100/125, keyboard, kosong/error, status teks+warna, dan input tidak hilang. Rekam hasil aktual/screenshot bila fasilitas tersedia; jangan mengisi pass otomatis.
- [ ] **Step 5:** Catat AT01–28/32/34/35 yang benar-benar diuji dan checkpoint `feat: connect inventory workflows and reports to WPF screens`.

### Task 14: Pengguna, pengaturan, audit, backup, dan restore aman

**Files:** Create `src/Stokin.Application/Administration/{UserService,SettingsService,MaintenanceService,AdministrationContracts}.cs`, `src/Stokin.Infrastructure/Administration/{SqlUserRepository,SqlSettingsRepository,SqlMaintenanceRepository}.cs`, Desktop View/ViewModel untuk Users, Settings, Audit, BackupRestore; `tests/Stokin.IntegrationTests/Administration/{UserManagementTests,MaintenanceTests}.cs`.

**Interfaces:** Produces `SaveUserAsync(SessionContext, UserEdit, CancellationToken): Task<long>`, `ResetPasswordAsync(SessionContext, long userId, string newPassword, CancellationToken): Task`; `SaveSettingsAsync(SessionContext, StoreSettings, CancellationToken): Task`; `BackupAsync(SessionContext, string serverFilePath, CancellationToken): Task<BackupResult>`; `InspectBackupAsync(SessionContext, string serverFilePath, CancellationToken): Task<BackupInfo>`; `RestoreAsync(SessionContext, RestoreRequest, CancellationToken): Task<RestoreResult>`. RestoreRequest memuat backup path, target database identity, confirmation token hasil inspection, dan maintenance token. Audit list paginasi hanya Admin, tidak mempunyai API delete.

- [ ] **Step 1:** Tulis UserManagementTests dua Admin diubah bersamaan menyisakan tepat minimal satu aktif; akun beraktivitas tidak dapat dihapus, reset tidak menulis password ke audit. MaintenanceTests backup→mutasi baru→restore database uji terpisah→Q/V dan ledger kembali ke snapshot; path tak berizin menghasilkan failure result tanpa klaim sukses; restore tanpa maintenance/konfirmasi ditolak.
- [ ] **Step 2:** Jalankan `dotnet test tests/Stokin.IntegrationTests --filter "UserManagementTests|MaintenanceTests"`; catat kegagalan. SQL service memerlukan akses folder backup server; jika tak tersedia, tandai belum teruji dan tampilkan penyebab, jangan menurunkan keamanan sistem.
- [ ] **Step 3:** Implementasikan admin-last locking, audit akses/master, profil/zona waktu valid, backup SQL dengan parameter path, inspection metadata, mode pemeliharaan eksklusif, stop sesi lain, pre-restore backup, clear pools, restore lewat master, recovery dan rekonsiliasi. Identifier DB diambil dari identity yang divalidasi dan diquote, tidak diinterpolasi dari input bebas. Audit restore ditulis setelah pemulihan ke DB baru dan log operasional menyimpan jejak sebelum restore agar histori operasi tidak hilang bersama database lama.
- [ ] **Step 4:** Jalankan tes Administration pada database uji, lalu uji UI pengaturan/error path. Pastikan runtime harian tidak memerlukan sysadmin; izin maintenance terpisah dijelaskan dan diuji bila tersedia.
- [ ] **Step 5:** Catat AT29–31 dan checkpoint `feat: add account administration audit and database recovery`.

### Task 15: Operasi terjadwal, installer, dan panduan penggunaan

**Files:** Create `scripts/{Publish,Backup-Stokin,Register-BackupTask,Test-Reconciliation}.ps1`, `installer/{Stokin.iss,README.md}`, `docs/{setup,user-guide,backup-restore}.md`, `tests/Stokin.IntegrationTests/Deployment/DeploymentTests.cs`. Gunakan Inno Setup bila compiler tersedia; jika tidak tersedia, sediakan definisi+publish folder dan laporkan installer executable belum dibangun.

**Interfaces:** `Publish.ps1 -Configuration Release -Runtime win-x64`; `Backup-Stokin.ps1 -ServerInstance <instance> -Database <name> -BackupDirectory <serverPath>`; `Register-BackupTask.ps1` menerima pilihan akun/jadwal eksplisit dan tidak mendaftarkan task sistem selama tes; `Test-Reconciliation.ps1` membandingkan sum ledger Q/V dengan balances dan exit nonzero bila berbeda. Installer tidak membawa connection string atau password bawaan.

- [ ] **Step 1:** Tulis DeploymentTests untuk serialisasi konfigurasi tanpa rahasia, prasyarat hilang menghasilkan pesan/penghentian jelas, skrip rekonsiliasi mendeteksi mismatch, dan perencanaan retensi menyimpan7 harian+4 mingguan. Uji restart aplikasi hanya draf tersimpan tersedia dan sesi/input memori pengguna lama tidak muncul.
- [ ] **Step 2:** Jalankan `dotnet test tests/Stokin.IntegrationTests --filter DeploymentTests`; validasi parser PowerShell semua skrip, belum mengubah Windows Task Scheduler.
- [ ] **Step 3:** Implementasikan self-contained publish x64, installer preflight Windows/SQL/koneksi/versi, setup pertama, backup harian independen aplikasi, retensi berdasarkan file yang benar-benar diverifikasi berhasil, dan dokumentasi salinan media terpisah/restore drill. Dokumentasikan lokasi server vs klien. Registrasi task/instalasi dilakukan hanya pada lingkungan yang memang diotorisasi, tidak otomatis pada komputer pengguna ketika menjalankan unit test.
- [ ] **Step 4:** Jalankan `dotnet publish src/Stokin.Desktop -c Release -r win-x64 --self-contained true` dan smoke run binary terbit. Build installer jika compiler tersedia; catat output nyata dan unsigned status. Uji migrasi salinan+rekonsiliasi dan validasi script syntax.
- [ ] **Step 5:** Catat FR22 dan AT30/35/36, panduan owner/Petugas, dan checkpoint `build: package Windows app and document recovery operations`.

### Task 16: Dataset baseline, penerimaan menyeluruh, dan serah terima

**Files:** Create `tests/Stokin.IntegrationTests/Performance/BaselineTests.cs`, `scripts/Seed-Benchmark.ps1`, `docs/acceptance/{matrix,results,performance,known-limitations}.md`; modify hanya file produk yang terbukti gagal pada verifikasi.

**Interfaces:** Generator membuat 10.000 SKU dan 100.000 ledger melalui setup/seed terkendali pada database uji, tidak pada database produksi. Benchmark merekam versi OS/.NET/SQL, spesifikasi, warmup, 100 read/search, dan 100 posting≤50 baris. `p95 = sorted[ceil(0.95*N)-1]`. Hasil menyimpan input, aktual, expected, nomor dokumen, audit reference, durasi, dan bukti atau status belum diuji.

- [ ] **Step 1:** Tulis BaselineTests untuk generator menjaga ledger/balance consistency, sample-count100, dan penghitungan percentile pada data contoh `1..100` menghasilkan95. Buat matriks AT01–AT36 dari PRD dan hubungkan ke tes/manual checklist; status awal semuanya belum dijalankan, jangan mengasumsikan pass dari keberadaan fitur.
- [ ] **Step 2:** Jalankan `dotnet test tests/Stokin.IntegrationTests --filter BaselineTests` dan periksa kegagalan awal helper pengukuran.
- [ ] **Step 3:** Implementasikan generator/instrumentasi, lalu jalankan pengukuran lokal. p95 read≤2s dan post≤3s menjadi acceptance target, tidak di-hardcode sebagai hasil. LAN tetap nonaktif; tes lima sesi dijalankan terpisah hanya bila hendak mengaktifkan mode tersebut.
- [ ] **Step 4:** Jalankan `dotnet build STOKIN.sln -c Release`, `dotnet test STOKIN.sln -c Release`, publish, rekonsiliasi, tes SQL konflik/rollback/lost-response, dan checklist WPF. Saat ada kegagalan, gunakan systematic-debugging, perbaiki, lalu ulang hanya tes terdampak dan regresi yang relevan. Review independen dilakukan sesuai metode eksekusi yang dipilih pengguna; reviewer membaca spec, plan, kode, dan hasil nyata.
- [ ] **Step 5:** Lengkapi matriks AT01–AT36 dengan pass/fail/belum diuji dan alasan. Berikan lokasi executable/installer yang benar-benar dihasilkan, perintah jalan, petunjuk setup tanpa password default, tes aktual, dan keterbatasan material. Jangan menyebut P0 selesai bila acceptance wajib masih gagal atau belum diverifikasi. Checkpoint `docs: record STOKIN acceptance evidence and delivery`.

## Pemeriksaan rencana sebelum eksekusi

- [x] Seluruh bagian spec mempunyai tugas: arsitektur1; domain2; skema3; security4/12; master5; dokumen6/7; koreksi8; opname9; dashboard/laporan10; CSV11; UI12/13; admin/recovery14; deployment15; acceptance16.
- [x] Semua metode lintas tahap memakai UserId/ProductId bigint↔long, RequestId Guid, UTC DateTimeOffset, versi byte[], Task dan CancellationToken.
- [x] Kelima Review Focus mempunyai tes pada tugas pemiliknya.
- [x] Tidak ada penambahan P1/P2, API, ORM, default password, atau klaim performa tanpa pengukuran.
- [x] Rencana membedakan executable terbit, installer definition, dan installer executable yang benar-benar berhasil dibangun.
- [x] Kegagalan koneksi SQL awal, restore paket, build WPF, hak folder backup, dan alat visual dicatat sebagai kondisi yang harus dibuktikan, bukan diasumsikan berhasil.

## Metode eksekusi untuk dipilih pengguna

**Rekomendasi: Native.** Agen utama mengerjakan tugas 1–16 langsung dalam sesi ini, dengan pemeriksaan tiap tahap dan review independen akhir. Kontrak antarfitur saling bergantung; satu implementer mengurangi biaya perpindahan konteks dan menjaga konsistensi mesin pembukuan.

**Alternatif: Subagent-driven.** Implementer dan reviewer terpisah per tugas, lalu review akhir menyeluruh. Lebih banyak review independen dengan biaya konteks dan koordinasi lebih tinggi. Jangan melakukan delegasi sebelum pengguna memilih metode ini atau instruksi skill yang sudah berlaku memang memerlukannya.

Persetujuan rancangan teknis sudah diterima. Eksekusi menunggu pengguna meninjau rencana ini dan memilih metode sesuai handoff skill writing-plans.
