# Rancangan teknis STOKIN P0

Tanggal: 22 Oktober 2026. Acuan produk: `PRD_STOKIN.pdf` versi 1.1, seluruh 19 halaman dan empat sketsa layar telah ditinjau.

Status: rancangan teknis disetujui pengguna melalui pesan "oke setuju" pada 22 Oktober 2026. Belum ada kode aplikasi atau database yang dibuat; langkah berikutnya adalah peninjauan rencana implementasi.

## 1. Tujuan dan batas pekerjaan

Membangun aplikasi desktop Windows berbahasa Indonesia untuk satu toko, satu lokasi, dan satu komputer. Admin dan Petugas dapat menelusuri stok dari dokumen sampai ledger. Nilai persediaan menggunakan biaya rata-rata bergerak, bukan harga jual.

Seluruh P0 adalah sasaran implementasi: setup; autentikasi; hak akses; barang, kategori, satuan; dashboard; stok awal; dokumen masuk dan keluar; penyesuaian jumlah; koreksi nilai; opname; kartu stok; laporan; CSV; pengguna; pengaturan; audit; backup dan restore; pemeriksaan prasyarat dan paket instalasi. Keberhasilan rilis diukur dengan AT01–AT36, dengan AT33 mode LAN sebagai gerbang terpisah.

P1 dan P2 tidak ditambahkan: impor, grafik, kasir, cloud, cabang, barcode, konversi satuan, kuantitas pecahan, retur terhubung, dan penyajian ulang biaya historis.

Keputusan yang sudah ditetapkan pengguna melalui PRD: C#, XAML, WPF, MVVM, .NET, SQL Server, ADO.NET, dan Microsoft.Data.SqlClient. Keputusan tambahan di bawah adalah usulan implementasi, bukan kebutuhan baru.

## 2. Pilihan pendekatan

1. **Rekomendasi: monolit desktop berlapis.** Satu executable WPF dengan pustaka Domain, Application, dan Infrastructure. Sesuai alur PRD, dapat dijalankan tanpa internet, dan aturan stok dapat diuji terpisah dari layar.
2. Satu proyek WPF untuk seluruh kode. Lebih sedikit proyek, tetapi pemisahan hak akses, transaksi, dan pengujian lebih mudah dilanggar.
3. WPF dengan layanan backend terpisah. Cocok untuk perangkat tidak tepercaya pada pengembangan berikutnya, tetapi menambah deployment dan operasi di luar kebutuhan satu komputer P0.

Pilih pendekatan pertama. Tidak menambah API atau ORM. Mode LAN dinonaktifkan sampai uji lima pengguna lulus.

## 3. Stack dan struktur solution

- Target awal `net10.0-windows`, SDK lokal 10.0.401, build x64.
- Microsoft.Data.SqlClient jalur LTS 6.1, patch 6.1.7 sebagai kandidat yang diverifikasi saat restore/build. Versi paket dipatok; kegagalan mendapatkan paket tidak disamarkan sebagai build berhasil.
- SQL Server Express lokal yang sudah terpasang; versi server dan kompatibilitas diuji sebelum skema dibekukan.
- MVVM dengan binding, command asinkron, dan validasi properti. Tidak menggunakan framework UI komersial.
- Hash kata sandi menggunakan PBKDF2-HMAC-SHA256 melalui API kriptografi .NET, salt acak per kata sandi, parameter versi tersimpan, pembandingan waktu konstan, dan peningkatan work factor saat login bila diperlukan.
- Pengujian unit untuk aturan domain dan pengujian integrasi SQL Server untuk transaksi, konflik, izin, serta laporan.

Struktur yang akan dibuat:

```text
STOKIN.sln
src/
  Stokin.Domain/          aturan stok, valuasi, status, dan validasi domain
  Stokin.Application/     layanan, otorisasi, DTO, dan kontrak repository
  Stokin.Infrastructure/  ADO.NET, migrasi, konfigurasi terlindungi, CSV, log
  Stokin.Desktop/         App, Views, ViewModels, tema, dan dialog WPF
tests/
  Stokin.Domain.Tests/
  Stokin.IntegrationTests/
database/                migrasi SQL bernomor dan konfigurasi hak minimum
scripts/                 pemeriksaan prasyarat, publish, backup, dan verifikasi
installer/               definisi paket instalasi dan dokumentasi prasyarat
docs/                    panduan, matriks penerimaan, dan hasil verifikasi
```

Domain tidak bergantung pada SQL atau WPF. Application mengakses persistence melalui kontrak. Infrastructure mengimplementasikan kontrak. Desktop menjadi composition root dan hanya memanggil layanan Application.

## 4. Model data dan integritas

Entitas mengikuti bagian 8 PRD: Roles, Users, Categories, Units, Products, StockBalances, StockDocuments, StockDocumentLines, StockLedger, Stocktakes, StocktakeLines, AuditLogs, dan Settings. Tambahkan tabel versi skema, pencatatan percobaan login, dan catatan koreksi Admin yang bersifat tambah saja.

- Gunakan kunci surrogate, foreign key tanpa cascade delete riwayat, indeks pencarian, dan `rowversion` pada master, saldo, draf, serta sesi opname.
- SKU, username, kategori, dan satuan mempunyai kunci normalisasi untuk keunikan tanpa membedakan kapital. Trim input sebelum validasi. Nama barang boleh sama.
- `StockBalances.Quantity` integer nonnegatif; `InventoryValue decimal(19,4)` nonnegatif; kuantitas nol mewajibkan nilai nol.
- Biaya unit dan nilai tersimpan memakai `decimal(19,4)`. Tolak input lebih dari empat desimal serta overflow, jangan membulatkan input tidak valid diam-diam.
- Dokumen mempunyai tipe, status, nomor unik, pemilik, pembuku, waktu UTC, referensi, alasan, dan versi. Nomor berasal dari sequence server dengan prefix tipe; nomor boleh memiliki celah.
- Detail unik `(DocumentId, ProductId)`. Identitas SKU, nama, dan satuan disalin saat pembukuan untuk mempertahankan tampilan historis.
- Ledger mempunyai ID berurutan dan referensi detail unik. Dokumen dibukukan, ledger, dan audit bisnis tidak dapat diubah melalui aplikasi.
- RequestId pembukuan memiliki indeks unik terfilter dan fingerprint isi permintaan yang dinormalisasi. Fingerprint mencakup dokumen, versi draf, seluruh baris, alasan, referensi, dan snapshot koreksi.
- Indeks ledger `(ProductId, LedgerId)` dan dokumen menurut waktu/status/pemilik. Daftar barang menggunakan paginasi server, awal 50 baris, serta whitelist kolom pengurutan.
- Laporan menggunakan snapshot isolation yang diaktifkan saat provisioning database. Semua pembacaan untuk satu laporan berada dalam satu snapshot transaksi.

Migrasi berjalan berurutan dengan checksum dan versi. Pemeriksaan koneksi/versi dilakukan sebelum membuka layar data. Akun migrasi dan backup/restore dipisahkan dari izin operasi harian bila SQL Server memerlukannya.

## 5. Mesin pembukuan

Satu layanan pembukuan menangani OPENING, IN, OUT, QUANTITY_ADJUSTMENT, dan VALUE_ADJUSTMENT. Menyimpan draf tidak mengubah saldo.

Urutan pembukuan:

1. Periksa sesi, status aktif, peran terbaru, pemilik draf, dan permintaan.
2. Buka transaksi database, serialkan RequestId, lalu cari hasil terdahulu. Isi identik mengembalikan hasil pertama; isi berbeda ditolak.
3. Kunci draf dan baris saldo dengan `UPDLOCK, HOLDLOCK`, selalu menurut ProductId untuk mengurangi deadlock. Periksa ulang izin dalam transaksi.
4. Validasi semua baris, referensi, status aktif, stok tersedia, batas desimal, dan versi snapshot bila relevan.
5. Hitung semua saldo menggunakan decimal C# dan aturan pembulatan PRD. Simpan snapshot identitas, detail pembukuan, ledger, saldo, status dokumen, fingerprint, dan audit dalam transaksi yang sama.
6. Commit, kembalikan hasil, lalu muat ulang tampilan yang bergantung pada saldo.

Kesalahan baris kedua membatalkan seluruh transaksi dan mempertahankan draf. Klik ganda dinonaktifkan di UI dan tetap aman di layanan/database. Saat hasil commit tidak diketahui karena koneksi terputus, UI menyimpan RequestId, menampilkan status perlu diperiksa, lalu mencari hasil dengan RequestId yang sama sebelum menawarkan pengulangan. Lookup RequestId tetap tunduk pada izin detail dokumen.

Stok awal hanya sekali per SKU dan sebelum mutasi apa pun. Tidak ada endpoint atau kolom master yang dapat mengubah saldo langsung. Dokumen dibukukan tidak dapat diedit, dibatalkan, atau dihapus.

## 6. Valuasi dan koreksi

- Masuk: `Qbaru = Q + q`, `Vbaru = V + q*c`.
- Keluar sebagian: `biaya = Round(q * (V/Q), 4, AwayFromZero)`; kurangi Q dan V.
- Keluar seluruh saldo: ambil seluruh V, sehingga saldo Q dan V tepat nol.
- Penyesuaian positif memakai rerata saat ini; jika Q nol, Admin wajib mengisi biaya unit positif. Penyesuaian negatif mengikuti biaya keluar.
- Koreksi nilai: Q tetap, Q harus positif, target nilai positif dan berbeda dari V, maksimum empat desimal. Simpan basis perhitungan, alasan, referensi dokumen dibukukan yang memuat SKU tersebut, snapshot Q/V, dan BalanceVersion. Tolak snapshot kedaluwarsa.
- Ledger koreksi nilai mempunyai delta kuantitas nol dan delta nilai bertanda. Tidak mengubah biaya transaksi sebelumnya.
- Koreksi kuantitas manual wajib mempunyai alasan dan referensi dokumen asal yang memuat SKU. Penyesuaian opname menggunakan referensi sesi.
- Koreksi nilai saat Q nol ditolak; Admin dapat menambahkan catatan koreksi pada dokumen asal tanpa mengubah dokumen atau ledger.

Tampilan rupiah dua desimal, rekonsiliasi dan ekspor empat desimal. Nilai maksimum diperiksa sebelum penyimpanan. Gunakan checked arithmetic untuk kuantitas.

## 7. Opname

Admin membuka sesi dan memilih SKU aktif. Snapshot mencakup Q, V, dan BalanceVersion. Petugas dapat mengisi fisik nonnegatif serta alasan selisih, tetapi tidak membukukan atau membaca biaya.

Saat Admin membukukan, kunci sesi dan seluruh saldo, cocokkan semua versi, validasi semua hitungan, lalu hasilkan paling banyak satu dokumen penyesuaian beserta ledger dan audit dalam satu transaksi. Satu konflik menolak seluruh sesi. Semua selisih nol menutup sesi tanpa dokumen mutasi. Selisih positif pada saldo nol memerlukan biaya unit Admin. Sesi hanya selesai satu kali.

Muat ulang acuan menandai baris perlu dihitung ulang dan menghapus hitungan fisik baris terdampak; tidak membawa hitungan lama seolah-olah valid.

## 8. Identitas, izin, dan sesi

Setup membuat Admin pertama dengan kata sandi yang dipilih pengguna; tidak ada akun atau kata sandi bawaan. Setup tidak dapat diulang untuk menyisipkan Admin setelah instalasi selesai.

Lima login gagal berturut-turut dalam jendela 15 menit mengunci akun 15 menit. Pesan login seragam, login berhasil mereset kegagalan. Sesi terkunci setelah 15 menit tanpa aktivitas; kata sandi diperlukan kembali. Input belum tersimpan tetap hanya di memori pemilik sesi dan disembunyikan saat terkunci atau pengguna berganti.

Layanan memeriksa ulang pengguna aktif dan peran pada setiap penulisan dan ekspor. Transaksi perubahan peran memakai penguncian untuk mempertahankan minimal satu Admin aktif. Akun yang memiliki aktivitas hanya dapat dinonaktifkan.

DTO yang diberikan ke Petugas tidak memuat biaya rata-rata, nilai saldo, biaya keluar, atau detail koreksi. Pengecualian: biaya input dan total penerimaan miliknya sendiri boleh dibaca pada draf maupun dokumen dibukukan. Ekspor Petugas selalu kuantitas saja. Kartu stok tetap menampilkan mutasi koreksi sebagai kuantitas nol dengan metadata yang diizinkan, tanpa alasan/detail koreksi Admin.

Rahasia koneksi memakai perlindungan Windows DPAPI; utamakan Windows Authentication. Log tidak berisi password, hash, atau connection string. Semua SQL berparameter bertipe eksplisit; tidak menggunakan interpolasi nilai pengguna ke SQL.

## 9. Layar dan perilaku UI

Gunakan susunan informasi empat sketsa PRD: sidebar, identitas toko dan pengguna, area konten, kartu dashboard, tabel, filter, serta tindakan sesuai izin. Tema terang dengan biru gelap, tipografi Segoe UI, dan status berlabel Aman/Menipis/Habis.

Layar setup/koneksi dan login mendahului shell. Menu: Dashboard, Data Barang, Kategori dan Satuan, Barang Masuk, Barang Keluar, Stok Awal, Stok Opname, Koreksi Persediaan, Laporan, Pengguna, Pengaturan, dan Audit. Menu khusus Admin disembunyikan dan layanannya tetap memeriksa izin.

- Dashboard menghitung SKU/kategori aktif, menipis tanpa habis, habis, nilai untuk Admin, prioritas habis kemudian rasio stok/minimum lalu SKU, dan sepuluh dokumen dibukukan terbaru. Kartu membuka filter terkait.
- Data barang mempunyai pencarian SKU/nama, filter kategori/status/aktif, paginasi, detail, dan kartu stok. Saldo hanya baca. SKU/satuan terkunci setelah mutasi.
- Form transaksi mendukung beberapa SKU tanpa duplikat, kuantitas per satuan, biaya sesuai izin, simpan/batalkan draf, konfirmasi pembukuan, serta pemulihan hasil tidak diketahui.
- Koreksi menampilkan nilai sebelum/sesudah dan dampak rerata sebelum konfirmasi. Opname menampilkan snapshot, fisik, selisih, alasan, dan konflik versi.
- Semua layar mempunyai loading, kosong, hasil pencarian kosong, validasi per kolom, berhasil, gagal koneksi, dan penanda data belum diperbarui. Gagal menyimpan mempertahankan input.
- I/O SQL asinkron; command cegah eksekusi bersamaan, mendukung pembatalan baca, serta konfirmasi ketika meninggalkan perubahan belum disimpan.
- Tata letak scrollable, fokus keyboard, access key, dan ukuran minimum diuji pada 1366×768 dengan skala 100% dan 125%.

## 10. Laporan, CSV, dan audit

Laporan P0: stok terkini, menipis/habis, kartu stok, riwayat transaksi, nilai persediaan Admin, hasil opname. Waktu tersimpan UTC; tampilan memakai zona toko awal Asia/Jakarta. Periode menggunakan waktu pembukuan dengan batas akhir eksklusif. Kartu stok menghitung saldo sebelum awal periode dan mengurutkan mutasi berdasarkan LedgerId.

Ekspor berasal dari snapshot laporan yang sudah dimuat, bukan query saldo terbaru. Periksa kembali izin saat ekspor; jika peran berubah, tolak snapshot berbiaya dan minta pemuatan ulang sesuai izin. Snapshot mencakup seluruh hasil filter, sementara UI dapat menampilkan halaman dari hasil tersebut. Untuk dataset besar, materialisasi laporan dilakukan asinkron dengan batas penggunaan memori yang diuji pada baseline.

CSV UTF-8 dengan BOM, escaping tanda petik/koma/newline, angka invariant tanpa ribuan dan empat desimal untuk nilai, waktu ISO 8601 dengan offset. Kolom teks yang setelah whitespace berawalan `=`, `+`, `-`, atau `@` diberi awalan apostrof; angka negatif numerik tetap angka. Nama file/metadata memuat toko, waktu snapshot, periode/filter, dan waktu ekspor.

Audit append-only mencatat login, perubahan master/akun, pembukuan/koreksi, ekspor, backup, dan restore dengan actor, waktu server, entitas, referensi, sebelum/sesudah yang relevan, hasil, dan CorrelationId. Audit pembukuan sukses berada dalam transaksi pembukuan. Kegagalan teknis mempunyai log terpisah yang tidak membocorkan pesan SQL ke UI.

## 11. Backup, restore, dan deployment

Backup Admin menjalankan backup SQL ke path server yang dipilih dan memverifikasi hasil; tampilkan waktu, lokasi server, dan status. Akun SQL harian tidak otomatis diberi sysadmin. Kegagalan izin ditampilkan dengan jelas.

Restore hanya dalam mode pemeliharaan, memblokir penulisan dan memastikan sesi lain berhenti. Tampilkan identitas database dan metadata backup, minta konfirmasi penggantian, buat backup sebelum restore bila memungkinkan, tutup connection pool, restore melalui koneksi master, lalu validasi skema serta rekonsiliasi. Jangan menganggap restore sudah diuji hanya karena backup dapat dibuat.

Sediakan skrip penjadwalan Windows Task Scheduler, dengan akun/operator yang dikonfigurasi, backup harian, retensi tujuh harian dan empat mingguan, serta petunjuk salinan media terpisah. Tidak bergantung pada SQL Agent Express atau aplikasi terbuka. Installer memeriksa Windows/.NET/SQL, meminta konfigurasi koneksi, dan tidak menyertakan rahasia.

Target publish awal Windows x64 self-contained. Paket instalasi, pemeriksaan prasyarat, dan panduan setup disertakan. Penandatanganan installer memerlukan sertifikat pengguna dan bukan syarat build lokal; hasil tanpa signature dilabeli secara akurat.

## 12. Pengujian dan bukti penerimaan

| Kelompok | Bukti yang harus tersedia |
|---|---|
| AT01–AT09 | Status batas/nol, kategori kosong, urutan prioritas, aktivitas, validasi master, kombinasi filter |
| AT10–AT14 | Stok awal sekali, snapshot historis, draf multi-SKU, immutable dokumen, penolakan stok negatif |
| AT15–AT18 | Dua koneksi SQL bersaing, idempotensi dengan payload sama/berbeda, rollback baris kedua, hasil commit hilang dan recovery RequestId |
| AT19–AT23 | Decimal 0,3333/0,6667, koreksi nilai positif/negatif, versi kedaluwarsa, catatan koreksi saldo nol |
| AT24–AT26 | Opname normal, konflik tanpa mutasi parsial, selisih nol, biaya saat stok nol, pembukuan ulang |
| AT27–AT29 | Awal/akhir periode, snapshot ekspor, redaksi biaya dan bypass layanan, lockout/inaktivitas, revokasi, Admin terakhir |
| AT30–AT32 | Restore ke database terpisah dan rekonsiliasi, kegagalan path backup, audit append-only, UTF-8/formula CSV |
| AT33 | Generator data 10.000 SKU/100.000 ledger, 100 pencarian dan 100 pembukuan ≤50 baris, p95 dan lingkungan tercatat |
| AT34–AT36 | WPF 1366×768 pada dua skala, keyboard/keadaan kosong, SQL berhenti/pulih dengan input tetap, prasyarat dan migrasi salinan |

Tes domain memverifikasi matematika dan validasi. Jaminan transaksi, locking, rollback, dan otorisasi diuji pada SQL Server nyata. Uji visual WPF dan gangguan koneksi ditandai manual bila alat yang tersedia tidak dapat menjalankannya; tidak mengklaim skenario tersebut lulus tanpa bukti.

Contoh angka PRD menjadi golden cases: 10×10.000 + 10×14.000 − 5 → Q15/V180.000; opname 14 → Q14/V168.000. Koreksi Q15/V165.000 menjadi V180.000 mempertahankan biaya keluar historis 55.000.

## 13. Kondisi lingkungan dan hal yang harus diselesaikan

- Folder awal hanya berisi PRD; belum merupakan repository Git dan tidak ditemukan AGENTS.md pada rantai direktori yang diperiksa. Jangan membuat klaim commit sebelum repository tersedia dan commit benar-benar berhasil.
- SDK .NET 10.0.401 dan Windows Desktop Runtime 10.0.12 terdeteksi.
- Layanan `MSSQL$SQLEXPRESS` sedang berjalan. Uji baca menggunakan sqlcmd/ODBC 17 gagal pada negosiasi enkripsi/kredensial; koneksi database belum terbukti. Implementasi harus menguji koneksi Microsoft.Data.SqlClient sebelum menetapkan connection string dan menjalankan migrasi. Tidak ada database yang diubah pada peninjauan ini.
- Restore paket, build WPF, dan pengujian SQL belum dijalankan karena belum ada kode. Tidak ada target performa atau uji penerimaan yang boleh dilaporkan lulus pada tahap rancangan.

## 14. Referensi teknis yang ditinjau

- Microsoft WPF overview: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/
- Microsoft SqlClient support lifecycle: https://learn.microsoft.com/en-us/sql/connect/ado-net/sqlclient-driver-support-lifecycle
- PRD_STOKIN v1.1: kebutuhan produk dan skenario penerimaan utama.

Rancangan mempertahankan lingkup P0 dan memisahkan fakta lingkungan, pilihan teknis, serta target penerimaan yang belum dibuktikan.
