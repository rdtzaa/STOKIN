# Review independen dan perbaikan

Reviewer: satu subagent independen, read-only. Tidak ada temuan Critical. Review mengidentifikasi lima Important dan dua Minor. Dua Minor dinilai ulang sebagai Important karena menyangkut informasi stok setelah refresh dan batas periode laporan yang salah.

| Temuan | Perbaikan | Bukti regresi |
|---|---|---|
| Restore membuka akses sebelum verifikasi | Shared transaction gate pada otorisasi/login memakai applock di master; restore memakai exclusive session gate sampai migrasi, invalidasi sesi, rekonsiliasi, dan audit selesai. Kegagalan mempertahankan maintenance, atau SINGLE_USER jika skema tidak dapat diakses. Lock dilepas eksplisit sebelum koneksi kembali ke pool. | `RecoveryTests.Restore_validation_failure_keeps_access_closed`, `Restore_reopens_only_after_verification`: RED→GREEN. SQL gate/restore aktual tetap belum diuji. |
| Hide/Show merusak modal editor setelah lock | Sembunyikan konten tanpa menyembunyikan Window; modal loop dan callback DialogResult dipertahankan. | Regresi WPF off-screen memakai ShowDialog sebenarnya, guard lock/unlock, lalu completion modal. RED→GREEN. |
| Jendela biaya tertinggal setelah penurunan peran | Semua child view direvokasi/ditutup pada perubahan role atau unlock dengan role berubah. Editor dapat ditutup tanpa menampilkan data atau konfirmasi sensitif. | Regresi WPF menutup tabel sensitif melalui guard dan memastikan tidak ditampilkan kembali. |
| Save/Post bertumpuk dan Dirty dihapus ketika ada edit lebih baru | Shared busy guard, CanEdit mengikuti busy, revision counter hanya menandai input tersimpan bila snapshot belum berubah. | `EditorRegressionTests.Shared_busy_guard_blocks_mutation_and_overlapping_save_post`, `Delayed_save_does_not_mark_newer_edits_as_saved`: keduanya RED→GREEN dengan repository tertunda. |
| Overflow dianggap hasil pembukuan tidak diketahui | Klasifikasikan OverflowException sebagai kegagalan pasti; pending request dibersihkan dan form dapat dikoreksi. Error transport tetap tidak diketahui. | `RecoveryTests.Overflow_is_definite_failure_so_editor_can_be_corrected`, `Transport_errors_remain_unknown`: RED→GREEN. |
| Refresh laporan memakai snapshot lama | Refresh eksplisit memuat ulang query snapshot; export tetap memakai snapshot yang sedang ditampilkan. | Regresi ShellViewModel dengan stok pertama1 kemudian2; RED actual1→GREEN actual2. |
| Periode laporan mengabaikan zona toko | Batas tanggal memakai StorePeriod dan zona snapshot toko, bukan Jakarta yang dihardcode. | `StorePeriodTests` Jakarta17:00UTC/Makassar16:00UTC hari sebelumnya, RED→GREEN. |

Application service menjadi facade untuk seluruh operasi WPF, dengan validasi domain sebelum repository dan otorisasi otoritatif tetap di transaksi SQL. Seluruh suite yang dapat berjalan diuji setelah perbaikan. Tidak ada review ulang berulang atau subagent implementer tambahan.

## Hal yang tidak dinilai reviewer

- Eksekusi SQL, rollback/konkurensi, restore, dan performa tanpa lingkungan integrasi. Keputusan: tidak mengklaim lulus; sediakan tes opt-in dan matriks penerimaan.
- Interaksi WPF di luar inspeksi sumber. Keputusan: regresi modal dan render dijalankan; keyboard, fokus penuh, sesi real15 menit, serta skala Windows aktual tetap checklist manual.

Tidak ada Minor yang sengaja ditunda; kedua temuan awal Minor diperbaiki setelah peningkatan tingkat dampak. Daftar ini tidak menjadikan SQL acceptance yang dilewati sebagai kelulusan P0.

Pemeriksaan kesesuaian akhir menambahkan total valuasi, tampilan waktu sesuai zona toko di tabel, detail minimum/satuan barang, serta metadata pendamping CSV. ReportPresentationTests membuktikan total nilai tanpa mencampur satuan dan konversi UTC ke Makassar; RED?GREEN.
