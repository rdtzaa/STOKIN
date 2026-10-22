# Checklist interaksi Windows

Render offscreen berhasil; pemeriksaan berikut membutuhkan sesi Windows interaktif dan koneksi SQL valid.

- [ ] 1366×768 pada skala100% dan125%; menu dapat discroll, tabel/editor tidak memotong kontrol penting.
- [ ] Tab/Shift+Tab, Enter, fokus, dan pesan validasi per kolom dapat digunakan.
- [ ] Cari SKU/nama; filter kategori/status/aktif; urutkan serta ganti halaman.
- [ ] Isi dokumen beberapa satuan, periksa subtotal/konfirmasi; SKU duplikat ditolak.
- [ ] Gagal SQL mempertahankan input; hasil commit tidak diketahui dipulihkan lewat RequestId.
- [ ] Sesi terkunci setelah15 menit; editor disembunyikan sampai pemilik login ulang.
- [ ] Logout/ganti pengguna tidak memperlihatkan input sesi sebelumnya.
- [ ] Petugas hanya melihat biaya penerimaan sendiri; ekspor tidak memuat biaya.
- [ ] Opname: isi fisik, validasi alasan, konflik, muat ulang, dan pembukuan selisih nol.
- [ ] Backup/restore pada database uji terpisah dan rekonsiliasi sebelum produksi.
