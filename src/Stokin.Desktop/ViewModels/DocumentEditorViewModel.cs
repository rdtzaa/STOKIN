using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Windows;
using Stokin.Application;
using Stokin.Application.Common;
namespace Stokin.Desktop.ViewModels;

public sealed class DocumentLineViewModel : ObservableObject
{
    public long ProductId { get; init; }
    public string SKU { get; init; } = ""; public string Name { get; init; } = ""; public string Unit { get; init; } = "";
    string q = "1", cost = "", direction = "IN", target = "", basis = "";
    public string Quantity { get => q; set => Set(ref q, value); }
    public string Cost { get => cost; set => Set(ref cost, value); }
    public string Direction { get => direction; set => Set(ref direction, value); }
    public string Target { get => target; set { Set(ref target, value); Changed(nameof(PreviewText)); } }
    public string Basis { get => basis; set => Set(ref basis, value); }
    public BalancePreview? Preview { get; set; }
    public string PreviewText => Preview == null ? "Belum ada pratinjau saldo" : $"Q {Preview.Quantity} | Nilai {Preview.Value:N2} | Rata-rata {Preview.AverageCost:N2}" + (decimal.TryParse(Target, NumberStyles.Number, CultureInfo.GetCultureInfo("id-ID"), out var v) ? $" | Selisih {v - Preview.Value:N2} | Rata-rata baru {(Preview.Quantity > 0 ? v / Preview.Quantity : 0):N2}" : "");
    public void RefreshPreview() => Changed(nameof(PreviewText));
}
public sealed class DocumentEditorViewModel : ObservableObject
{
    readonly StokinService app; readonly Func<SessionContext> session; DocumentView? current; PostDocumentRequest? pending;
    public string Type { get; }
    public string Title => Type switch { "IN" => "Penerimaan barang", "OUT" => "Pengeluaran barang", "OPENING" => "Saldo awal barang", "VALUE_ADJUSTMENT" => "Koreksi nilai persediaan", _ => "Penyesuaian kuantitas" };
    public bool IsValue => Type == "VALUE_ADJUSTMENT"; public bool IsQuantity => Type == "QUANTITY_ADJUSTMENT"; public bool NeedsReference => IsValue || IsQuantity;
    public bool CostsVisible => Type is "IN" or "OPENING" or "QUANTITY_ADJUSTMENT"; public bool CanEdit => pending == null && !IsBusy;
    public ObservableCollection<DocumentLineViewModel> Lines { get; } = []; public ObservableCollection<Choice> Products { get; } = []; public Choice? SelectedProduct { get; set; }
    public DocumentLineViewModel? SelectedLine { get; set; }
    long editRevision; void MarkDirty() { Dirty = true; editRevision++; }
    string search = "", reason = "", external = "", reference = "", message = ""; bool busy;
    public string Search { get => search; set => Set(ref search, value); }
    public string Reason { get => reason; set { Set(ref reason, value); MarkDirty(); } }
    public string External { get => external; set { Set(ref external, value); MarkDirty(); } }
    public string ReferenceId { get => reference; set { Set(ref reference, value); MarkDirty(); } }
    public string Message { get => message; private set => Set(ref message, value); }
    public bool IsBusy { get => busy; private set { Set(ref busy, value); Changed(nameof(CanEdit)); } }
    public bool Dirty { get; private set; }
    public Action? Close { get; set; }
    public string Number => current?.Number ?? "DRAF BARU";
    public AsyncCommand SearchCommand { get; }
    public AsyncCommand AddCommand { get; }
    public AsyncCommand PreviewCommand { get; }
    public AsyncCommand SaveCommand { get; }
    public AsyncCommand PostCommand { get; }
    public AsyncCommand RecoverCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public DocumentEditorViewModel(StokinService service, Func<SessionContext> context, string type, DocumentView? existing = null)
    {
        Lines.CollectionChanged += (_, e) => { if (e.NewItems != null) foreach (DocumentLineViewModel line in e.NewItems) line.PropertyChanged += (_, _) => MarkDirty(); MarkDirty(); };
        app = service; session = context; Type = type; current = existing; reason = existing?.Reason ?? ""; external = existing?.ExternalReference ?? ""; reference = existing?.ReferenceDocumentId?.ToString() ?? "";
        SearchCommand = new(() => Run(LoadProducts)); AddCommand = new(() => Run(Add)); PreviewCommand = new(() => Run(Preview)); SaveCommand = new(() => Run(Save)); PostCommand = new(() => Run(Post)); RecoverCommand = new(() => Run(Recover)); RemoveCommand = new(_ => { if (pending == null && SelectedLine != null) { Lines.Remove(SelectedLine); MarkDirty(); } });
        if (existing != null) foreach (var l in existing.Lines) AddLine(new() { ProductId = l.ProductId, SKU = existing.Display.Rows[existing.Lines.ToList().IndexOf(l)]["SKU"].ToString()!, Name = existing.Display.Rows[existing.Lines.ToList().IndexOf(l)]["Nama"].ToString()!, Unit = existing.Display.Rows[existing.Lines.ToList().IndexOf(l)]["Satuan"].ToString()!, Quantity = l.Quantity.ToString(), Cost = l.UnitCost?.ToString(CultureInfo.GetCultureInfo("id-ID")) ?? "", Direction = l.Direction ?? "IN", Target = l.TargetValue?.ToString(CultureInfo.GetCultureInfo("id-ID")) ?? "", Basis = l.CalculationBasis ?? "", Preview = l.BalanceVersion != null ? new(l.ProductId, l.SnapshotQuantity ?? 0, l.SnapshotValue ?? 0, l.BalanceVersion) : null });
        Dirty = false;
    }
    async Task Run(Func<Task> work) { if (IsBusy) return; IsBusy = true; Message = ""; try { await work(); } catch (Exception e) { Message = UiError.Message(e); } finally { IsBusy = false; } }
    public async Task LoadProducts() { var p = await app.Operations.ProductsAsync(session(), new(Search), new(1, 50)); Products.Clear(); foreach (var r in p.Items) Products.Add(new(Convert.ToInt64(r["Id"]), $"{r["SKU"]} · {r["Nama"]} ({r["Satuan"]})", r["Satuan"].ToString()!)); }
    void AddLine(DocumentLineViewModel l) { Lines.Add(l); }
    async Task Add() { if (pending != null) return; if (SelectedProduct == null) throw new ArgumentException("Pilih barang dari hasil pencarian."); if (Lines.Any(l => l.ProductId == SelectedProduct.Id)) throw new ArgumentException("SKU sudah ada. Ubah jumlah baris yang tersedia."); var l = new DocumentLineViewModel { ProductId = SelectedProduct.Id, SKU = SelectedProduct.Name.Split('·')[0].Trim(), Name = SelectedProduct.Name, Unit = SelectedProduct.Unit, Quantity = IsValue ? "0" : "1" }; AddLine(l); MarkDirty(); if (IsValue) { l.Preview = await app.Operations.PreviewAsync(session(), l.ProductId); l.RefreshPreview(); } }
    async Task Preview() { if (SelectedLine == null) throw new ArgumentException("Pilih baris yang akan dipratinjau."); SelectedLine.Preview = await app.Operations.PreviewAsync(session(), SelectedLine.ProductId); SelectedLine.RefreshPreview(); MarkDirty(); }
    static decimal? Money(string text, bool optional = false) { if (optional && string.IsNullOrWhiteSpace(text)) return null; if (!decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.GetCultureInfo("id-ID"), out var v)) throw new ArgumentException("Nominal: gunakan koma untuk desimal, tanpa ribuan."); return v; }
    async Task Save()
    {
        if (pending != null) throw new ArgumentException("Periksa hasil pembukuan sebelum mengubah draf."); long savedRevision = editRevision; var list = new List<DocumentLine>();
        foreach (var l in Lines) { if (!int.TryParse(l.Quantity, out var q)) throw new ArgumentException("Jumlah wajib bilangan bulat."); list.Add(new(l.ProductId, q, CostsVisible ? Money(l.Cost, true) : null, IsQuantity ? l.Direction : null, IsValue ? Money(l.Target) : null, IsValue ? l.Preview?.Version : null, IsValue ? l.Basis : null, IsValue ? l.Preview?.Quantity : null, IsValue ? l.Preview?.Value : null)); }
        long? referenceId = long.TryParse(ReferenceId, out var id) ? id : null; current = await app.SaveDraftAsync(session(), new(current?.Id, Type, Reason, External, referenceId, list, current?.Version)); if (editRevision == savedRevision) Dirty = false; Message = "Draf tersimpan. Saldo belum berubah."; Changed(nameof(Number));
    }
    async Task Post()
    {
        if (pending == null)
        {
            await Save(); var totals = current!.Lines.GroupBy(l => Lines.First(x => x.ProductId == l.ProductId).Unit).Select(g => $"{g.Sum(x => x.Quantity)} {g.Key}"); string summary = string.Join("; ", totals);
            if (Type is "IN" or "OPENING") summary += $"\nNilai penerimaan: Rp{current.Lines.Sum(l => l.Quantity * (l.UnitCost ?? 0)):N2}";
            if (IsValue) summary = "Jumlah tetap. " + string.Join("\n", Lines.Select(l => l.PreviewText + " | Target Rp" + l.Target));
            if (MessageBox.Show(summary + "\n\nBukukan dokumen ini? Dokumen tidak dapat diubah setelah dibukukan.", "Konfirmasi pembukuan", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            pending = new(current.Id, current.Version, Guid.NewGuid(), DocumentFingerprint.Compute(current)); Changed(nameof(CanEdit));
        }
        try { await app.Operations.PostAsync(session(), pending); Dirty = false; pending = null; Close?.Invoke(); }
        catch (Exception e) when (PostingFailure.IsDefinite(e)) { pending = null; Changed(nameof(CanEdit)); throw; }
        catch (Exception e) { Message = $"Hasil belum diketahui. RequestId: {pending!.RequestId}. Gunakan Periksa Hasil. " + UiError.Message(e); }
    }
    async Task Recover() { if (pending == null) { Message = "Tidak ada pembukuan yang menunggu pemeriksaan."; return; } var result = await app.Operations.FindPostingAsync(session(), pending.RequestId); if (result != null) { Dirty = false; Close?.Invoke(); } else Message = "RequestId belum ditemukan. Tekan Bukukan untuk mengirim ulang permintaan yang sama; saldo tidak akan digandakan."; }
}
