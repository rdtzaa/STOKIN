# Pencadangan dan pemulihan

## Backup manual

Admin membuka Pengaturan → Backup database, lalu memasukkan path pada **server SQL**, bukan lokasi browser/file klien. Akun layanan SQL Server harus dapat menulisnya. Hasil diverifikasi dengan RESTORE VERIFYONLY/CHECKSUM. Status terakhir tersimpan dalam pengaturan dan audit sukses/gagal.

## Jadwal

Jalankan skrip di komputer SQL Server dengan akun Windows yang memiliki izin backup dan direktori tujuan:

```powershell
.\scripts\Backup-Stokin.ps1 -BackupDirectory 'D:\BackupSTOKIN'
.\scripts\Register-BackupTask.ps1 -BackupDirectory 'D:\BackupSTOKIN' -At '23:00'
```

Registrasi meminta kredensial Windows secara aman. Task harian tidak memerlukan aplikasi desktop terbuka. Skrip menyimpan tujuh backup harian dan empat mingguan; backup mingguan dibuat pada Minggu. Retensi hanya menghapus file berawalan nama database yang mempunyai marker verifikasi dan berada tepat dalam folder yang diatur. Salin backup ke media terpisah dengan akses terbatas dan uji aksesnya; skrip tidak mengklaim menyalin ke media yang tidak dikonfigurasi.

## Restore

Pengaturan → Periksa/restore menampilkan database asal, waktu backup, serta path server. Ketik `PULIHKAN Stokin` (sesuaikan nama tujuan), lalu konfirmasi penggantian. Aplikasi membuat backup sebelum restore, menghentikan sesi lain melalui mode pemeliharaan, menutup pool koneksi, dan memulihkan melalui koneksi master. Pengguna harus login kembali setelah restore.

Uji pertama pada database terpisah. Cocokkan saldo dengan ledger menggunakan `Test-Reconciliation.ps1`. Catat waktu proses untuk target pemulihan dua jam; target kehilangan data maksimum 24 jam memerlukan jadwal benar-benar aktif. Restore memerlukan izin tinggi yang sebaiknya diberikan hanya pada akun operator pemeliharaan. Jangan menguji penggantian data pada database toko nyata.

Backup/restore SQL nyata belum terbukti dari lingkungan agen karena autentikasi Windows/SSPI gagal. Keberhasilan build tidak membuktikan latihan pemulihan berhasil.

Selama restore, gate eksklusif di master menahan otorisasi aplikasi sampai migrasi, invalidasi sesi, dan rekonsiliasi selesai. Jika verifikasi gagal, mode pemeliharaan tetap aktif; jangan membuka akses secara manual sebelum operator memperbaiki/menilai database. Setelah sukses, login baru diperlukan.
