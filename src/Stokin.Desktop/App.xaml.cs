using System.Configuration;
using System.Data;
using System.Windows;

namespace Stokin.Desktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    public App() { System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("id-ID"); System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("id-ID"); }
}

