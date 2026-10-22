# Batasan yang harus diketahui

1. Autentikasi SSPI menghalangi SQL acceptance di lingkungan agen. Integritas skema, SQL runtime, locking, migrasi, dan restore belum boleh dinyatakan lulus dari build saja.
2. .NET 8 dipilih untuk dependency graph offline yang lengkap. Migrasi ke target .NET 10 rancangan awal memerlukan restore dependency versi lebih baru dan tes ulang.
3. Paket saat ini framework-dependent, belum berupa installer terkompilasi atau self-contained. Inno Setup definition dan publish script disediakan.
4. Script PowerShell dibatasi execution policy mesin. Gunakan perintah dotnet langsung; pendaftaran jadwal backup harus dilakukan operator sesuai kebijakan Windows yang berlaku.
5. P95, baseline10.000 SKU/100.000 mutasi, lima pengguna LAN, gangguan jaringan nyata setelah commit, instalasi bersih, pengujian keyboard/dpi Windows, dan latihan restore/RPO/RTO belum diukur. Mode LAN tetap nonaktif.
6. UI render memakai fixture sintetis khusus tes; data contoh tidak dimasukkan diam-diam ke database toko. Tidak ada akun/kata sandi aplikasi bawaan.

7. Gate pemeliharaan memakai application lock di database master agar bertahan ketika database target diganti. Izin eksekusi sys.sp_getapplock di master dan urutan restore harus diverifikasi pada SQL Server target; tes orchestration bukan pengganti latihan SQL nyata.
