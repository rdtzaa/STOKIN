using Stokin.Desktop.Services;
using System.Windows;
using Stokin.Desktop.ViewModels;
namespace Stokin.Desktop.Views;

public partial class DocumentEditorWindow : Window, IRevocableWindow
{
    bool revoked;
    public void RevokeAccess() { revoked = true; DataContext = null; Close(); }

    public DocumentEditorWindow(DocumentEditorViewModel vm)
    {
        InitializeComponent(); DataContext = vm; vm.Close = () => { if (!revoked && IsVisible) DialogResult = true; }; Loaded += async (_, _) => { try { await vm.LoadProducts(); } catch (Exception e) { MessageBox.Show(this, UiError.Message(e)); } };
        // Costs are never shown for stock-out; corrections have their own preview columns.
        var grid = LinesGrid; grid.RowHeight = double.NaN; grid.Columns[1].Visibility = vm.IsValue ? Visibility.Collapsed : Visibility.Visible; grid.Columns[2].Visibility = vm.CostsVisible ? Visibility.Visible : Visibility.Collapsed; grid.Columns[3].Visibility = vm.IsQuantity ? Visibility.Visible : Visibility.Collapsed; for (int i = 4; i < 7; i++) grid.Columns[i].Visibility = vm.IsValue ? Visibility.Visible : Visibility.Collapsed;
        Closing += (_, e) => { if (!revoked && vm.Dirty && MessageBox.Show(this, "Perubahan belum disimpan. Tinggalkan form?", "Draf belum tersimpan", MessageBoxButton.YesNo) != MessageBoxResult.Yes) e.Cancel = true; };
    }
    static System.Windows.Controls.DataGrid? FindGrid(DependencyObject parent) { for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++) { var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i); if (child is System.Windows.Controls.DataGrid g) return g; var found = FindGrid(child); if (found != null) return found; } return null; }
}
