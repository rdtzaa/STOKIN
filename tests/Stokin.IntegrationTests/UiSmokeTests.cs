using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Stokin.Application;
using Stokin.Application.Common;
using Stokin.Desktop;
using Stokin.Desktop.Views;
using Stokin.Desktop.ViewModels;
using Stokin.Infrastructure;
using Stokin.Infrastructure.Database;
using Stokin.Testing;
namespace Stokin.IntegrationTests;

public class UiSmokeTests
{
    [Test]
    public void WPF_views_render_at_100_and_125_percent_logical_sizes()
    {
        Exception? failure = null; var thread = new Thread(() =>
        {
            try
            {
                var application = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; application.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Stokin.Desktop;component/Themes/Controls.xaml") });
                var service = new StokinService(new SqlStokinRepository(new SqlStore("Server=.;Database=Stokin;Integrated Security=true")));
                // Real modal lifecycle, off-screen. Obscuring content must not end ShowDialog.
                var modalModel = new EditorViewModel { Title = "Session regression", Save = _ => Task.CompletedTask }; var modal = new EditorWindow(modalModel) { WindowStartupLocation = WindowStartupLocation.Manual, Left = -30000, Top = -30000, ShowInTaskbar = false };
                Exception? modalFailure = null; var modalTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
                modalTimer.Tick += (_, _) => { modalTimer.Stop(); try { var hidden = Stokin.Desktop.Services.WindowSessionGuard.Obscure([modal]); Assert.True(modal.IsVisible, "Lock must preserve modal window"); Assert.Equal(Visibility.Hidden, ((UIElement)modal.Content).Visibility); Stokin.Desktop.Services.WindowSessionGuard.Restore(hidden); modalModel.Close!(); } catch (Exception ex) { modalFailure = ex; modal.Close(); } }; modalTimer.Start(); var modalResult = modal.ShowDialog(); if (modalFailure != null) throw modalFailure; Assert.Equal(true, modalResult);
                var sensitive = new TableWindow("Cost-bearing view", new DataTable(), "Admin costs") { WindowStartupLocation = WindowStartupLocation.Manual, Left = -30000, Top = -30000, ShowInTaskbar = false }; var revokeTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) }; revokeTimer.Tick += (_, _) => { revokeTimer.Stop(); Stokin.Desktop.Services.WindowSessionGuard.Revoke([sensitive]); }; revokeTimer.Start(); sensitive.ShowDialog(); Assert.True(!sensitive.IsVisible, "Revoked sensitive windows must close"); Assert.Null(sensitive.DataContext);
                string folder = Path.GetFullPath("output/ui"); Directory.CreateDirectory(folder);
                void Render(Window w, string name, int width, int height)
                {
                    var root = (FrameworkElement)w.Content; System.Windows.Documents.TextElement.SetForeground(root, (Brush)w.FindResource("Ink"));
                    for (int pass = 0; pass < 3; pass++) { root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout(); }
                    var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); var backdrop = new DrawingVisual(); using (var draw = backdrop.RenderOpen()) draw.DrawRectangle((Brush)w.FindResource("Canvas"), null, new Rect(0, 0, width, height)); bitmap.Render(backdrop); bitmap.Render(root); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); var path = Path.Combine(folder, name + ".png"); using (var file = File.Create(path)) encoder.Save(file); Assert.True(new FileInfo(path).Length > 5000, "View must contain rendered content");
                }
                Render(new ConnectionWindow(), "connection", 960, 650);
                Render(new LoginWindow(service, true), "setup", 480, 580);
                var main = new MainWindow();
                var reportRepository = System.Reflection.DispatchProxy.Create<IStokinRepository, ReportRepositoryDouble>(); var reportVm = new ShellViewModel(new(reportRepository), new(new(Guid.NewGuid(), 1), "Pemilik", UserRole.Admin), main); reportVm.NavigateCommand.Execute(reportVm.Navigation.First(n => n.Route == "Reports")); Assert.Equal(1, (int)reportVm.Table.Rows[0]["Stok"]); reportVm.RefreshCommand.Execute(null); Assert.Equal(2, (int)reportVm.Table.Rows[0]["Stok"]);
                var vm = new ShellViewModel(service, new(new(Guid.NewGuid(), 1), "Pemilik", UserRole.Admin), main); main.DataContext = vm;
                vm.Cards.Add(new("TOTAL BARANG", "128", "SKU aktif", "Products")); vm.Cards.Add(new("KATEGORI", "8", "kategori aktif", "Categories")); vm.Cards.Add(new("STOK MENIPIS", "12", "perlu perhatian", "Products")); vm.Cards.Add(new("STOK HABIS", "3", "butuh pengadaan", "Products")); vm.Cards.Add(new("NILAI PERSEDIAAN", "Rp18.500.000,00", "biaya perolehan", "Reports"));
                var table = new DataTable(); table.Columns.Add("SKU"); table.Columns.Add("Nama"); table.Columns.Add("Stok", typeof(int)); table.Columns.Add("Minimum", typeof(int)); table.Columns.Add("Status"); table.Rows.Add("BRG001", "Air Mineral 600 ml", 0, 5, "Habis"); table.Rows.Add("BRG002", "Sabun Cuci", 2, 5, "Menipis"); typeof(ShellViewModel).GetProperty("Table")!.SetValue(vm, table);
                Render(main, "dashboard-100", 1280, 720); Render(main, "dashboard-125", 1093, 614);
                var editor = new EditorViewModel { Title = "Tambah barang", Description = "Saldo berubah melalui dokumen yang dibukukan." }; editor.Fields.Add(EditorViewModel.Input("sku", "SKU *", "BRG003", true)); editor.Fields.Add(EditorViewModel.Input("name", "Nama barang *", "Biskuit", true)); editor.Fields.Add(EditorViewModel.Input("minimum", "Minimum *", "5", true)); Render(new EditorWindow(editor), "product-editor", 560, 650);
                var docvm = new DocumentEditorViewModel(service, () => vm.Context, "IN"); docvm.Lines.Add(new() { ProductId = 1, SKU = "BRG001", Name = "BRG001 · Air Mineral", Unit = "botol", Quantity = "10", Cost = "4000" }); Render(new DocumentEditorWindow(docvm), "receipt", 1100, 690);
                Console.WriteLine("UI renders: " + folder);
            }
            catch (Exception e) { failure = e; }
        }); thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join(); if (failure != null) throw new Exception("WPF render failed", failure);
    }
    [Test]
    public void Indonesian_decimal_input_rejects_grouping_and_integer_fraction()
    {
        var form = new EditorViewModel(); form.Fields.Add(EditorViewModel.Input("money", "Biaya", "1,25")); form.Fields.Add(EditorViewModel.Input("quantity", "Jumlah", "1,5")); Assert.Equal(1.25m, form.Money("money")!.Value); Assert.Throws<ArgumentException>(() => form.Integer("quantity")); form["money"].Value = "1.000,25"; Assert.Throws<ArgumentException>(() => form.Money("money"));
    }
    [Test]
    public async Task Async_command_blocks_double_execution()
    {
        int calls = 0; var signal = new TaskCompletionSource(); var command = new AsyncCommand(async () => { calls++; await signal.Task; }); command.Execute(null); command.Execute(null); Assert.Equal(1, calls); signal.SetResult(); await Task.Delay(10); Assert.True(!command.IsRunning);
    }
}
