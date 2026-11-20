# Protokol Uji Coba Pemain - Haunted House: Hantu yang Mempelajari Ketakutanmu

Panduan menjalankan uji coba dengan pemain sungguhan untuk menjawab dua pertanyaan:

1. Apakah **DDA** membuat permainan lebih seru dan tingkat kesulitannya lebih pas dibanding **kesulitan tetap**?
2. Apakah **hantu benar-benar mempelajari kebiasaan** pemain sungguhan (lebih tepat dari tebakan acak)?

Semua pencatatan, urutan kondisi, kuesioner, dan analisis statistik sudah ada di dalam game. Peneliti cukup menyiapkan peserta dan menjalankan mode **UJI COBA PEMAIN**.

## Desain

| Aspek | Ketentuan |
|---|---|
| Peserta | 8-12 orang (minimal 8 agar uji statistik bermakna), belum pernah memainkan game ini |
| Desain | *within-subject*: tiap peserta memainkan **dua sesi**, satu dengan DDA dan satu dengan kesulitan tetap (d = 0,40) |
| Urutan | diseimbangkan otomatis: nomor peserta **ganjil = DDA dulu**, **genap = Tetap dulu** |
| Penyamaran | peserta hanya melihat "SESI A" dan "SESI B"; label kesulitan tidak ditampilkan |
| Kesetaraan | seed rumah sama untuk semua peserta (latihan 31000, Sesi A 41000, Sesi B 52000); tiap sesi mulai dari d = 0,40 dengan memori hantu kosong |
| Durasi | latihan 3 menit (1 rumah, dengan panduan awal) + 2 x 8 menit + kuesioner ±5 menit -> **±25-30 menit per peserta** |

## Persiapan

1. Build game: **Tools > Hantu Belajar > Build Windows (.exe)** -> `Builds/Windows/HauntedHouse.exe`.
2. Cek alur sekali dengan peserta simulasi: **Tools > Hantu Belajar > Uji > Uji coba pemain (peserta simulasi)** (bot memainkan seluruh alur, kuesioner diisi acak; hasilnya `TestOutput/Hasil_Uji_Coba_SIMULASI.md`).
3. Ruangan tenang, headphone, mouse. Gunakan komputer dan pengaturan (kecerahan, FOV, volume) yang **sama** untuk semua peserta.
4. Siapkan lembar persetujuan (lihat di bawah). Nama peserta **tidak** dimasukkan ke game; game hanya mencatat kode `P01`, `P02`, ...

## Persetujuan peserta (dibacakan sebelum mulai)

- Game ini bergenre horor dan memiliki *jumpscare* (kejutan tiba-tiba). Peserta dengan kondisi jantung, epilepsi fotosensitif, atau ketakutan berlebih sebaiknya tidak ikut.
- Peserta boleh berhenti kapan saja tanpa alasan (peneliti menekan Esc -> Menu Utama; data yang sudah tercatat tetap tersimpan dan ditandai "dibatalkan").
- Yang dicatat hanya data permainan (waktu, relik, tertangkap, sembunyi, dll.) dan jawaban kuesioner, dengan kode peserta tanpa nama.
- Tidak ada jawaban benar atau salah; yang dinilai adalah game-nya, bukan pemainnya.

## Langkah per peserta

1. Jalankan `HauntedHouse.exe` -> **Menu Utama -> UJI COBA PEMAIN**.
2. Layar pengaturan (untuk peneliti): nomor peserta terisi otomatis (nomor berikutnya yang belum dipakai), durasi sesi 8 menit, latihan 3 menit -> **MULAI UJI COBA**. Bila uji coba dilakukan di beberapa komputer, atur nomor peserta secara manual agar **tidak ada nomor ganda**.
3. Serahkan ke peserta. Game memandu sendiri:
   - **Panduan bermain** (4 halaman) -> **Latihan** (1 rumah dengan panduan awal; hantu tidur sampai langkah dasar selesai)
   - **Sesi A** (8 menit; sisa waktu tampil di kartu tujuan) -> **kuesioner** 8 butir
   - **Sesi B** (8 menit) -> **kuesioner** yang sama -> **perbandingan** 3 pertanyaan -> **Selesai**
4. Selama bermain peneliti **tidak membantu** dan tidak menyebut kondisi DDA/Tetap. Catat kejadian tak terduga di catatan terpisah (sertakan kode peserta).
5. Setelah layar Selesai, tekan **MENU UTAMA** untuk peserta berikutnya. Simpanan dan memori hantu milik pemilik komputer dikembalikan otomatis.

Selama uji coba, mode presentasi (F1/Tab) dan tombol pengembang (G, F2-F9) dinonaktifkan, dan progres permainan biasa tidak disimpan, agar kondisi tidak terganggu.

## Kuesioner (skala 1-5, diisi setelah tiap sesi)

| No | Butir | Skala |
|---|---|---|
| 1 | Permainan di sesi ini menyenangkan. | 1 sangat tidak setuju - 5 sangat setuju |
| 2 | Saya merasa tegang atau takut selama bermain. | idem |
| 3 | Tingkat kesulitannya pas dengan kemampuan saya. | idem |
| 4 | Menurut saya, permainan di sesi ini... | 1 terlalu mudah - **3 pas** - 5 terlalu sulit |
| 5 | Saya merasa frustrasi. | idem butir 1 |
| 6 | Saya merasa mampu menguasai permainan. | idem butir 1 |
| 7 | Hantu terasa menyesuaikan diri dengan cara saya bermain. | idem butir 1 |
| 8 | Saya ingin memainkan sesi ini lagi. | idem butir 1 |

Perbandingan setelah kedua sesi (Sesi A / Sama saja / Sesi B): sesi mana yang lebih seru, kesulitannya lebih pas, dan hantunya terasa lebih pintar.

## Data yang tercatat

Folder `DataUjiCoba/Pxx/` di samping exe (`Builds/Windows/DataUjiCoba/`), atau di folder proyek bila dijalankan dari editor. Ditulis langsung ke disk setiap kejadian, jadi aman bila game ditutup paksa.

| File | Isi |
|---|---|
| `info.txt` | kode peserta, urutan kondisi, durasi, seed, versi game, resolusi & pengaturan layar, status (selesai / dibatalkan) |
| `rumah.csv` | satu baris per rumah: hasil (lolos / tertangkap / waktu_habis / dibatalkan), durasi, relik, kunci, dikejar (jumlah & detik), sembunyi (jumlah & detik), nyaris tertangkap, kesulitan d awal-akhir, indikator DDA **P** rata-rata dan jaraknya ke target 0,62, persentase senter menyala, jarak tempuh |
| `kejadian.csv` | kejadian bertanda waktu: relik, kunci, gembok, mulai dikejar, sembunyi / keluar, evaluasi DDA (P, d), tertangkap, lolos |
| `belajar.csv` | ukuran "hantu belajar": tiap kali pemain bersembunyi, peluang tempat itu menurut memori hantu vs peluang acak; tiap keputusan hadang / jaga, apakah tepat vs pilihan acak pada lintasan yang sama |
| `kuesioner.csv` | jawaban 8 butir per sesi (kolom `kondisi` = DDA / Tetap) |
| `perbandingan.csv` | jawaban 3 pertanyaan perbandingan, sudah diterjemahkan ke kondisi (DDA / Tetap / sama) |

Angka di CSV memakai titik desimal (bisa dibuka di Excel/Sheets/R/Python).

## Analisis

1. Kumpulkan folder `Pxx` dari semua komputer ke `DataUjiCoba/` di folder proyek (atau biarkan di `Builds/Windows/DataUjiCoba/`; keduanya dibaca).
2. Unity: **Tools > Hantu Belajar > Analisis Data Uji Coba** -> `Hasil_Uji_Coba.md` + grafik `Analisis/ujicoba_kuesioner.svg`.
   Tanpa membuka editor: `Unity.exe -batchmode -nographics -projectPath <folder proyek> -executeMethod HantuBelajar.EditorTools.ResearchTools.AnalyzeBatch -quit`.
3. Isi laporan:
   - **Kuesioner**: rata-rata (selang kepercayaan 95%) DDA vs Tetap per butir, **uji peringkat bertanda Wilcoxon** berpasangan, efek r; jarak butir 4 ke "pas".
   - **Performa**: rumah dimainkan, tingkat lolos, tertangkap, dikejar, relik, |P - 0,62|, kesulitan akhir; Wilcoxon berpasangan.
   - **Preferensi**: jumlah peserta yang memilih DDA / Tetap / sama, **uji binomial**.
   - **Hantu belajar**: keunggulan prediksi tempat sembunyi atas acak (Wilcoxon vs 0), ketepatan hadang & jaga vs acak (**McNemar eksak**).
   - **Efek urutan**: perubahan Sesi A -> Sesi B (efek latihan), yang terbagi rata ke kedua kondisi berkat penyeimbangan urutan.
4. Hipotesis utama yang disarankan: butir **1 (seru)**, **3 (kesulitan pas)**, **5 (frustrasi)**. Butir lain bersifat eksploratif; menguji banyak butir sekaligus memperbesar peluang temuan kebetulan.

## Eksperimen bot pendukung (tanpa manusia)

- **Tools > Hantu Belajar > Uji > Eksperimen DDA** -> `Hasil_Eksperimen_DDA.md`: 3 profil bot x DDA/Tetap x replikasi x rumah, selang Wilson, uji Fisher, Mann-Whitney, bootstrap, grafik `Analisis/dda_*.svg`.
- **Tools > Hantu Belajar > Uji > Eksperimen hantu belajar** -> `Hasil_Eksperimen_Belajar.md`: bot berkebiasaan vs bot acak, keputusan hantu vs pilihan acak, grafik `Analisis/belajar_*.svg`.
- **Tools > Hantu Belajar > Uji > Kalibrasi keterampilan bot** -> `TestOutput/kalibrasi_bot.txt`: memeriksa urutan Pro > Sedang > Lemah tanpa DDA.

Variabel lingkungan (opsional): `HB_REPS` (replikasi), `HB_HOUSES` (rumah per replikasi), `HB_SCALE` (skala waktu; laporan saat ini memakai 16), `HB_PROFILES`. Pada komputer dengan RAM terbatas, jalankan per potongan dengan `HB_REP_FROM` / `HB_REP_TO` (mis. satu profil, 1-2 replikasi per kali); data tiap potongan disimpan di `TestOutput/dda_rows/` atau `TestOutput/learn_rows/` dan laporan disusun dari gabungan semua potongan. Untuk menjalankan cepat tanpa grafis: `Unity.exe -batchmode -nographics -projectPath <folder proyek> -executeMethod HantuBelajar.EditorTools.HarnessLauncher.RunDda -logFile dda.log`.
