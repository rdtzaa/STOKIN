using System.Windows;
using Microsoft.Data.SqlClient;
using Stokin.Application;
using Stokin.Desktop.ViewModels;
using Stokin.Infrastructure.Security;
namespace Stokin.Desktop.Views;

public partial class ConnectionWindow : Window
{
    public StokinService? Service { get; private set; }
    public ConnectionWindow()
    {
        InitializeComponent(); var saved = ConnectionSettings.Load(); if (saved != null) try { var b = new SqlConnectionStringBuilder(saved); Server.Text = b.DataSource; Database.Text = b.InitialCatalog; Integrated.IsChecked = b.IntegratedSecurity; Username.Text = b.UserID; Password.Password = b.Password; Trust.IsChecked = b.TrustServerCertificate; } catch { }
    }
    async void Connect(object sender, RoutedEventArgs e)
    {
        ConnectButton.IsEnabled = false; Progress.Visibility = Visibility.Visible; Error.Text = "";
        try { var cs = ConnectionSettings.Build(Server.Text.Trim(), Database.Text.Trim(), Integrated.IsChecked == true, Username.Text.Trim(), Password.Password, Trust.IsChecked == true); Service = await ConnectionSettings.ConnectAsync(cs, Create.IsChecked == true); ConnectionSettings.Save(cs); DialogResult = true; }
        catch (Exception ex) { Error.Text = UiError.Message(ex); }
        finally { ConnectButton.IsEnabled = true; Progress.Visibility = Visibility.Collapsed; }
    }
}
