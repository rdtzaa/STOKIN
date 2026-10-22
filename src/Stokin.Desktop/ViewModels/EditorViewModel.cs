using System.Collections.ObjectModel;
using System.Globalization;
namespace Stokin.Desktop.ViewModels;

public sealed class EditorViewModel : ObservableObject
{
    public string Title { get; init; } = ""; public string Description { get; init; } = ""; public ObservableCollection<Field> Fields { get; } = [];
    string error = ""; bool busy; public string Error { get => error; private set => Set(ref error, value); }
    public bool IsBusy { get => busy; private set => Set(ref busy, value); }
    public bool Saved { get; private set; }
    public bool Dirty { get; private set; }
    public Action? Close { get; set; }
    public Func<EditorViewModel, Task> Save { get; init; } = _ => Task.CompletedTask;
    public AsyncCommand SaveCommand => new(async () =>
    {
        Error = ""; bool valid = true; foreach (var f in Fields) if (f.Required && (f.IsChoice ? f.Selected == null : string.IsNullOrWhiteSpace(f.Value))) { f.Error = "Kolom ini wajib diisi."; valid = false; }
        if (!valid) return;
        IsBusy = true; try { await Save(this); Saved = true; Close?.Invoke(); } catch (Exception e) { Error = UiError.Message(e); } finally { IsBusy = false; }
    });
    public void Track() { foreach (var f in Fields) f.PropertyChanged += (_, e) => { if (e.PropertyName is "Value" or "Selected") Dirty = true; }; }
    public Field this[string key] => Fields.First(x => x.Key == key);
    public string Text(string key) => this[key].Value.Trim();
    public long Choice(string key) => this[key].Selected?.Id ?? 0;
    public int Integer(string key)
    {
        if (int.TryParse(Text(key), NumberStyles.Integer, CultureInfo.GetCultureInfo("id-ID"), out int v)) return v; this[key].Error = "Masukkan bilangan bulat yang valid."; throw new ArgumentException(this[key].Label + ": bilangan bulat tidak valid.");
    }
    public decimal? Money(string key, bool optional = false)
    {
        if (optional && Text(key) == "") return null;
        // Decimal comma is supported; thousands separators are deliberately rejected to avoid ambiguity.
        if (decimal.TryParse(Text(key), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.GetCultureInfo("id-ID"), out decimal v)) return v;
        this[key].Error = "Gunakan koma untuk desimal, tanpa pemisah ribuan."; throw new ArgumentException(this[key].Label + ": nominal tidak valid.");
    }
    public static Field Input(string key, string label, string value = "", bool required = false, bool readOnly = false, bool secret = false) => new() { Key = key, Label = label, Value = value, Required = required, ReadOnly = readOnly, Secret = secret };
    public static Field Select(string key, string label, IReadOnlyList<Choice> choices, long? selected = null, bool required = true) => new() { Key = key, Label = label, Options = choices, Selected = choices.FirstOrDefault(x => x.Id == selected), Required = required };
}
