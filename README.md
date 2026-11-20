# Haunted House: Hantu yang Mempelajari Ketakutanmu

3D First-Person Survival Horror (Unity 6000.0.83f1, URP) dengan **Procedural Content Generation**, **Dynamic Difficulty Adjustment**, dan **Game AI** berupa hantu yang mempelajari kebiasaan pemain.

Pemain terjebak di rumah berhantu yang berubah setiap malam. Kumpulkan semua relik (satu biasanya ada di ruang yang digembok, kuncinya tersembunyi di bagian lain rumah), lalu kabur lewat pintu keluar. Pemain bisa berjalan, berlari (stamina), berjongkok, memakai senter (baterai terbatas, bisa difokuskan), dan bersembunyi di lemari atau kolong ranjang. Di persembunyian pemain **aman**: hantu tidak bisa melihat atau menemukannya. Namun hantu mengingat jalur dan tempat sembunyi favoritmu, lalu menghadang dan berjaga di sana. Tingkat kesulitan menyesuaikan caramu bermain.

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

Pengaturan URP dari paket Stealth Game dibuat dengan URP yang lebih baru; pengaturan yang tidak dikenal Unity 6000.0 dibersihkan otomatis saat proyek dibuka (atau lewat **Tools > Hantu Belajar > Bersihkan Peringatan Proyek**), sehingga peringatan *Missing types referenced ... UniversalRenderPipelineGlobalSettings* tidak muncul lagi. Saat editor kosong (belum Play), tab Game memang menampilkan *No cameras rendering* karena seluruh rumah, kamera, dan menu dibuat saat Play.

## Kontrol
| Tombol | Fungsi |
|---|---|
| WASD / Mouse | bergerak / melihat |
| Shift / C (Ctrl) | lari / jongkok |
| F / tahan klik kanan | senter nyala-mati / fokuskan senter (lebih terang & jauh, boros baterai) |
| E | ambil relik, kunci, baterai; buka gembok; sembunyi / keluar |
| M | denah lengkap rumah (kedua lantai + jalur ke tujuan) |
| Esc | jeda (Lanjutkan, Panduan, Pengaturan, Menu Utama + denah rumah) |
| F1 | mode presentasi: panel PCG-DDA-AI, state hantu di indikator, memori hantu, hantu & heatmap jalur di peta |
| Tab | panel presentasi |
| G | rumah baru (PCG) |
| F2 / F3 | kesulitan DDA turun / naik |
| F5 / F6 / F9 | bekukan hantu / panggil hantu mengejar / hapus memori hantu |

## Antarmuka & panduan (`Scripts/UI`)
- **Menu utama** dengan kartu *Memori Hantu* (berapa kali kamu bersembunyi & tempat favoritmu), **pengaturan bertab** (Kontrol, Grafis, Audio, Gameplay), layar jeda dengan **denah rumah**, layar akhir dengan statistik dan perubahan kesulitan DDA, serta tombol **UJI COBA PEMAIN** (mode penelitian, lihat di bawah).
- **Panduan bermain** 4 halaman (tujuan, kontrol, sifat hantu, petunjuk arah) otomatis muncul saat pertama kali bermain; bisa dibuka lagi dari menu / jeda.
- **Panduan awal** (checklist) di rumah pertama: bergerak, senter, peta, ambil relik, bersembunyi. Hantu tetap tidur sampai langkah dasar selesai.
- **Kompas** di atas layar menunjuk tujuan berikutnya (relik terdekat -> kunci -> gembok -> pintu keluar) mengikuti jalur **A\*** rumah (lewat pintu & tangga), lengkap dengan jarak dan tanda naik/turun tangga; baterai ditandai saat senter hampir habis.
- **Peta mini** berputar mengikuti arah pandang + **denah penuh (M)** kedua lantai: relik, kunci, pintu digembok, pintu keluar, baterai, tempat sembunyi, tangga, jalur ke tujuan, ruangan yang sudah dikunjungi.
- **Informasi di layar ditata dalam jalur tetap** supaya tidak saling menimpa:
  - *indikator kewaspadaan hantu* di bawah kompas - satu elemen stabil: **HANTU MENDENGAR SESUATU** (menyelidiki suara), **HANTU MELIHATMU** (mengejar), **HANTU KEHILANGAN JEJAKMU** (setelah lolos). Tidak muncul berulang tiap langkah; saat peta penuh dibuka pindah ke bawah layar;
  - *notifikasi peristiwa* (relik, kunci, baterai, pintu) berikon, antre tanpa duplikat sehingga pesan relik tidak terpotong pesan lain;
  - *petunjuk* sekali-tampil di bawah tengah, antre (petunjuk bahaya didahulukan), status TERSEMBUNYI menumpuk rapi di atasnya;
  - notifikasi & petunjuk ditahan selama judul "RUMAH #N" atau peta penuh tampil; umpan balik tombol pengaturan terlihat di atas menu.
- **Mode presentasi (F1)**: panel PCG/DDA/AI di kolom kiri (di bawah kartu tujuan, tinggi mengikuti isi), memori hantu di kanan (di bawah peta mini), state FSM hantu di indikator - tidak ada panel yang menutupi petunjuk, kartu senter, atau notifikasi.
- Ikon, kartu, tombol, keycap dibuat prosedural (`Icons`, `UIKit`); font Bahnschrift / Segoe UI dari Windows dengan cadangan bawaan Unity.

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

`HouseBuilder` + `Furnish` mengubah data menjadi rumah 3D dari modul paket Haunted House (dinding dua sisi per tipe ruangan, jendela + cahaya bulan, kusen pintu, tangga berpapan samping utuh dengan list anak tangga + balok tepi lubang yang menopang dinding & pagar lantai atas, perabot per tipe ruangan, lampu dinding, perapian menyala, lilin). Karpet, balok plafon, sarang laba-laba, jendela, lukisan, dan lampu dinding tidak pernah ditempatkan di/menutupi lubang tangga. Hasil uji: `Hasil_Eksperimen_PCG.md`.

### Game AI (`Scripts/AI`)
- `GhostAI`: **Finite State Machine** Patroli / Hadang / Selidiki / Kejar / Cari / Periksa / Jaga / Muncul, gerak **A\*** pada graf node (termasuk tangga), indra penglihatan (kerucut pandang + raycast; senter menyala = lebih terlihat, ruang gelap & jongkok = sulit terlihat) dan pendengaran (langkah kaki; beda lantai lebih redup).
- **Hantu tidak tahu pemain bersembunyi**: pemain di persembunyian tidak terlihat, tidak terdengar, tidak bisa ditangkap, dan tidak pernah ditemukan saat hantu memeriksa tempat itu. Hantu hanya tahu posisi terakhir pemain terlihat; tekanan "muncul dekat pemain" juga berhenti selama pemain bersembunyi.
- `GhostMemory`: model belajar **probabilistik**. Tiap tempat sembunyi = distribusi **Beta(alfa, beta)** dengan prior dari kebiasaan lintas rumah (hitungan Dirichlet per jenis tempat & tipe ruangan); kebiasaan dicatat setelah pemain keluar dari persembunyian. Hantu memakai **Thompson sampling** (sampel theta ~ Beta) untuk memilih tempat yang diperiksa dan tempat **berjaga** (state Jaga), sehingga belajar tetapi tetap bereksplorasi. Jalur favorit dicatat sebagai heatmap dengan peluruhan; lokasi hadang dipilih dengan softmax.
- Lampu berkedip saat hantu dekat, jumpscare saat tertangkap.

### Senter (`Scripts/Player`)
Lampu sorot dengan bayangan lembut, pola cahaya (cookie) prosedural bertitik terang di tengah, cahaya sebaran lebar, dan sinar yang diarahkan ke titik bidik (sedikit tertinggal saat menoleh). Tahan klik kanan untuk mode fokus. Baterai tahan +-4,5 - 10 menit tergantung kesulitan, baru meredup di bawah 20% dan tersendat di bawah 8%.
Tangan hanya memegang senter di depan saat senter dipakai: dimatikan (F), baterai habis, atau bersembunyi -> senter menunduk lalu tangan turun keluar layar; dinyalakan -> tangan terangkat dan sinar menyala seiring senter diarahkan ke depan.

### Dynamic Difficulty Adjustment (`Scripts/DDA`)
- `DDAManager`: indikator tiap 8 detik (tempo relik dibanding jadwal, waktu dikejar, baterai, waktu sembunyi, nyaris tertangkap) -> skor **P** -> dihaluskan (EMA) -> `d += (P - 0,62) x 0,1`, dibatasi +0,15 / -0,3 per rumah. Event: tertangkap -0,15, relik +0,03, lolos +0,08. Evaluasi berhenti saat jeda atau hantu dibekukan.
- `DdaMap`: d -> parameter hantu (kecepatan, pandang, dengar, lama mencari, bobot belajar, lama berjaga, eksplorasi), pengurasan baterai senter, dan parameter PCG rumah berikutnya (jumlah lantai, ukuran, tempat sembunyi, baterai, relik, ruang terkunci, jarak relik-tempat sembunyi, proporsi lampu menyala).
- Eksperimen bot (Pro / Sedang / Lemah, dengan vs tanpa DDA, 192 rumah): `Hasil_Eksperimen_DDA.md` - ringkasan di bagian Penelitian.

Alur integrasi: Performa Pemain -> DDA -> parameter Game AI + parameter PCG -> perubahan gameplay, ditambah memori belajar hantu sebagai adaptasi jangka panjang.

## Penelitian (`Scripts/Research`)
Tiga alat untuk membuktikan bahwa DDA dan hantu yang belajar benar-benar bekerja:

1. **Uji coba pemain** (Menu Utama -> **UJI COBA PEMAIN**) untuk 8-12 peserta. Desain *within-subject*: tiap peserta bermain latihan (dengan panduan awal) lalu **Sesi A** dan **Sesi B** (8 menit), satu memakai DDA dan satu kesulitan tetap. Urutan diseimbangkan otomatis (nomor ganjil DDA dulu) dan disamarkan sebagai "Sesi A/B". Seed rumah, kesulitan awal, dan memori hantu disamakan. Setelah tiap sesi ada kuesioner 8 butir (seru, tegang, kesulitan pas, tingkat kesulitan, frustrasi, mampu, hantu adaptif, ingin main lagi), di akhir ada 3 pertanyaan perbandingan. Semua tercatat otomatis ke `DataUjiCoba/Pxx/` (`rumah.csv`, `kejadian.csv`, `belajar.csv`, `kuesioner.csv`, `perbandingan.csv`, `info.txt`). Simpanan dan memori hantu pemilik komputer dicadangkan lalu dikembalikan. Analisis: **Tools > Hantu Belajar > Analisis Data Uji Coba** -> `Hasil_Uji_Coba.md` (Wilcoxon berpasangan, uji binomial, McNemar, selang 95%, grafik). Panduan lengkap: [`PROTOKOL_UJI_COBA.md`](PROTOKOL_UJI_COBA.md).
2. **Eksperimen DDA** (bot, 3 profil x DDA/Tetap x 4 replikasi x 8 rumah = 192 rumah, seed berpasangan) -> [`Hasil_Eksperimen_DDA.md`](Hasil_Eksperimen_DDA.md):
   - DDA menaikkan tingkat lolos Pro 22% -> 63% (Fisher p = 0,002), Sedang 28% -> 41% (p = 0,43), Lemah 3% -> 13% (p = 0,36).
   - Kesulitan akhir mengikuti keterampilan: Pro 0,41, Sedang 0,30, Lemah 0,20 (semua mulai 0,40).
   - Temuan jujur: jarak indikator **P** ke target 0,62 tidak membaik (p = 0,91) karena P rata-rata rumah yang berakhir tertangkap tidak lebih rendah daripada yang lolos - penangkapan terjadi tiba-tiba setelah jendela evaluasi yang tenang. DDA tetap bekerja lewat kejadian tertangkap/lolos; perbaikan P disarankan di laporan.
3. **Eksperimen hantu belajar** (bot berkebiasaan 85% memilih lemari vs bot acak, 10 replikasi x 6 rumah, d = 0,60) -> [`Hasil_Eksperimen_Belajar.md`](Hasil_Eksperimen_Belajar.md). Setiap keputusan hantu yang memakai memori dibandingkan dengan pilihan **acak** pada lintasan pemain yang sama (`LearningProbe`, kontrafaktual):
   - Prediksi tempat sembunyi: keunggulan atas acak +0,107 pada pemain berkebiasaan vs +0,041 pada pemain acak (2,6x, Mann-Whitney p < 0,001).
   - **Jaga** tepat 65% vs acak 49% (McNemar p = 0,020) hanya pada pemain berkebiasaan; pada pemain acak tidak berbeda.
   - **Hadang** (belajar dari jalur) tepat 41% vs acak 27% (gabungan, p = 0,010).

Grafik SVG ada di `Analisis/`. Statistik dihitung di C# tanpa pustaka luar (`Stats`: selang Wilson & t, Fisher eksak, Mann-Whitney, Wilcoxon eksak/normal, McNemar eksak, binomial, bootstrap). Bot `AutoPilot` memakai profil keterampilan (persepsi, waktu reaksi, keputusan) dengan navigasi dan penghindar perabot yang sama untuk semua profil.

## Pengujian otomatis (Tools > Hantu Belajar > Uji)
- **Screenshot visual** -> `TestOutput/shots/` (semua layar menu, HUD, peta, panduan)
- **Alur pemain** -> klik tombol UI sungguhan: menu -> panduan -> panduan awal -> peta -> jeda/pengaturan -> kunci & gembok -> menang -> rumah berikutnya -> menu -> lanjutkan
- **Apakah hantu tahu pemain bersembunyi** -> 9 cek (tidak terlihat, tidak ditemukan saat diperiksa, berjaga lalu pergi, heatmap tidak mencatat)
- **Informasi HUD** -> 17 cek: indikator hantu stabil walau hantu mendengar berkali-kali, tidak ada notifikasi berulang, pesan relik tidak terpotong, notifikasi ditahan saat judul rumah / peta penuh, tidak ada panel yang saling menutupi pada layar 16:9 & 16:10 (termasuk mode presentasi); screenshot di `TestOutput/shots/hud/`
- **Kerapian tangga** -> 30 rumah: tidak ada benda di ruang lubang tangga, kamera pemain tidak menembus apa pun saat naik, pemain bisa naik/turun di 3 lajur; render sudut pandang pemain + potongan samping + denah di `TestOutput/shots/stairs/`
- **Bot bermain (smoke)** -> `TestOutput/report_smoke.txt` (error, macet, posisi di luar batas)
- **Eksperimen DDA** -> `Hasil_Eksperimen_DDA.md` + `Analisis/dda_*.svg` (env `HB_REPS`, `HB_HOUSES`, `HB_SCALE`, `HB_PROFILES`; bisa per potongan dengan `HB_REP_FROM`/`HB_REP_TO`, data digabung dari `TestOutput/dda_rows/`)
- **Eksperimen hantu belajar** -> `Hasil_Eksperimen_Belajar.md` + `Analisis/belajar_*.svg`
- **Kalibrasi keterampilan bot** -> `TestOutput/kalibrasi_bot.txt` (urutan Pro / Sedang / Lemah tanpa DDA, jejak pengejaran)
- **Uji coba pemain (peserta simulasi)** -> 28 cek: bot menjalani seluruh alur uji coba lewat tombol UI (pengaturan, panduan, latihan, sesi A/B, kuesioner, perbandingan), urutan seimbang, CSV lengkap, simpanan pemilik dikembalikan, laporan analisis berlabel SIMULASI
- **Susun ulang laporan eksperimen** -> menulis ulang kedua laporan dari data mentah tersimpan tanpa memainkan ulang
- **Eksperimen PCG** -> `Hasil_Eksperimen_PCG.md`

## Kredit aset
- Model hantu: Kenney Graveyard Kit "character-ghost" (CC0).
- Stealth Game: Haunted House (Unity Technologies), Unnerving Ambient Sounds (DHSFX), Horror Music - Free Starter Kit (Fictium Sound Design) dari Unity Asset Store.
