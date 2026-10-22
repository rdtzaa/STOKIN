# Paket Windows

Jalankan `scripts/Publish.ps1`, kemudian kompilasi `Stokin.iss` memakai Inno Setup 6. Paket memasang aplikasi untuk pengguna Windows yang menjalankannya; tidak mengubah SQL Server, akun Windows, atau jadwal backup secara otomatis.

Build saat ini framework-dependent: memerlukan .NET 8 Windows Desktop Runtime x64. Installer memeriksa runtime. SQL Server, koneksi, hak migrasi dan skema diperiksa pada setup aplikasi. Koneksi tersimpan melalui DPAPI pengguna Windows, tidak dibundel dalam installer.

Definisi installer tersedia; executable installer hanya boleh dinyatakan tersedia setelah compiler benar-benar berhasil. Paket belum ditandatangani karena sertifikat penandatanganan tidak disediakan.
