using Stokin.Desktop.Services;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
namespace Stokin.Desktop.Views;

public partial class TableWindow : Window, IRevocableWindow
{
    public void RevokeAccess() { Grid.ItemsSource = null; DataContext = null; Close(); }

    public TableWindow(string heading, DataTable data, string description) { InitializeComponent(); Title = heading; Heading.Text = heading; Description.Text = description; Grid.ItemsSource = data.DefaultView; }
    void CloseWindow(object sender, RoutedEventArgs e) => Close();
    void AutoColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e) => GridFormatting.Format(e);
}
public static class GridFormatting
{
    public static void Format(DataGridAutoGeneratingColumnEventArgs e)
    {
        if (e.PropertyType == typeof(byte[]) || e.PropertyName is "CategoryId" or "UnitId") { e.Cancel = true; return; }
        if (e.Column is DataGridTextColumn col)
        {
            col.MinWidth = e.PropertyName is "Nama" or "Barang" ? 220 : e.PropertyName == "SKU" ? 120 : 90; col.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
            if (e.PropertyType == typeof(decimal)) col.Binding = new Binding("[" + e.PropertyName + "]") { StringFormat = "N2" };
            if (e.PropertyType == typeof(DateTimeOffset)) col.Binding = new Binding("[" + e.PropertyName + "]") { Converter = new StoreTimeConverter() };
            if (e.PropertyName == "Status")
            {
                var style = new Style(typeof(TextBlock)); style.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
                foreach (var (v, c) in new[] { ("Habis", "#B13D3D"), ("Menipis", "#A46D0D"), ("Aman", "#126E65") }) { var t = new DataTrigger { Binding = new Binding("Text") { RelativeSource = new RelativeSource(RelativeSourceMode.Self) }, Value = v }; t.Setters.Add(new Setter(TextBlock.ForegroundProperty, (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(c)!)); style.Triggers.Add(t); }
                col.ElementStyle = style;
            }
        }
    }
}

public sealed class StoreTimeConverter : IValueConverter
{
    public static string Zone { get; set; } = "Asia/Jakarta";
    public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture) => value is DateTimeOffset instant ? Stokin.Application.ReportPresentation.StoreTime(instant, Zone).ToString("dd MMM yyyy HH:mm", culture) : "";
    public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new System.NotSupportedException();
}
