<p align="center">
  <picture>
    <!-- The full logo's navy lettering disappears on GitHub's dark theme, so dark mode shows the mark only -->
    <source media="(prefers-color-scheme: dark)" srcset="assets/brand/evseq-logo.png">
    <img src="assets/brand/evseq-full-logo.png" alt="Event Sequencer (EVSEQ)" width="240">
  </picture>
</p>

# Event Sequencer (EVSEQ)

Pemutar audio multi-track untuk operator event (wedding, seminar, gathering, siaran), native Windows.
Dua jalur output terpisah: **MAIN** untuk audiens dan **MONITOR** untuk operator mendengarkan (cue) track berikutnya tanpa bocor ke sound system.

*A native Windows multi-track audio player for live event operators, with separate Main (audience) and Monitor (cue) outputs.*

## Fitur

- **Dua output** WASAPI di device berbeda (mis. sound system + headphone), bisa diganti saat live tanpa memutus output lain.
- **Satu daftar track**: tiap track punya tombol **Cue** (ke Monitor) dan **Play** (ke Main). Track *overlay* (jingle/SFX) bisa main di atas track utama.
- **Auto fader**: fade-in/fade-out per track atau default global, kurva linear / equal-power, fade otomatis menjelang end point.
- **Start/end point** per track, bisa di-drag langsung di seek bar.
- **Seek bar dengan waveform** yang dibentuk oleh kurva fade, penanda start/end, dan label waktu saat hover.
- **Fader** per track dan master MAIN/MONITOR bergaya mixing console.
- **Shortcut** lokal & global (jalan walau jendela tidak fokus), per track dan untuk transport MAIN/CUE; **PANIC** menghentikan semua seketika.
- **MIDI controller** dengan MIDI learn; **StreamDeck** lewat aksi Hotkey ke shortcut global.
- **Project** (`.approj`, JSON) dengan autosave, pemulihan setelah crash, dan daftar project terakhir.
- **Panel docking** yang bisa dipindah, ditumpuk, atau dilepas ke monitor lain; layout diingat.
- Format: WAV, MP3, AIFF, FLAC, OGG.

## Instalasi

Unduh `EVSEQ-Setup-x.y.z.exe` dari halaman **Releases**, lalu jalankan. Installer bersifat offline dan tidak membutuhkan instalasi .NET terpisah.
Installer belum ditandatangani secara digital, jadi Windows SmartScreen mungkin menampilkan peringatan ("More info" → "Run anyway").

Kebutuhan: Windows 10 atau 11, 64-bit.

## Build dari source

Butuh [.NET SDK 10](https://dotnet.microsoft.com/download).

```powershell
dotnet test                                   # unit test engine audio
dotnet run --project src/AudioPlayer.App      # jalankan aplikasi
.\installer\build.ps1                         # test + publish + installer (butuh Inno Setup 6)
```

Struktur:

- `src/AudioPlayer.Core` — engine audio (NAudio), model project, MIDI; tanpa UI, bisa dites tanpa soundcard.
- `src/AudioPlayer.App` — aplikasi WPF.
- `tests/AudioPlayer.Core.Tests` — unit test.
- `installer/` — skrip Inno Setup.

## Lisensi

[MIT](LICENSE) © 2026 Imagaa. Komponen pihak ketiga dan lisensinya tercantum di [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
