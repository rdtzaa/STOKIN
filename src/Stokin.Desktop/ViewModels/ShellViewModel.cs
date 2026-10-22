using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using Stokin.Application;
using Stokin.Application.Common;
using Stokin.Desktop.Views;
namespace Stokin.Desktop.ViewModels;

public record NavigationItem(string Route, string Label, string Icon, bool Admin = false);
public record ActionItem(string Label, AsyncCommand Command);
public record SummaryCard(string Label, string Value, string Detail, string Route, string? Filter = null);
public sealed class ShellViewModel : ObservableObject
{
    readonly StokinService app; readonly Window owner; UserSession user; DataTable table = new(), latest = new(); bool busy, stale; string title = "Dashboard persediaan", message = "", caption = "", route = "Dashboard", search = "", sort = "SKU"; int page = 1; long total;
    ReportSnapshot? snapshot; string defaultReport = "CurrentStock";
    public ShellViewModel(StokinService service, UserSession session, Window window)
    {
        app = service; user = session; owner = window; RefreshCommand = new(() => Run(Refresh)); NavigateCommand = new(p => { if (p is NavigationItem n) { route = n.Route; page = 1; _ = Run(Load); } }); CardCommand = new(p => { if (p is SummaryCard c) { route = c.Route; Status = c.Filter ?? "Semua"; defaultReport = c.Label == "NILAI PERSEDIAAN" ? "Valuation" : "CurrentStock"; if (route == "Reports") snapshot = null; page = 1; _ = Run(Load); } }); SearchCommand = new(() => { page = 1; return Run(Load); }); PrevCommand = new(() => { if (page > 1) page--; return Run(Load); }); NextCommand = new(() => { if (Table.Rows.Count == 50) page++; return Run(Load); }); OpenCommand = new(() => Run(OpenSelected)); BuildNavigation();
    }
    public UserSession User { get => user; set { if (user.Role != value.Role) { snapshot = null; defaultReport = "CurrentStock"; route = "Dashboard"; Table = new(); Cards.Clear(); } user = value; Changed(); Changed(nameof(Identity)); BuildNavigation(); } }
    public SessionContext Context => User.Context; public string Identity => $"{User.Username}  ·  {User.Role}";
    public ObservableCollection<NavigationItem> Navigation { get; } = []; public ObservableCollection<ActionItem> Actions { get; } = []; public ObservableCollection<SummaryCard> Cards { get; } = []; public ObservableCollection<Choice> Categories { get; } = [];
    public DataTable Table { get => table; private set { table = value; Changed(); Changed(nameof(View)); Changed(nameof(IsEmpty)); } }
    public DataView View => Table.DefaultView;
    public DataTable Latest { get => latest; private set { latest = value; Changed(); Changed(nameof(LatestView)); } }
    public DataView LatestView => Latest.DefaultView;
    public DataRowView? Selected { get; set; }
    public string Title { get => title; private set => Set(ref title, value); }
    public string Caption { get => caption; private set => Set(ref caption, value); }
    public string Message { get => message; private set => Set(ref message, value); }
    public bool IsBusy { get => busy; private set => Set(ref busy, value); }
    public bool Stale { get => stale; private set => Set(ref stale, value); }
    public bool IsDashboard => route == "Dashboard"; public bool IsProducts => route == "Products"; public bool IsEmpty => Table.Rows.Count == 0; public string Search { get => search; set => Set(ref search, value); }
    public Choice? Category { get; set; }
    public string Status { get; set; } = "Semua"; public string Active { get; set; } = "Aktif"; public string Sort { get => sort; set => Set(ref sort, value); }
    public string PageText => IsProducts ? $"Halaman {page} · {total:N0} barang · 50 per halaman" : $"Halaman {page} · {Table.Rows.Count:N0} baris";
    public string[] StatusOptions => ["Semua", "Aman", "Menipis", "Habis"]; public string[] ActiveOptions => ["Aktif", "Nonaktif", "Semua"]; public AsyncCommand RefreshCommand { get; }
    public RelayCommand NavigateCommand { get; }
    public RelayCommand CardCommand { get; }
    public AsyncCommand SearchCommand { get; }
    public AsyncCommand PrevCommand { get; }
    public AsyncCommand NextCommand { get; }
    public AsyncCommand OpenCommand { get; }
    public event Action? SessionExpired; public event Action? LogoutRequested; public event Action? SessionResetRequested;
    void BuildNavigation()
    {
        Navigation.Clear(); NavigationItem[] items = [new("Dashboard", "Dashboard", "▦"), new("Products", "Data Barang", "▤"), new("Categories", "Kategori", "◫"), new("Units", "Satuan", "◈", true), new("IN", "Barang Masuk", "↙"), new("OUT", "Barang Keluar", "↗"), new("OPENING", "Stok Awal", "⊞", true), new("Stocktakes", "Stok Opname", "☑"), new("QUANTITY_ADJUSTMENT", "Koreksi Jumlah", "±", true), new("VALUE_ADJUSTMENT", "Koreksi Nilai", "◇", true), new("Reports", "Laporan", "▥"), new("Users", "Pengguna", "♙", true), new("Settings", "Pengaturan", "⚙", true), new("Audit", "Audit", "≡", true)]; foreach (var n in items) if (!n.Admin || User.Role == UserRole.Admin) Navigation.Add(n);
    }
    public async Task Run(Func<Task> work)
    {
        if (IsBusy) return; IsBusy = true; Message = ""; try { await work(); Stale = false; } catch (SessionExpiredException) { SessionExpired?.Invoke(); } catch (Exception e) { Stale = true; Message = UiError.Message(e); } finally { IsBusy = false; Changed(nameof(PageText)); Changed(nameof(IsDashboard)); Changed(nameof(IsProducts)); }
    }
    void Action(string label, Func<Task> work) => Actions.Add(new(label, new(() => Run(work))));
    DataRow Row() => Selected?.Row ?? throw new ArgumentException("Pilih satu baris terlebih dahulu.");
    long SelectedId() => Convert.ToInt64(Row()["Id"]);
    static string Value(DataRow? r, string key) => r == null || r[key] is DBNull ? "" : r[key].ToString()!;
    static DataTable ToTable(IEnumerable<DataRow> rows) { var items = rows.ToArray(); if (items.Length == 0) return new(); var t = items[0].Table.Clone(); foreach (var r in items) t.ImportRow(r); return t; }
    async Task Refresh() { if (route == "Reports" && snapshot != null) snapshot = await app.Operations.ReportAsync(Context, snapshot.Query); await Load(); }
    public async Task Load()
    {
        Actions.Clear(); Cards.Clear(); Selected = null; Changed(nameof(Status)); Title = Navigation.FirstOrDefault(n => n.Route == route)?.Label ?? "STOKIN"; Caption = "Data toko Anda, dalam satu tempat.";
        if (route == "Dashboard")
        {
            var d = await app.Operations.DashboardAsync(Context); StoreTimeConverter.Zone = d.TimeZone; Cards.Add(new("TOTAL BARANG", d.TotalProducts.ToString("N0"), "SKU aktif", "Products")); Cards.Add(new("KATEGORI", d.TotalCategories.ToString("N0"), "kategori aktif", "Categories")); Cards.Add(new("STOK MENIPIS", d.LowStock.ToString("N0"), "perlu perhatian", "Products", "Menipis")); Cards.Add(new("STOK HABIS", d.OutOfStock.ToString("N0"), "butuh pengadaan", "Products", "Habis")); if (d.InventoryValue.HasValue) Cards.Add(new("NILAI PERSEDIAAN", "Rp" + d.InventoryValue.Value.ToString("N2"), "berdasarkan biaya perolehan", "Reports")); Table = d.Priority; Latest = d.Latest; Caption = $"Prioritas pengadaan · diperbarui {ReportPresentation.StoreTime(d.LoadedAt, d.TimeZone):dd MMM yyyy HH:mm:ss}"; return;
        }
        if (route == "Products")
        {
            if (Categories.Count == 0) { Categories.Add(new(0, "Semua kategori")); foreach (var c in await app.Operations.LookupsAsync(Context, "Categories")) Categories.Add(new(c.Id, c.Name)); Category = Categories[0]; Changed(nameof(Category)); }
            var products = await app.Operations.ProductsAsync(Context, new(Search, Category?.Id > 0 ? Category.Id : null, Status == "Semua" ? null : Status, Active == "Semua" ? null : Active == "Aktif"), new(page, 50, Sort)); Table = ToTable(products.Items); total = products.Total;
            if (User.Role == UserRole.Admin) { Action("+ Tambah barang", () => EditProduct(null)); Action("Ubah", () => EditProduct(Row())); Action("Hapus", () => Delete("Products")); }
            Action("Detail & kartu stok", ProductDetail); Action("Ekspor stok", async () => { snapshot = await app.Operations.ReportAsync(Context, new("CurrentStock", CategoryId: Category?.Id > 0 ? Category.Id : null, StockStatus: Status == "Semua" ? null : Status, IsActive: Active == "Semua" ? null : Active == "Aktif", Search: Search)); await Export(); });
        }
        else if (route is "Categories" or "Units")
        {
            var rows = await app.Operations.LookupsAsync(Context, route); var t = new DataTable(); t.Columns.Add("Id", typeof(long)); t.Columns.Add("Nama"); t.Columns.Add("Aktif", typeof(bool)); t.Columns.Add("Version", typeof(byte[])); foreach (var r in rows) t.Rows.Add(r.Id, r.Name, r.IsActive, r.Version); Table = t;
            if (User.Role == UserRole.Admin) { Action("+ Tambah", () => EditLookup(null)); Action("Ubah", () => EditLookup(Row())); Action("Hapus", () => Delete(route)); }
        }
        else if (route is "IN" or "OUT" or "OPENING" or "QUANTITY_ADJUSTMENT" or "VALUE_ADJUSTMENT")
        {
            Table = await app.Operations.ListDocumentsAsync(Context, route, page); Action("+ Dokumen baru", () => EditDocument(null)); Action("Buka dokumen", OpenDocument); Action("Batalkan draf", CancelDocument); if (User.Role == UserRole.Admin) Action("Catatan koreksi", AddNote); Caption = "Draf belum memengaruhi saldo. Dokumen dibukukan bersifat tetap. ID tersedia saat membuka dokumen.";
        }
        else if (route == "Stocktakes")
        {
            Table = await app.Operations.ListStocktakesAsync(Context, page); if (User.Role == UserRole.Admin) Action("+ Buka sesi", NewTake); Action("Isi / tinjau sesi", OpenTake);
        }
        else if (route == "Reports")
        {
            if (snapshot == null) snapshot = await app.Operations.ReportAsync(Context, new(defaultReport)); StoreTimeConverter.Zone = snapshot.TimeZone; Table = snapshot.Data; Caption = $"Snapshot {ReportPresentation.StoreTime(snapshot.AsOf, snapshot.TimeZone):dd MMM yyyy HH:mm:ss} · {snapshot.Query.Kind} · ekspor mengikuti snapshot ini"; Action("Pilih laporan & filter", ChooseReport); Action("Ekspor CSV", Export);
        }
        else if (route == "Users") { Table = await app.Operations.UsersAsync(Context); Action("+ Tambah akun", () => EditUser(null)); Action("Ubah / reset sandi", () => EditUser(Row())); }
        else if (route == "Settings")
        {
            var settings = await app.Operations.SettingsAsync(Context); var t = new DataTable(); t.Columns.Add("Pengaturan"); t.Columns.Add("Nilai"); foreach (var p in settings) t.Rows.Add(p.Key, p.Value); Table = t; Action("Profil toko", EditSettings); Action("Backup database", Backup); Action("Periksa / restore backup", Restore);
        }
        else if (route == "Audit") { Table = await app.Operations.AuditAsync(Context, page); Caption = "Audit bisnis hanya dapat ditambah. Gunakan halaman berikutnya untuk riwayat lama."; }
    }
    public async Task SortAsync(string column) { Sort = column; page = 1; await Run(Load); }
    public async Task Initialize() => await Run(Load);
    public void RequestLogout() => LogoutRequested?.Invoke();
    bool Confirm(string text) => MessageBox.Show(owner, text, "STOKIN", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    bool Form(EditorViewModel f) => new EditorWindow(f) { Owner = owner }.ShowDialog() == true;
    async Task EditProduct(DataRow? r)
    {
        var cats = (await app.Operations.LookupsAsync(Context, "Categories")).Where(x => x.IsActive).Select(x => new Choice(x.Id, x.Name)).ToArray(); var units = (await app.Operations.LookupsAsync(Context, "Units")).Where(x => x.IsActive).Select(x => new Choice(x.Id, x.Name)).ToArray();
        var f = new EditorViewModel { Title = r == null ? "Tambah barang" : "Ubah barang", Description = "Stok dan biaya hanya berubah melalui dokumen pembukuan.", Save = async m => { await app.SaveProductAsync(Context, new(r == null ? null : Convert.ToInt64(r["Id"]), m.Text("sku"), m.Text("name"), m.Choice("cat"), m.Choice("unit"), m.Integer("min"), m.Money("price", true), m.Text("notes"), m.Choice("active") == 1, r?["Version"] as byte[])); } };
        f.Fields.Add(EditorViewModel.Input("sku", "SKU *", Value(r, "SKU"), true)); f.Fields.Add(EditorViewModel.Input("name", "Nama barang *", Value(r, "Nama"), true)); f.Fields.Add(EditorViewModel.Select("cat", "Kategori *", cats, r == null ? null : Convert.ToInt64(r["CategoryId"]))); f.Fields.Add(EditorViewModel.Select("unit", "Satuan *", units, r == null ? null : Convert.ToInt64(r["UnitId"]))); f.Fields.Add(EditorViewModel.Input("min", "Batas stok minimum *", r == null ? "0" : Value(r, "Minimum"), true)); f.Fields.Add(EditorViewModel.Input("price", "Harga jual referensi (opsional)", Value(r, "HargaJual"))); f.Fields.Add(EditorViewModel.Input("notes", "Catatan", Value(r, "Catatan"))); f.Fields.Add(EditorViewModel.Select("active", "Status", [new(1, "Aktif"), new(0, "Nonaktif")], r == null || Convert.ToBoolean(r["Aktif"]) ? 1 : 0)); if (Form(f)) await Load();
    }
    async Task EditLookup(DataRow? r)
    {
        string kind = route; var f = new EditorViewModel { Title = "Kelola " + Title, Save = async m => { await app.Operations.SaveLookupAsync(Context, kind, r == null ? null : Convert.ToInt64(r["Id"]), m.Text("name"), m.Choice("active") == 1, r?["Version"] as byte[]); } }; f.Fields.Add(EditorViewModel.Input("name", "Nama *", Value(r, "Nama"), true)); f.Fields.Add(EditorViewModel.Select("active", "Status", [new(1, "Aktif"), new(0, "Nonaktif")], r == null || Convert.ToBoolean(r["Aktif"]) ? 1 : 0)); if (Form(f)) { Categories.Clear(); await Load(); }
    }
    async Task Delete(string kind) { var id = SelectedId(); if (!Confirm("Hapus master yang belum pernah digunakan ini?")) return; await app.Operations.DeleteMasterAsync(Context, kind, id); await Load(); }
    Task EditDocument(DocumentView? doc) { var vm = new DocumentEditorViewModel(app, () => Context, route, doc); new DocumentEditorWindow(vm) { Owner = owner }.ShowDialog(); return Load(); }
    async Task OpenDocument() { var d = await app.Operations.GetDocumentAsync(Context, SelectedId()); if (d.Status == "DRAFT") await EditDocument(d); else ShowTable($"{d.Number} · ID {d.Id} · {d.Status}", d.Display, $"{d.Reason} | Referensi {d.ExternalReference}"); }
    async Task CancelDocument() { var d = await app.Operations.GetDocumentAsync(Context, SelectedId()); if (Confirm("Batalkan draf " + d.Number + "?")) { await app.Operations.CancelDraftAsync(Context, d.Id, d.Version); await Load(); } }
    Task AddNote() { long id = SelectedId(); var f = new EditorViewModel { Title = "Catatan koreksi Admin", Description = "Catatan tidak mengubah dokumen, jumlah, nilai, atau ledger.", Save = async m => await app.Operations.AddCorrectionNoteAsync(Context, id, m.Text("note")) }; f.Fields.Add(EditorViewModel.Input("note", "Alasan / catatan *", required: true)); Form(f); return Task.CompletedTask; }
    async Task ProductDetail() { long id = SelectedId(); var report = await app.Operations.ReportAsync(Context, new("StockCard", ProductId: id, IsActive: null)); var selected = Row(); string detail = $"Stok {selected["Stok"]} {selected["Satuan"]} | Minimum {selected["Minimum"]} | Status {selected["Status"]}"; if (User.Role == UserRole.Admin) { var b = await app.Operations.PreviewAsync(Context, id); detail += $" | Nilai Rp{b.Value:N2} | Biaya rata-rata Rp{b.AverageCost:N2}"; } ShowTable("Kartu stok · " + Value(Row(), "SKU"), report.Data, detail); }
    async Task OpenSelected() { if (IsProducts) await ProductDetail(); else if (route == "Stocktakes") await OpenTake(); else if (route is "IN" or "OUT" or "OPENING" or "QUANTITY_ADJUSTMENT" or "VALUE_ADJUSTMENT") await OpenDocument(); }
    void ShowTable(string heading, DataTable data, string description = "") => new TableWindow(heading, data, description) { Owner = owner }.ShowDialog();
    async Task ChooseReport()
    {
        string[] kinds = User.Role == UserRole.Admin ? ["CurrentStock", "CriticalStock", "StockCard", "Transactions", "Valuation", "StocktakeResults"] : ["CurrentStock", "CriticalStock", "StockCard", "Transactions", "StocktakeResults"]; string[] names = ["Stok terkini", "Menipis / habis", "Kartu stok", "Riwayat transaksi", "Nilai persediaan", "Hasil opname"];
        var reportZone = snapshot?.TimeZone ?? "Asia/Jakarta"; var p = await app.Operations.ProductsAsync(Context, new(IsActive: null), new(1, 10000)); var productChoices = new List<Choice> { new(0, "Semua barang") }; productChoices.AddRange(p.Items.Select(r => new Choice(Convert.ToInt64(r["Id"]), $"{r["SKU"]} · {r["Nama"]}")));
        var cats = await app.Operations.LookupsAsync(Context, "Categories"); var f = new EditorViewModel
        {
            Title = "Laporan persediaan",
            Description = "Periode memakai waktu pembukuan, bukan tanggal nota. Akhir periode tidak disertakan.",
            Save = async m =>
        {
            DateTimeOffset? Date(string key) { if (m.Text(key) == "") return null; return StorePeriod.StartUtc(m.Text(key), reportZone); }
            long? Pick(string k) => m.Choice(k) == 0 ? null : m.Choice(k);
            var kind = kinds[(int)m.Choice("kind")]; snapshot = await app.Operations.ReportAsync(Context, new(kind, Pick("product"), Pick("cat"), m.Text("status") == "" ? null : m.Text("status"), m.Choice("active") == 2 ? null : m.Choice("active") == 1, Date("start"), Date("end"), m.Text("type") == "" ? null : m.Text("type"), long.TryParse(m.Text("user"), out var uid) ? uid : null, long.TryParse(m.Text("take"), out var sid) ? sid : null));
        }
        };
        f.Fields.Add(EditorViewModel.Select("kind", "Jenis laporan", kinds.Select((k, i) => new Choice(i, k switch { "Valuation" => "Nilai persediaan", "StocktakeResults" => "Hasil opname", "StockCard" => "Kartu stok", "Transactions" => "Riwayat transaksi", "CriticalStock" => "Menipis / habis", _ => "Stok terkini" })).ToArray(), 0)); f.Fields.Add(EditorViewModel.Select("product", "Barang (wajib untuk kartu stok)", productChoices, 0)); f.Fields.Add(EditorViewModel.Select("cat", "Kategori", new[] { new Choice(0, "Semua kategori") }.Concat(cats.Select(c => new Choice(c.Id, c.Name))).ToArray(), 0)); f.Fields.Add(EditorViewModel.Select("active", "Barang aktif", [new(1, "Aktif"), new(0, "Nonaktif"), new(2, "Semua")], 1)); f.Fields.Add(EditorViewModel.Input("status", "Status stok: Aman, Menipis, Habis (kosong = semua)")); f.Fields.Add(EditorViewModel.Input("start", "Awal periode yyyy-MM-dd (opsional)")); f.Fields.Add(EditorViewModel.Input("end", "Akhir eksklusif yyyy-MM-dd (opsional)")); f.Fields.Add(EditorViewModel.Input("type", "Jenis transaksi IN / OUT / OPENING / QUANTITY_ADJUSTMENT / VALUE_ADJUSTMENT")); f.Fields.Add(EditorViewModel.Input("user", "ID pembuat (opsional)")); f.Fields.Add(EditorViewModel.Input("take", "ID sesi opname (opsional)")); if (Form(f)) await Load();
    }
    async Task Export()
    {
        if (snapshot == null) throw new ArgumentException("Muat laporan terlebih dahulu."); string safe = new(snapshot.StoreName.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray()); var dlg = new SaveFileDialog { Filter = "CSV UTF-8 (*.csv)|*.csv", FileName = $"STOKIN_{safe}_{snapshot.Query.Kind}_{snapshot.AsOf:yyyyMMdd_HHmmss}.csv" }; if (dlg.ShowDialog(owner) != true) return;
        string temp = dlg.FileName + ".tmp"; try { await using (var file = File.Create(temp)) await app.ExportAsync(Context, snapshot, file); File.Move(temp, dlg.FileName, true); await File.WriteAllTextAsync(Path.ChangeExtension(dlg.FileName, "metadata.json"), System.Text.Json.JsonSerializer.Serialize(ReportPresentation.ExportMetadata(snapshot, DateTimeOffset.UtcNow), new System.Text.Json.JsonSerializerOptions { WriteIndented = true })); Message = "CSV berhasil disimpan dari snapshot laporan."; } finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    async Task NewTake()
    {
        var products = await app.Operations.ProductsAsync(Context, new(), new(1, 10000)); var f = new EditorViewModel { Title = "Buka sesi opname", Description = "Masukkan ID barang dipisahkan koma. Kosong berarti semua barang aktif. Hentikan sementara pergerakan fisik selama penghitungan.", Save = async m => { var ids = m.Text("ids") == "" ? products.Items.Select(r => Convert.ToInt64(r["Id"])).ToArray() : m.Text("ids").Split(',').Select(x => long.Parse(x.Trim())).ToArray(); await app.Operations.OpenStocktakeAsync(Context, ids); } }; f.Fields.Add(EditorViewModel.Input("ids", "ID barang (opsional)")); if (Form(f)) await Load();
    }
    async Task OpenTake() { var take = await app.Operations.GetStocktakeAsync(Context, SelectedId()); new StocktakeWindow(app, () => Context, take, User.Role == UserRole.Admin) { Owner = owner }.ShowDialog(); await Load(); }
    Task EditUser(DataRow? r)
    {
        var f = new EditorViewModel { Title = r == null ? "Tambah akun" : "Ubah akun / reset sandi", Description = "Admin aktif terakhir selalu dipertahankan. Kosongkan kata sandi untuk mempertahankan sandi lama.", Save = async m => await app.Operations.SaveUserAsync(Context, new(r == null ? null : Convert.ToInt64(r["Id"]), m.Text("name"), (UserRole)m.Choice("role"), m.Choice("active") == 1, m.Text("password") == "" ? null : m.Text("password"))) }; f.Fields.Add(EditorViewModel.Input("name", "Nama akun *", Value(r, "NamaAkun"), true)); f.Fields.Add(EditorViewModel.Select("role", "Peran", [new(1, "Admin"), new(2, "Petugas")], r != null && Value(r, "Peran") == "Admin" ? 1 : 2)); f.Fields.Add(EditorViewModel.Select("active", "Status", [new(1, "Aktif"), new(0, "Nonaktif")], r == null || Convert.ToBoolean(r["Aktif"]) ? 1 : 0)); f.Fields.Add(EditorViewModel.Input("password", "Kata sandi baru (10–128 karakter)", required: r == null, secret: true)); if (Form(f)) return Load(); return Task.CompletedTask;
    }
    async Task SaveProfile(EditorViewModel m) { await app.Operations.SaveSettingsAsync(Context, m.Text("store"), m.Text("zone")); StoreTimeConverter.Zone = m.Text("zone"); }
    async Task EditSettings() { var settings = await app.Operations.SettingsAsync(Context); var f = new EditorViewModel { Title = "Profil toko", Save = SaveProfile }; f.Fields.Add(EditorViewModel.Input("store", "Nama toko *", settings["StoreName"], true)); f.Fields.Add(EditorViewModel.Input("zone", "Zona waktu *", settings["TimeZone"], true)); if (Form(f)) await Load(); }
    Task Backup() { var f = new EditorViewModel { Title = "Backup database", Description = "Path berada di komputer SQL Server dan harus dapat ditulis akun layanan SQL Server. Gunakan lokasi/media terpisah.", Save = async m => await app.Operations.BackupAsync(Context, m.Text("path")) }; f.Fields.Add(EditorViewModel.Input("path", "Path server lengkap, contoh D:\\Backup\\stokin.bak", required: true)); if (Form(f)) return Load(); return Task.CompletedTask; }
    Task Restore()
    {
        var f = new EditorViewModel { Title = "Periksa dan pulihkan backup", Description = "Memasuki mode pemeliharaan akan menghentikan sesi lain. Backup sebelum restore akan dibuat di folder backup server yang sama.", Save = async m => { var info = await app.Operations.InspectBackupAsync(Context, m.Text("path")); if (!Confirm($"Database backup: {info.Database}\nWaktu backup: {info.FinishedAt}\nLokasi server: {info.ServerPath}\n\nLanjutkan penggantian data?")) throw new ArgumentException("Pemulihan dibatalkan."); await app.Operations.RestoreAsync(Context, info, m.Text("confirm")); } }; f.Fields.Add(EditorViewModel.Input("path", "Path backup di server *", required: true)); f.Fields.Add(EditorViewModel.Input("confirm", "Ketik PULIHKAN diikuti nama database tujuan, contoh PULIHKAN Stokin", required: true)); if (Form(f)) SessionResetRequested?.Invoke(); return Task.CompletedTask;
    }
}
