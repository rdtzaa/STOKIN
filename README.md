# STOKIN

Aplikasi inventaris toko Windows desktop dengan WPF/MVVM, C#, SQL Server, dan ADO.NET. Lingkup implementasi mengikuti PRD v1.1: barang/kategori/satuan, dashboard, stok awal, dokumen masuk/keluar, koreksi kuantitas/nilai, opname, kartu stok, laporan CSV, akun Admin/Petugas, audit, serta backup/restore.

## Jalankan

1. Gunakan Windows x64 dengan **.NET 8 Windows Desktop Runtime x64** dan SQL Server Express.
2. Klik dua kali `Jalankan-STOKIN.cmd` atau buka `output/app/Stokin.Desktop.exe` setelah publish, atau jalankan `dotnet run --project src/Stokin.Desktop` setelah restore.
3. Server awal `.\SQLEXPRESS`, database `Stokin`, dan Windows Authentication. Centang **Buat database baru** hanya untuk instalasi pertama; akun setup SQL memerlukan izin membuat database dan skema.
4. Buat nama toko serta Admin pertama dengan kata sandi 10–128 karakter. Tidak ada akun/kata sandi bawaan.
5. Buat kategori dan satuan sebelum membuat barang. Saldo awal dicatat melalui menu Stok Awal.

Detail: [setup](docs/setup.md), [panduan penggunaan](docs/user-guide.md), [backup dan restore](docs/backup-restore.md), [hasil verifikasi](docs/acceptance/results.md).

## Build dan pengujian

SDK yang dipatok: .NET 10.0.401. Proyek menargetkan .NET 8 untuk kompatibilitas dengan paket offline dan runtime lokal yang tersedia. Tidak memakai Entity Framework atau backend web.

```powershell
.\scripts\Build.ps1
.\scripts\Test.ps1
.\scripts\Publish.ps1
```

Build offline pada komputer ini:

```powershell
.\scripts\Build.ps1 -OfflinePackageSource "$env:USERPROFILE\.nuget\packages"
```

Tes menggunakan executable runner tanpa paket pengujian tambahan. **`dotnet test` bukan perintah untuk suite ini.** Tes SQL dilewati bila connection string uji tidak disediakan; skip bukan pass penerimaan.

```powershell
.\scripts\Test.ps1 -SqlTestConnection 'Server=.\SQLEXPRESS;Database=Stokin_Test_Acceptance;Integrated Security=true;Encrypt=true;TrustServerCertificate=true'
```

Fixture hanya membuat/menghapus database bernama `Stokin_Test_<GUID>`; jangan memakai database produksi. Uji memakai SQL Server nyata untuk konkurensi, rollback, idempotensi, redaksi biaya, koreksi, opname, dan snapshot laporan.

## Status validasi

Build dan tes tanpa SQL dapat dijalankan di lingkungan ini. Koneksi SQL lokal dari proses agen gagal dengan **Cannot generate SSPI context**; belum ada klaim P0 lulus seluruh penerimaan. Lihat matriks pengujian dan batasan deployment sebelum memakai data toko nyata.

Paket self-contained membutuhkan runtime packs yang belum tersedia offline. `Publish.ps1 -SelfContained` disediakan untuk lingkungan dengan akses paket. Definisi Inno Setup ada di `installer/`; installer executable memerlukan compiler tersebut.

Jika execution policy Windows memblokir `.ps1`, gunakan perintah langsung tanpa mengubah kebijakan sistem:

```powershell
dotnet restore STOKIN.sln --packages tmp/packages
dotnet build STOKIN.sln -c Release --no-restore
dotnet run --project tests/Stokin.Domain.Tests -c Release --no-restore
dotnet run --project tests/Stokin.IntegrationTests -c Release --no-restore
dotnet publish src/Stokin.Desktop -c Release --no-restore --self-contained false -o output/app
```
