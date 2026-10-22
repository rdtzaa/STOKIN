# Setup STOKIN

## Prasyarat

Windows x64, .NET 8 Windows Desktop Runtime x64, SQL Server Express atau edisi SQL Server kompatibel. SDK build 10.0.401. Driver dipatok Microsoft.Data.SqlClient 7.1.1. Jalankan `scripts/Test-Prerequisites.ps1` untuk runtime; tambahkan `-TestSql` untuk memeriksa koneksi Windows memakai sqlcmd.

Target awal rancangan .NET 10 diganti .NET 8 karena dependensi SqlClient untuk target yang lebih baru tidak tersedia di cache dan akses NuGet diblokir. Pindahkan target ke .NET 10 dan uji ulang ketika seluruh paket bisa direstore. Ini keputusan kompatibilitas lingkungan, bukan perubahan lingkup fitur.

## Koneksi dan setup pertama

- Buka aplikasi sebagai pengguna Windows yang biasanya memakai SSMS.
- Pilih server `.\SQLEXPRESS` atau nama instance yang dipakai di SSMS, Windows Authentication, dan database `Stokin`.
- Centang pembuatan database hanya bila database belum dibuat. Setup hanya menerima nama `Stokin` atau `Stokin_<nama>` untuk operasi pembuatan database.
- Sertifikat lokal self-signed dapat dipercaya dengan checkbox khusus. Koneksi tetap meminta enkripsi; untuk LAN gunakan sertifikat valid dan nonaktifkan trust-bypass.
- Setup membuat database, mengaktifkan snapshot isolation, menerapkan migrasi bernomor dengan checksum, dan membuat Admin pertama berdasarkan input pengguna.
- Simpan kredensial konfigurasi memakai DPAPI CurrentUser di `%LOCALAPPDATA%\STOKIN\connection.dpapi`. Jangan menyalin file ini ke pengguna Windows lain.

Setup/migrasi memerlukan izin DDL; operasi harian sebaiknya memakai akun Windows/database terbatas dengan izin tabel/sequence yang sesuai. Lihat `database/permissions.sql`. Akun backup/restore memerlukan izin SQL tambahan; akun harian tidak harus sysadmin.

## Error SSPI

Jika SSMS berhasil tetapi aplikasi menyatakan koneksi gagal, buka aplikasi dari sesi Windows yang sama dan coba nama server persis seperti SSMS. Jalankan uji baca `sqlcmd -S '.\SQLEXPRESS' -E -Q 'SELECT 1'`. Error SSPI dapat terkait token logon, principal/SPN, atau konteks keamanan proses; jangan mereset password/akun SQL atau mengubah konfigurasi server tanpa diagnosis.

Log teknis di `%LOCALAPPDATA%\STOKIN\technical.log` memuat CorrelationId, tipe error, nomor SQL, dan stack tanpa password/hash/connection string. Aplikasi mempertahankan input ketika koneksi gagal; tidak ada antrean pembukuan offline.

## Skema dan perubahan

Migrasi ada di `database/migrations`. Jangan mengubah migrasi yang sudah diterapkan. Buat migrasi baru dan uji pada salinan data terlebih dahulu. Jangan menjalankan migrasi ke database lain yang bukan STOKIN. Rekonsiliasi saldo dengan ledger sebelum dan setelah pemulihan/migrasi.
