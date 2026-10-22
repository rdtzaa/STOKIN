using Stokin.Desktop.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Stokin.Application;
using Stokin.Desktop.ViewModels;
using Stokin.Desktop.Views;
namespace Stokin.Desktop;

public partial class MainWindow : Window
{
    StokinService? service; ShellViewModel? vm; readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(20) }; DateTime lastActivity = DateTime.UtcNow, heartbeat = DateTime.UtcNow; bool locked, checking;
    public MainWindow() { InitializeComponent(); Width = Math.Min(1280, SystemParameters.WorkArea.Width); Height = Math.Min(800, SystemParameters.WorkArea.Height); Loaded += Start; InputManager.Current.PreProcessInput += Activity; timer.Tick += Tick; Closed += (_, _) => { timer.Stop(); InputManager.Current.PreProcessInput -= Activity; }; }
    async void Start(object sender, RoutedEventArgs e)
    {
        Loaded -= Start; var connection = new ConnectionWindow { Owner = this }; if (connection.ShowDialog() != true || connection.Service == null) { Close(); return; }
        service = connection.Service; await Login();
    }
    async Task Login()
    {
        if (service == null) return; try { var login = new LoginWindow(service, !await service.Operations.IsSetupAsync()) { Owner = this }; if (login.ShowDialog() != true || login.Session == null) { Close(); return; } vm = new(service, login.Session, this); vm.SessionExpired += () => _ = Lock(); vm.LogoutRequested += () => _ = LogoutAsync(); vm.SessionResetRequested += () => _ = ForceLogin(); DataContext = vm; lastActivity = DateTime.UtcNow; timer.Start(); await vm.Initialize(); } catch (Exception ex) { MessageBox.Show(this, UiError.Message(ex)); Close(); }
    }
    void Activity(object sender, PreProcessInputEventArgs e) { if (e.StagingItem.Input is KeyEventArgs or MouseButtonEventArgs or TextCompositionEventArgs) lastActivity = DateTime.UtcNow; }
    async void Tick(object? sender, EventArgs e)
    {
        if (locked || checking || vm == null || service == null) return; if (DateTime.UtcNow - lastActivity >= TimeSpan.FromMinutes(15)) { await Lock(); return; }
        checking = true; try { var role = await service.Operations.CheckAsync(vm.Context, activity: lastActivity > heartbeat); heartbeat = DateTime.UtcNow; if (role != vm.User.Role) { WindowSessionGuard.Revoke(OwnedWindows.Cast<Window>().ToArray()); vm.User = vm.User with { Role = role }; await vm.Initialize(); } } catch (Stokin.Application.Common.SessionExpiredException) { await Lock(); } catch { } finally { checking = false; }
    }
    async Task Lock()
    {
        if (locked || vm == null || service == null) return; locked = true; var windows = OwnedWindows.Cast<Window>().Where(w => w.IsVisible).ToArray(); var obscured = WindowSessionGuard.Obscure(windows); Root.Visibility = Visibility.Hidden;
        var login = new LoginWindow(service, false, vm.User.Username) { Owner = windows.LastOrDefault() ?? this }; if (login.ShowDialog() == true && login.Session != null) { bool changed = login.Session.Role != vm.User.Role; if (changed) WindowSessionGuard.Revoke(windows); vm.User = login.Session; Root.Visibility = Visibility.Visible; if (!changed) WindowSessionGuard.Restore(obscured); lastActivity = heartbeat = DateTime.UtcNow; locked = false; await Task.CompletedTask; } else { WindowSessionGuard.Revoke(windows); System.Windows.Application.Current.Shutdown(); }
    }
    async Task ForceLogin() { timer.Stop(); WindowSessionGuard.Revoke(OwnedWindows.Cast<Window>().ToArray()); DataContext = null; vm = null; await Login(); }
    async Task LogoutAsync()
    {
        if (vm == null || service == null) return; if (MessageBox.Show(this, "Keluar akun? Pastikan perubahan yang diperlukan sudah disimpan sebagai draf.", "Keluar STOKIN", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return; timer.Stop(); foreach (Window w in OwnedWindows.Cast<Window>().ToArray()) { w.Close(); if (w.IsVisible) { timer.Start(); return; } }
        try { await service.Operations.LogoutAsync(vm.Context); } catch { }
        DataContext = null; vm = null; await Login();
    }
    async void Logout(object sender, RoutedEventArgs e) => await LogoutAsync();
    void AutoColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e) => GridFormatting.Format(e);
    void OpenRow(object sender, MouseButtonEventArgs e) { if (vm != null && vm.Selected != null) vm.OpenCommand.Execute(null); }
    async void SortColumn(object sender, DataGridSortingEventArgs e) { if (vm?.IsProducts == true) { e.Handled = true; await vm.SortAsync(e.Column.Header.ToString() ?? "SKU"); } }
}