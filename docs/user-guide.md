# Panduan STOKIN

## Persiapan

Admin membuat kategori dan satuan, lalu barang dengan SKU unik, nama, kategori, satuan, dan minimum. Harga jual hanya referensi. Barang baru mempunyai stok dan nilai nol. Masukkan jumlah yang sudah ada melalui Stok Awal, sekali sebelum mutasi lain. SKU dan satuan terkunci setelah mutasi.

## Operasi barang

Cari barang lewat SKU/nama dan gabungkan kategori, status stok, serta aktif/nonaktif. Pilih baris untuk mengubah atau membuka kartu stok. Minimum nol hanya menghasilkan Aman/Habis. Menipis berarti stok positif yang tidak melebihi minimum. Dashboard memprioritaskan habis, lalu rasio stok/minimum terendah.

Barang Masuk/Keluar mendukung beberapa SKU. Cari dan pilih barang, tambah baris, isi jumlah integer dan biaya masuk jika relevan. Setiap SKU hanya muncul sekali. Jumlah tidak dijumlahkan lintas satuan. Simpan Draf tidak mengubah saldo. Bukukan menampilkan konfirmasi; dokumen yang dibukukan tidak dapat diubah/dihapus. Tombol batal di daftar hanya berlaku untuk draf.

Jika hasil pembukuan belum diketahui setelah koneksi putus, gunakan Periksa Hasil. Permintaan berikutnya memakai RequestId yang sama sampai hasil diketahui. Jangan membuat dokumen pengganti untuk transaksi yang masih belum diketahui hasilnya.

## Koreksi dan opname

Koreksi manual hanya Admin dan memerlukan ID dokumen asal yang sudah dibukukan dan memuat SKU terkait. ID terlihat pada daftar dokumen. Koreksi jumlah memakai jumlah absolut dan arah IN/OUT. Koreksi nilai memakai jumlah nol, pratinjau saldo, target nilai seluruh barang tersisa, dasar perhitungan, dan alasan. Nilai harus positif, berbeda, dan maksimal empat desimal. Koreksi tidak mengubah biaya transaksi historis.

Admin membuka sesi opname; kosongkan daftar ID untuk memilih semua SKU aktif. Petugas mengisi fisik dan alasan selisih; Admin meninjau biaya ketika stok acuan nol dan membukukan. Jika versi saldo berubah, muat ulang acuan dan hitung ulang baris yang berubah. Semua selisih nol menutup sesi tanpa ledger baru.

## Laporan dan akses

Laporan stok, menipis/habis, kartu stok, riwayat, valuasi, dan hasil opname memakai snapshot yang konsisten. Kartu stok menampilkan saldo awal periode dan mutasi sampai batas akhir eksklusif. Ekspor CSV mengikuti snapshot; transaksi setelah snapshot tidak mengubah CSV tersebut. Gunakan koma untuk input desimal rupiah, tanpa pemisah ribuan; CSV menggunakan titik dan empat desimal untuk rekonsiliasi.

Petugas dapat membaca kuantitas dan biaya input penerimaan miliknya sendiri. Biaya penerimaan orang lain, rerata, valuasi, biaya keluar, detail koreksi, serta ekspor biaya hanya Admin. Semua ekspor Petugas hanya kuantitas.

## Akun dan sesi

Setiap pengguna memakai akun sendiri. Lima kegagalan login dalam 15 menit mengunci akun selama 15 menit. Sesi terkunci setelah 15 menit tanpa aktivitas. Login ulang menggunakan akun yang sama untuk melanjutkan input; logout menutup akses. Input belum disimpan hanya bertahan dalam memori aplikasi. Admin terakhir tidak dapat dinonaktifkan atau diturunkan perannya.

CSV menyertakan file pendamping `.metadata.json` yang menyimpan toko, waktu snapshot, waktu ekspor, zona, serta filter/periode. Laporan valuasi menambahkan baris total nilai; jumlah unit tidak dijumlahkan lintas satuan.
