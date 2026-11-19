# Haunted House: Hantu yang Mempelajari Ketakutanmu

3D First-Person Survival Horror (Unity 6000.0.83f1, URP) dengan **Procedural Content Generation**, **Dynamic Difficulty Adjustment**, dan **Game AI** berupa hantu yang mempelajari kebiasaan pemain.

Pemain terjebak di rumah berhantu yang berubah setiap malam. Kumpulkan semua relik (satu biasanya ada di ruang yang digembok, kuncinya tersembunyi di bagian lain rumah), lalu kabur lewat pintu keluar. Pemain bisa berjalan, berlari (stamina), berjongkok, memakai senter (baterai terbatas), dan bersembunyi di lemari atau kolong ranjang. Hantu mengingat tempat sembunyi dan jalur favoritmu, dan tingkat kesulitan menyesuaikan caramu bermain.

## Menjalankan
1. Buka folder ini dengan Unity **6000.0.83f1**.
2. Import paket Asset Store dari *Window > Package Manager > My Assets* (tidak disertakan di repo karena lisensi Asset Store):
   - Stealth Game: Haunted House | 3D Sample Project
   - Unnerving Ambient Sounds - Horror Game Sound Effect Pack
   - Horror Music - Free Starter Kit
3. Menu **Tools > Hantu Belajar > Bangun Proyek (aset + scene)**.
4. Buka `Assets/Scenes/HauntedHouse.unity`, tekan Play.

Tanpa paket Asset Store game tetap jalan dengan model dan suara pengganti sederhana.
Build Windows: **Tools > Hantu Belajar > Build Windows (.exe)** -> `Builds/Windows/HauntedHouse.exe`.

## Kontrol
| Tombol | Fungsi |
|---|---|
| WASD / Mouse | bergerak / melihat |
| Shift / C (Ctrl) | lari / jongkok |
| F | senter |
| E | ambil relik, kunci, baterai; buka gembok; sembunyi / keluar |
| Esc | jeda (Lanjutkan, Pengaturan, Cara Bermain, Menu Utama) |
| F1 | mode presentasi: panel PCG-DDA-AI + peta + status hantu |
| Tab / M | panel / peta |
| G | rumah baru (PCG) |
| F2 / F3 | kesulitan DDA turun / naik |
| F5 / F6 / F9 | bekukan hantu / panggil hantu mengejar / hapus memori hantu |

## Sistem
### Procedural Content Generation (`Scripts/PCG`)
`HouseGenerator.Generate(PcgParams)` menghasilkan `HouseLayout` (data murni, deterministik per seed):
1. **BSP** membagi denah tiap lantai (grid sel 3,6 m) menjadi ruangan.
2. Graf ketetanggaan ruang -> **spanning tree Kruskal** (union-find) + pintu loop (`loopChance`).
3. **Tangga** menghubungkan lantai 1 dan 2 (dipilih dari kandidat, dicek konektivitas BFS).
4. Ruang awal (ruang tamu) & **pintu keluar** di ruang batas terdalam (kedalaman BFS graf ruangan).
5. **Lock-and-key**: satu ruang daun dikunci, kuncinya ditempatkan di tempat yang terjangkau tanpa kunci (lebih disukai lantai lain).
6. Tempat sembunyi (lemari / kolong ranjang), **relik dengan batas jarak ke tempat sembunyi terdekat** (`relicSpotDist`), baterai, dan perabot: generate-and-test (penempatan dibatalkan bila memutus konektivitas).
7. Validasi (semua sel terhubung, kunci & pintu keluar terjangkau tanpa kunci, tiap lantai punya relik); gagal -> ulang dengan seed turunan, lalu parameter dilonggarkan.

`HouseBuilder` + `Furnish` mengubah data menjadi rumah 3D dari modul paket Haunted House (dinding dua sisi per tipe ruangan, jendela + cahaya bulan, kusen pintu, tangga berpagar, perabot per tipe ruangan, lampu dinding, perapian menyala, lilin). Hasil uji: `Hasil_Eksperimen_PCG.md`.

### Game AI (`Scripts/AI`)
- `GhostAI`: **Finite State Machine** Patroli / Hadang / Selidiki / Kejar / Cari / Periksa / Muncul, gerak **A\*** pada graf node (termasuk tangga), indra penglihatan (kerucut pandang + raycast; senter menyala = lebih terlihat, ruang gelap & jongkok = sulit terlihat) dan pendengaran (langkah kaki; beda lantai lebih redup).
- `GhostMemory`: model belajar **probabilistik**. Tiap tempat sembunyi = distribusi **Beta(alfa, beta)** dengan prior dari kebiasaan lintas rumah (hitungan Dirichlet per jenis tempat & tipe ruangan). Saat mencari, hantu memakai **Thompson sampling** (sampel theta ~ Beta) sehingga belajar tetapi tetap bereksplorasi. Jalur favorit dicatat sebagai heatmap dengan peluruhan; lokasi hadang dipilih dengan softmax.
- Lampu berkedip saat hantu dekat, jumpscare saat tertangkap.

### Dynamic Difficulty Adjustment (`Scripts/DDA`)
- `DDAManager`: indikator tiap 8 detik (tempo relik dibanding jadwal, waktu dikejar, baterai, waktu sembunyi, nyaris tertangkap) -> skor **P** -> dihaluskan (EMA) -> `d += (P - 0,62) x 0,1`, dibatasi +0,15 / -0,3 per rumah. Event: tertangkap -0,15, relik +0,03, lolos +0,08. Evaluasi berhenti saat jeda atau hantu dibekukan.
- `DdaMap`: d -> parameter hantu (kecepatan, pandang, dengar, lama mencari, bobot belajar, peluang memeriksa, eksplorasi), pengurasan baterai, dan parameter PCG rumah berikutnya (jumlah lantai, ukuran, tempat sembunyi, baterai, relik, ruang terkunci, jarak relik-tempat sembunyi, proporsi lampu menyala).
- Eksperimen bot (Pro / Sedang / Lemah, dengan vs tanpa DDA): `Hasil_Eksperimen_DDA.md`.

Alur integrasi: Performa Pemain -> DDA -> parameter Game AI + parameter PCG -> perubahan gameplay, ditambah memori belajar hantu sebagai adaptasi jangka panjang.

## Pengujian otomatis (Tools > Hantu Belajar > Uji)
- **Screenshot visual** -> `TestOutput/shots/`
- **Bot bermain (smoke)** -> `TestOutput/report_smoke.txt` (error, macet, posisi di luar batas)
- **Eksperimen DDA** -> `Hasil_Eksperimen_DDA.md`
- **Eksperimen PCG** -> `Hasil_Eksperimen_PCG.md`

## Kredit aset
- Model hantu: Kenney Graveyard Kit "character-ghost" (CC0).
- Stealth Game: Haunted House (Unity Technologies), Unnerving Ambient Sounds (DHSFX), Horror Music - Free Starter Kit (Fictium Sound Design) dari Unity Asset Store.
