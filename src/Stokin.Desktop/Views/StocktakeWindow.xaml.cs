using Stokin.Desktop.Services;
using System.Windows;
using System.Windows.Controls;
using Stokin.Application;
using Stokin.Application.Common;
using Stokin.Desktop.ViewModels;
namespace Stokin.Desktop.Views;

public partial class StocktakeWindow : Window, IRevocableWindow
{
    bool revoked;
    public void RevokeAccess() { revoked = true; DataContext = null; Close(); }

    readonly StocktakeViewModel vm;
    public StocktakeWindow(StokinService service, Func<SessionContext> session, StocktakeView take, bool admin) { InitializeComponent(); vm = new(service, session, take, admin); DataContext = vm; vm.Close = () => { if (!revoked && IsVisible) DialogResult = true; }; Closing += (_, e) => { Grid.CommitEdit(DataGridEditingUnit.Cell, true); Grid.CommitEdit(DataGridEditingUnit.Row, true); if (!revoked && vm.Dirty && MessageBox.Show(this, "Tinggalkan hitungan yang belum disimpan?", "Perubahan belum disimpan", MessageBoxButton.YesNo) != MessageBoxResult.Yes) e.Cancel = true; }; }
    bool CommitCells() { if (Grid.CommitEdit(DataGridEditingUnit.Cell, true) && Grid.CommitEdit(DataGridEditingUnit.Row, true)) return true; vm.Message = "Perbaiki jumlah fisik atau biaya yang tidak valid sebelum menyimpan."; return false; }
    void SaveClicked(object sender, RoutedEventArgs e) { if (CommitCells()) vm.SaveCommand.Execute(null); }
    void PostClicked(object sender, RoutedEventArgs e) { if (CommitCells()) vm.PostCommand.Execute(null); }
    void AutoColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e) { GridFormatting.Format(e); e.Column.IsReadOnly = e.PropertyName is not ("Fisik" or "Alasan") && !(vm.Admin && e.PropertyName == "BiayaUnit"); }
}
