# Raja Sawit

Game strategi kebun sawit untuk HP, portrait, tampilan isometrik dari atas. Isinya satir tentang sistem: bakar hutan biar murah, ubah aturan lewat Dewan, pamer harta. Semua tokoh, fraksi, dan merek di dalam game adalah fiksi.

- Game design document dan roadmap ada di Claude Docs (link di thread Claude Duet).
- Engine: Unity 6.3 LTS. Target pertama: Android.

## Status: Fase 0 · Fondasi

Yang sudah ada:

- **Simulasi sawit** dalam C# murni (`Assets/_Project/Scripts/Sim`): tanam, tumbuh 3 tahap, panen TBS tiap 20 menit, TBS membusuk setelah 2 jam, truk angkut, PKS dengan rendemen 20–24%, tangki CPO, harga CPO harian, hama, pupuk, buka lahan 3 cara (beli, mekanis, bakar), api menjalar mengikuti angin, meter Asap dan Sorotan, Razia, naik jabatan level 1–6.
- **15 tes EditMode** untuk simulasi, termasuk tes ekonomi 7 hari.
- **Save lokal** JSON dan progres offline (paling lama 8 jam dikejar).
- **Peta isometrik 64x64** dengan art placeholder yang dibuat dari kode, kamera geser dan cubit untuk zoom.
- **HUD portrait** placeholder: sumber daya, Asap, Sorotan, bottom sheet untuk petak yang diketuk, tombol Panen semua, Jual CPO, Naik jabatan.

Belum ada: art final, Dewan, Harta, rival NPC, suara. Itu masuk Fase 2 dan 3.

## Membuka project

Unity Editor tidak berjalan di cloud, jadi langkah ini dilakukan di komputermu.

1. Pasang Unity 6.3 LTS lewat Unity Hub, dengan modul **Android Build Support** (termasuk OpenJDK dan Android SDK/NDK).
2. Clone repo ini, lalu pindah ke branch fase:
   ```
   git clone https://github.com/Zersya/raja-sawit-duet.git
   cd raja-sawit-duet
   git checkout fase-0-fondasi
   ```
3. Di Unity Hub pilih **Add > Add project from disk**, arahkan ke folder `raja-sawit-duet`. Kalau Hub bilang versi 6000.3.0f1 tidak terpasang, pilih versi 6.3 mana saja yang kamu punya.
4. Pembukaan pertama akan mengunduh package dari `Packages/manifest.json` (Input System, 2D Sprite, 2D Tilemap, uGUI, Test Framework). Kalau Unity menanyakan **Enable the new Input System backends**, jawab **Yes**; Editor akan restart.
5. Di menu atas, jalankan **Raja Sawit > 1. Siapkan Project**, lalu **Raja Sawit > 2. Buat Scene Home**.
6. Buka `Assets/_Project/Scenes/Home.unity` dan tekan **Play**. Di Game view pilih resolusi portrait, misalnya 1080x1920.

Cara main di Editor: klik dan seret untuk geser peta, scroll untuk zoom, klik petak untuk memilih.

Untuk mempercepat waktu saat mencoba, pilih objek **GameSession** dan ubah **Time Scale** (misalnya 60, berarti satu detik nyata menjalankan satu jam game).

## Build ke HP Android

1. **File > Build Profiles**, pilih **Android**, lalu **Switch Platform**.
2. Sambungkan HP dengan USB debugging aktif, lalu **Build And Run**.

## Menjalankan tes

- Di Unity: **Window > General > Test Runner**, tab **EditMode**, **Run All**.
- Tanpa Unity (butuh .NET 8 SDK): `dotnet test tools/sim-check`. Ini mengompilasi folder `Sim` dan tesnya saja.

## Struktur

```
Assets/_Project/
  Scripts/
    Sim/        aturan sawit, C# murni, tanpa UnityEngine
    Data/       BalanceConfigAsset (ScriptableObject)
    Core/       GameSession (tick 1 detik), SaveService
    View/       peta isometrik, kamera sentuh, art placeholder
    UI/         HUD, bottom sheet, interaksi ketuk
    Editor/     menu Raja Sawit
  Config/       BalanceConfig.asset (dibuat oleh menu Siapkan Project)
  Scenes/       Home.unity (dibuat oleh menu Buat Scene Home)
  Tests/EditMode/
tools/sim-check/  tes simulasi lewat dotnet
```

Satu tick sama dengan satu detik nyata sama dengan satu menit game, jadi satu hari game berlangsung 24 menit.

## Catatan

- Semua angka balance ada di `Assets/_Project/Config/BalanceConfig.asset`. Setelah aset itu dibuat, nilai di Inspector yang dipakai, bukan nilai default di kode.
- Aset gambar dan suara memakai Git LFS (lihat `.gitattributes`). Pasang Git LFS sebelum menambah art.
- URP 2D dan TextMeshPro sengaja belum dipasang. Keduanya masuk saat art final di Fase 2.
- Commit juga file `.meta` yang dibuat Unity, jangan di-ignore.
