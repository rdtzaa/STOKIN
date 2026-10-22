using System.Data;
using System.Windows;
using Stokin.Application;
using Stokin.Application.Common;
namespace Stokin.Desktop.ViewModels;

public sealed class StocktakeViewModel : ObservableObject
{
    readonly StokinService app; readonly Func<SessionContext> session; StocktakeView current; string message = ""; bool busy, pending; readonly Guid requestId = Guid.NewGuid();
    public StocktakeViewModel(StokinService service, Func<SessionContext> context, StocktakeView take, bool admin)
    {
        app = service; session = context; current = take; Admin = admin; SaveCommand = new(() => Run(Save)); PostCommand = new(() => Run(Post)); ReloadCommand = new(() => Run(Reload)); CancelCommand = new(() => Run(Cancel)); current.Lines.RowChanged += (_, _) => Dirty = true;
    }
    public bool Admin { get; }
    public bool Open => current.Status == "OPEN" && !pending && !IsBusy; public string Title => $"{current.Number} · {current.Status}"; public DataView Lines => current.Lines.DefaultView; public bool Dirty { get; private set; }
    public Action? Close { get; set; }
    public string Message { get => message; set => Set(ref message, value); }
    public bool IsBusy { get => busy; private set { Set(ref busy, value); Changed(nameof(Open)); } }
    public AsyncCommand SaveCommand { get; }
    public AsyncCommand PostCommand { get; }
    public AsyncCommand ReloadCommand { get; }
    public AsyncCommand CancelCommand { get; }
    async Task Run(Func<Task> f) { if (IsBusy) return; IsBusy = true; Message = ""; try { await f(); } catch (Exception e) { Message = UiError.Message(e); } finally { IsBusy = false; Changed(nameof(Open)); } }
    void Use(StocktakeView value) { current = value; current.Lines.RowChanged += (_, _) => Dirty = true; Dirty = false; Changed(nameof(Lines)); Changed(nameof(Title)); Changed(nameof(Open)); }
    async Task Save()
    {
        if (pending) throw new ArgumentException("Periksa pembukuan sebelum mengubah hitungan."); var counts = new List<StocktakeCount>(); foreach (DataRow row in current.Lines.Rows) { row.EndEdit(); if (row["Fisik"] is DBNull) continue; counts.Add(new(Convert.ToInt64(row["ProductId"]), Convert.ToInt32(row["Fisik"]), row["Alasan"] as string, Admin && row["BiayaUnit"] is decimal d ? d : null)); }
        Use(await app.Operations.CountStocktakeAsync(session(), current.Id, current.Version, counts)); Message = "Hasil hitung tersimpan. Saldo belum berubah.";
    }
    async Task Post()
    {
        if (!pending) { await Save(); if (MessageBox.Show("Tinjau seluruh selisih lalu bukukan opname ini? Saldo acuan yang berubah akan ditolak.", "Konfirmasi opname", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return; pending = true; }
        try { Use(await app.Operations.PostStocktakeAsync(session(), current.Id, current.Version, requestId)); pending = false; Close?.Invoke(); }
        catch (Exception e) when (PostingFailure.IsDefinite(e)) { pending = false; throw; }
        catch (Exception e) { Message = "Hasil belum diketahui. Tekan Bukukan untuk memeriksa/mengulang RequestId yang sama: " + requestId + ". " + UiError.Message(e); }
    }
    async Task Reload() { if (pending) throw new ArgumentException("Periksa hasil pembukuan dahulu."); if (MessageBox.Show("Acuan barang yang berubah akan dimuat ulang. Hitungan baris tersebut harus diisi ulang.", "Muat ulang acuan", MessageBoxButton.YesNo) == MessageBoxResult.Yes) Use(await app.Operations.ReloadStocktakeAsync(session(), current.Id)); }
    async Task Cancel() { if (MessageBox.Show("Batalkan sesi opname terbuka ini?", "Batalkan opname", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { await app.Operations.CancelStocktakeAsync(session(), current.Id, current.Version); Dirty = false; Close?.Invoke(); } }
}
