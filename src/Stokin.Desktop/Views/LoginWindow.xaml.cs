using System.Windows;
using Stokin.Application;
using Stokin.Desktop.ViewModels;
namespace Stokin.Desktop.Views;

public partial class LoginWindow : Window
{
    readonly StokinService service; readonly bool setup; public UserSession? Session { get; private set; }
    public LoginWindow(StokinService app, bool firstSetup, string? lockedUsername = null)
    {
        InitializeComponent(); service = app; setup = firstSetup; if (setup) { StorePanel.Visibility = Visibility.Visible; Heading.Text = "Siapkan toko pertama"; Description.Text = "Buat Admin pertama. Kata sandi 10–128 karakter; tidak ada kata sandi bawaan."; LoginButton.Content = "Buat toko dan masuk"; }
        if (lockedUsername != null) { Username.Text = lockedUsername; Username.IsReadOnly = true; Heading.Text = "Sesi terkunci"; Description.Text = "Login kembali untuk melanjutkan input Anda."; }
        Loaded += (_, _) => { if (lockedUsername != null) Password.Focus(); else Username.Focus(); };
    }
    async void Login(object sender, RoutedEventArgs e)
    {
        LoginButton.IsEnabled = false; Error.Text = ""; try { if (setup) await service.Operations.SetupAsync(new(Store.Text, Username.Text, Password.Password)); Session = await service.Operations.LoginAsync(Username.Text, Password.Password); Password.Clear(); DialogResult = true; } catch (Exception ex) { Error.Text = UiError.Message(ex); } finally { LoginButton.IsEnabled = true; }
    }
}
