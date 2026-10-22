# Hasil verifikasi STOKIN

Tanggal: 22 Oktober 2026. Status: implementasi tersedia dan dapat dibuild; **belum lulus penerimaan P0 menyeluruh**.

## Hasil aktual

- `dotnet build STOKIN.sln -c Release --no-restore`: berhasil, 0 warning, 0 error.
- `dotnet run --project tests/Stokin.Domain.Tests -c Release --no-restore`: 24 lulus, 0 gagal. Mencakup matematika PRD, residu pembulatan, status stok, overflow, presisi, validasi dasar, CSV, serta percentile.
- `dotnet run --project tests/Stokin.IntegrationTests -c Release --no-restore`: 6 lulus, 0 gagal, **13 dilewati**. Yang lulus: hash/salt PBKDF2, render dan modal WPF, parser angka Indonesia, command guard, serta dua regresi editor dengan operasi simpan tertunda. Yang dilewati: database/migrasi, transaksi, konkurensi, koreksi, opname, izin SQL, periode, backup/restore, dan benchmark.
- `dotnet publish src/Stokin.Desktop -c Release --no-restore --self-contained false -o output/app`: berhasil; executable `output/app/Stokin.Desktop.exe` tersedia dengan dependensinya.
- Seluruh skrip PowerShell lolos pemeriksaan parser. Eksekusi `.ps1` langsung diblokir kebijakan PowerShell mesin; tidak ada kebijakan sistem yang diubah. Perintah dotnet langsung digunakan untuk build/tes/publish.
- WPF dirender secara terprogram ke `output/ui/`: koneksi, setup Admin, dashboard pada ukuran logis 1280×720 dan 1093×614, editor barang, serta penerimaan. Fixture render memakai data sintetis khusus tes, bukan data aplikasi terhubung. Ini bukan bukti seluruh workflow keyboard/interaksi/dpi Windows nyata lulus.

## Hambatan lingkungan

NuGet menolak koneksi socket ke `api.nuget.org`. Paket tersedia direstore dari cache lokal; test runner executable tidak memerlukan framework pengujian eksternal. Target semua proyek .NET 8 agar dependency graph SqlClient yang tersedia dapat diresolve; SDK tetap10.0.401. Paket framework-dependent memerlukan Windows Desktop Runtime8 x64.

Probe Microsoft.Data.SqlClient ke `.\SQLEXPRESS` dan shared-memory `lpc:.\SQLEXPRESS` gagal dengan **The target principal name is incorrect. Cannot generate SSPI context.** LocalDB juga tidak dapat membuat instance dari konteks proses ini. Pengguna mengonfirmasi koneksi biasa melalui SSMS memakai SQLEXPRESS + Windows Authentication. Belum ada database toko yang dibuat/diubah oleh pengujian ini.

Compiler Inno Setup dan runtime packs self-contained tidak tersedia; hanya definisi installer disediakan. Tidak ada klaim installer executable atau paket self-contained sudah dihasilkan.

## Menjalankan SQL acceptance

Jalankan dari akun Windows yang dapat mengakses instance melalui SSMS:

```powershell
$env:STOKIN_TEST_CONNECTION='Server=.\SQLEXPRESS;Database=Stokin_Test_Acceptance;Integrated Security=true;Encrypt=true;TrustServerCertificate=true'
dotnet run --project tests/Stokin.IntegrationTests -c Release --no-restore
```

Tes membuat/menghapus database uji dengan nama GUID terisolasi. Backup memerlukan `STOKIN_SQL_BACKUP_DIR` yang dapat ditulis layanan SQL; benchmark memerlukan `STOKIN_RUN_BENCHMARK=1`. SQL skip tidak boleh ditafsirkan sebagai pass. Jalankan rekonsiliasi dan latihan restore sebelum memasukkan data produksi.

## Bukti lanjutan

Matriks AT01–AT36 ada di `matrix.md`. Review independen dan perbaikannya tercatat pada `review.md`; seluruh regresi yang dapat dijalankan sudah lulus. Angka performa serta target RPO/RTO belum diukur.

Pemeriksaan akhir juga memverifikasi total valuasi tanpa menjumlahkan kuantitas lintas satuan dan konversi waktu ke zona toko. CSV disertai metadata JSON berisi toko, snapshot, waktu ekspor dengan offset, dan filter/periode. Smoke startup executable berhasil tanpa crash awal; hal ini tidak membuktikan koneksi SQL berhasil.
