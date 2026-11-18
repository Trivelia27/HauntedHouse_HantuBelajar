# Haunted House: Hantu yang Mempelajari Ketakutanmu

3D First-Person Survival Horror (Unity 6000.0.83f1, URP) dengan **Procedural Content Generation**, **Dynamic Difficulty Adjustment**, dan **Game AI** (hantu yang mempelajari kebiasaan pemain).

Pemain terjebak di rumah berhantu dan harus mengumpulkan relik untuk membuka pintu keluar. Pemain bisa berjalan, berlari (stamina), berjongkok, memakai senter (baterai terbatas), dan bersembunyi di lemari / kolong ranjang. Hantu mengingat tempat sembunyi dan jalur favorit pemain, dan tingkat kesulitan menyesuaikan performa pemain.

## Menjalankan
1. Buka folder ini dengan Unity **6000.0.83f1**.
2. Import paket Asset Store dari *Window > Package Manager > My Assets* (tidak disertakan di repo karena lisensi Asset Store):
   - Stealth Game: Haunted House | 3D Sample Project
   - Unnerving Ambient Sounds - Horror Game Sound Effect Pack
   - Horror Music - Free Starter Kit
3. Menu **Tools > Hantu Belajar > 2. Pindai Aset + Bangun Scene**.
4. Buka `Assets/Scenes/HauntedHouse.unity`, tekan Play.

Tanpa paket Asset Store game tetap jalan dengan model dan suara bawaan (fallback).

## Kontrol
| Tombol | Fungsi |
|---|---|
| WASD / Shift / C | jalan / lari / jongkok |
| F | senter |
| E | ambil relik/baterai, sembunyi / keluar |
| Tab / M | panel PCG-DDA-AI / peta (heatmap) |
| G | rumah baru (PCG) |
| F2 / F3 | DDA turun / naik (demo) |
| F5 / F6 / F9 | bekukan hantu / panggil hantu / reset memori hantu |

## Sistem
- **PCG** (`HouseGenerator.cs`): BSP membagi denah -> graf ketetanggaan ruang -> spanning tree Kruskal + pintu loop -> start/exit dari kedalaman BFS -> relik, tempat sembunyi, baterai, perabot dengan generate-and-test (cek konektivitas) -> validasi BFS + retry seed. Parameter: seed, ukuran, ukuran ruang min/maks, peluang loop, jumlah relik, tempat sembunyi, baterai, kepadatan perabot.
- **Game AI** (`GhostAI.cs`): FSM Patroli / Hadang / Selidiki / Kejar / Cari / Periksa, A* di grid, penglihatan (kerucut + raycast) dan pendengaran (suara langkah). `GhostMemory` mencatat heatmap jalur pemain dan pemakaian tempat sembunyi (per tempat, per jenis, per tipe ruangan; lintas rumah) -> urutan pemeriksaan dan peluang menemukan pemain.
- **DDA** (`DDAManager.cs`): indikator tempo relik, waktu dikejar, baterai, waktu sembunyi, nyaris tertangkap -> skor P -> `d += (P - 0.5) * 0.12` tiap 8 detik + event (tertangkap -0.15, relik +0.04, lolos +0.10). Output: parameter AI hantu (kecepatan, pandang, dengar, cari, bobot belajar, peluang periksa), habisnya baterai, dan parameter PCG rumah berikutnya.

Alur integrasi: Player Performance -> DDA -> parameter PCG & Game AI -> perubahan gameplay.

## Kredit aset
- Model hantu: Kenney Graveyard Kit "character-ghost" (CC0).
- Stealth Game: Haunted House (Unity Technologies), Unnerving Ambient Sounds (DHSFX), Horror Music - Free Starter Kit (Fictium Sound Design) dari Unity Asset Store.
