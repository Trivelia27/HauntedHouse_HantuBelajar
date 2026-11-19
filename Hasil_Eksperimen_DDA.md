# Hasil Eksperimen DDA - Haunted House: Hantu yang Mempelajari Ketakutanmu

Dibuat otomatis oleh `Tools > Hantu Belajar > Uji > Eksperimen DDA` (5 rumah per blok, bot AutoPilot memainkan game sungguhan, skala waktu x4). Tiap blok mulai dari d = 0,40, memori hantu dikosongkan, dan seed rumah sama untuk kondisi **DDA** maupun **Tetap** (d = 0,40 sepanjang blok).

Profil bot: **Pro** (tenang, jongkok saat hantu dekat, memilih tempat sembunyi yang jarang dipakai), **Sedang** (tempat sembunyi terdekat), **Lemah** (sering berlari, reaksi lambat, selalu kembali ke tempat sembunyi favorit).

| Profil | Kondisi | Lolos | Waktu rata2 (dtk) | Relik rata2 | Dikejar rata2 | Sembunyi rata2 | d akhir | Timeout |
|---|---|---|---|---|---|---|---|---|
| Pro | DDA | 2/5 (40%) | 129 | 87% | 2.6 | 1.8 | 0.29 | 0 |
| Pro | Tetap | 2/5 (40%) | 163 | 80% | 2.8 | 2.2 | 0.40 | 0 |
| Sedang | DDA | 2/5 (40%) | 213 | 87% | 3.6 | 3.4 | 0.15 | 1 |
| Sedang | Tetap | 0/5 (0%) | 168 | 67% | 3.0 | 2.4 | 0.40 | 1 |
| Lemah | DDA | 1/5 (20%) | 38 | 67% | 0.8 | 0.2 | 0.47 | 0 |
| Lemah | Tetap | 4/5 (80%) | 40 | 100% | 0.8 | 0.0 | 0.40 | 0 |

## Ringkasan
- **Pro**: dengan DDA kesulitan bergerak dari 0.40 ke 0.29 (rata-rata 0.29); lolos 2/5 vs 2/5 tanpa DDA.
- **Sedang**: dengan DDA kesulitan bergerak dari 0.40 ke 0.15 (rata-rata 0.17); lolos 2/5 vs 0/5 tanpa DDA.
- **Lemah**: dengan DDA kesulitan bergerak dari 0.40 ke 0.47 (rata-rata 0.45); lolos 1/5 vs 4/5 tanpa DDA.

Interpretasi: setelah tertangkap DDA menurunkan kesulitan (hantu lebih lambat & kurang peka, rumah berikutnya lebih kecil dengan lebih banyak tempat sembunyi & baterai) dan setelah lolos menaikkannya. Dengan 5 rumah per blok, selisih tingkat lolos antara kondisi DDA dan Tetap masih dalam variasi acak; tabel ini menunjukkan arah adaptasi kesulitan, belum bukti statistik. Tambah jumlah rumah per blok (HB_HOUSES) dan lakukan playtest manusia untuk laporan final.

Data per rumah: `Hasil_Eksperimen_DDA.csv`.
