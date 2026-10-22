using Stokin.Desktop.Services;
using System.Windows;
using System.Windows.Controls;
using Stokin.Desktop.ViewModels;
namespace Stokin.Desktop.Views;

public partial class EditorWindow : Window, IRevocableWindow
{
    bool revoked;
    public void RevokeAccess() { revoked = true; DataContext = null; Close(); }

    readonly EditorViewModel vm;
    public EditorWindow(EditorViewModel model) { InitializeComponent(); vm = model; DataContext = vm; vm.Close = () => { if (!revoked && IsVisible) DialogResult = true; }; vm.Track(); Closing += (_, e) => { if (!revoked && vm.Dirty && !vm.Saved && MessageBox.Show(this, "Perubahan belum disimpan. Tinggalkan form?", "Perubahan belum disimpan", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) e.Cancel = true; }; }
    void PasswordChanged(object sender, RoutedEventArgs e) { if (sender is PasswordBox { DataContext: Field field } p) field.Value = p.Password; }
    void Cancel(object sender, RoutedEventArgs e) => Close();
}
