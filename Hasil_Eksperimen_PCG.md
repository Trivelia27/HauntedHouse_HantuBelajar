# Hasil Eksperimen PCG - Haunted House: Hantu yang Mempelajari Ketakutanmu

Dibuat otomatis oleh `Tools > Hantu Belajar > Eksperimen PCG` (200 seed per tingkat kesulitan, seed 1..200). Parameter PCG berasal dari `DdaMap.Pcg(d, seed)`.

| Tingkat | Valid | Percobaan rata2 | Waktu rata2 (ms) | Lantai | Ruangan | Pintu (loop) | Ruang terkunci | Tempat sembunyi | Jarak relik-sembunyi maks (target) | Dilonggarkan | Deterministik |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Mudah (d=0,10) | 200/200 | 1.0 | 0.6 | 1.0 | 8.0 | 8.9 (1.9) | 0/200 | 9.7 | 1.8 (2) | 0 | 200/200 |
| Sedang (d=0,45) | 200/200 | 1.0 | 0.7 | 2.0 | 12.0 | 11.6 (1.6) | 183/200 | 7.8 | 2.8 (4) | 0 | 200/200 |
| Sulit (d=0,80) | 200/200 | 1.0 | 1.2 | 2.0 | 18.4 | 18.5 (2.1) | 198/200 | 5.8 | 5.0 (6) | 0 | 200/200 |

Keterangan: *Valid* = semua sel kosong terhubung, pintu keluar terjangkau tanpa kunci, kunci terjangkau tanpa kunci, semua relik & tempat sembunyi terjangkau, tiap lantai punya relik. *Dilonggarkan* = jumlah rumah yang batas jarak relik-ke-tempat-sembunyi perlu dinaikkan agar relik cukup.
