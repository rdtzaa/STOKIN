using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Stokin.Application.Common;
using Stokin.Infrastructure.Security;
namespace Stokin.Desktop.ViewModels;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null) { if (EqualityComparer<T>.Default.Equals(field, value)) return false; field = value; Changed(name); return true; }
    protected void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
public sealed class AsyncCommand(Func<Task> execute, Func<bool>? allowed = null) : ObservableObject, ICommand
{
    bool running; public bool IsRunning { get => running; private set { Set(ref running, value); CanExecuteChanged?.Invoke(this, EventArgs.Empty); } }
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !IsRunning && (allowed?.Invoke() ?? true);
    public async void Execute(object? parameter) { if (!CanExecute(parameter)) return; IsRunning = true; try { await execute(); } finally { IsRunning = false; } }
}
public sealed class RelayCommand(Action<object?> execute) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true; public void Execute(object? parameter) => execute(parameter);
}
public record Choice(long Id, string Name, string Unit = "") { public override string ToString() => Name; }
public sealed class Field : ObservableObject
{
    string value = "", error = ""; Choice? selected;
    public string Key { get; init; } = ""; public string Label { get; init; } = ""; public bool Required { get; init; }
    public bool ReadOnly { get; init; }
    public bool Secret { get; init; }
    public IReadOnlyList<Choice>? Options { get; init; }
    public bool IsChoice => Options != null;
    public string Value { get => value; set { Set(ref this.value, value); Error = ""; } }
    public Choice? Selected { get => selected; set { Set(ref selected, value); Error = ""; } }
    public string Error { get => error; set => Set(ref error, value); }
}
public static class UiError
{
    public static string Message(Exception e)
    {
        if (e is OverflowException) return "Jumlah atau nilai melampaui batas penyimpanan. Kurangi input dan simpan ulang draf.";
        if (e is ValidationException or AuthorizationException or ConcurrencyException or SessionExpiredException or ArgumentException) return e.Message;
        var id = ConnectionSettings.Log(e);
        if (e is Microsoft.Data.SqlClient.SqlException se && se.Number is 2601 or 2627) return "Kode atau nama sudah dipakai. Gunakan nilai yang unik.";
        return $"Operasi belum berhasil. Periksa koneksi dan izin SQL Server. Input tetap tersedia. Kode bantuan: {id[..10]}";
    }
}
