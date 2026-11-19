# Hasil Eksperimen DDA - Haunted House: Hantu yang Mempelajari Ketakutanmu

Dibuat otomatis oleh `Tools > Hantu Belajar > Uji > Eksperimen DDA` (5 rumah per blok, bot AutoPilot memainkan game sungguhan, skala waktu x4). Tiap blok mulai dari d = 0,40, memori hantu dikosongkan, dan seed rumah sama untuk kondisi **DDA** maupun **Tetap** (d = 0,40 sepanjang blok).

Profil bot: **Pro** (tenang, jongkok saat hantu dekat, memilih tempat sembunyi yang jarang dipakai), **Sedang** (tempat sembunyi terdekat), **Lemah** (sering berlari, reaksi lambat, selalu kembali ke tempat sembunyi favorit).

| Profil | Kondisi | Lolos | Waktu rata2 (dtk) | Relik rata2 | Dikejar rata2 | Sembunyi rata2 | d akhir | Timeout |
|---|---|---|---|---|---|---|---|---|
| Pro | DDA | 1/5 (20%) | 130 | 87% | 2.0 | 1.8 | 0.30 | 0 |
| Pro | Tetap | 1/5 (20%) | 129 | 67% | 2.2 | 2.2 | 0.40 | 0 |
| Sedang | DDA | 0/5 (0%) | 53 | 53% | 1.4 | 0.6 | 0.25 | 0 |
| Sedang | Tetap | 1/5 (20%) | 94 | 60% | 2.2 | 1.8 | 0.40 | 0 |
| Lemah | DDA | 1/5 (20%) | 36 | 67% | 1.0 | 0.0 | 0.46 | 0 |
| Lemah | Tetap | 2/5 (40%) | 39 | 80% | 1.0 | 0.0 | 0.40 | 0 |

## Ringkasan
- **Pro**: dengan DDA kesulitan bergerak dari 0.40 ke 0.30 (rata-rata 0.38); lolos 1/5 vs 1/5 tanpa DDA.
- **Sedang**: dengan DDA kesulitan bergerak dari 0.40 ke 0.25 (rata-rata 0.30); lolos 0/5 vs 1/5 tanpa DDA.
- **Lemah**: dengan DDA kesulitan bergerak dari 0.40 ke 0.46 (rata-rata 0.52); lolos 1/5 vs 2/5 tanpa DDA.

Interpretasi: DDA bekerja ke arah yang dirancang - setelah tertangkap kesulitan turun (Pro 0.40 -> 0.30, Sedang 0.40 -> 0.25) sehingga hantu lebih lambat dan kurang peka serta rumah berikutnya lebih kecil dengan lebih banyak tempat sembunyi & baterai; setelah lolos kesulitan naik (Pro 0.40 -> 0.63 setelah rumah pertama, Lemah 0.37 -> 0.60). Dengan 5 rumah per blok, selisih tingkat lolos antara kondisi DDA dan Tetap masih berada dalam variasi acak (perilaku hantu dan bot memakai keacakan), sehingga tabel ini menunjukkan arah adaptasi kesulitan, belum bukti statistik perbedaan tingkat kemenangan. Untuk laporan final disarankan menambah jumlah rumah per blok dan playtest dengan pemain manusia.

Data per rumah: `Hasil_Eksperimen_DDA.csv`.
