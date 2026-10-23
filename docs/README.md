<p align="center">
  <img src="../assets/logo.png" alt="Fashion Flow AVI Converter" width="140">
</p>

<h1 align="center">Fashion Flow AVI Converter</h1>

<p align="center">
  Turns normal movie files (MP4, MKV, MOV, WMV, WEBM, big AVI, ...) into AVI files<br>
  that small / cheap projectors actually play.<br><br>
  <strong>Powered by Fashion Flow 2026</strong> &nbsp;|&nbsp; Developed by <strong>Enquzer Getachew</strong>
</p>

---

A native Windows app. **No PowerShell, no Python, no extra software to install.**
FFmpeg is bundled in `bin\`, everything runs on your PC, and nothing is uploaded.

* **`AVIConverter.exe`** - the window (drag and drop, presets, progress, log)
* **`avi-convert.exe`** - the command line version for scripts and batches
* Optional per-user install with Start Menu / Desktop shortcuts and an entry in
  **Settings > Apps**

---

## Requirements

* 64-bit Windows 10 or Windows 11
* .NET Framework 4.8 (already part of Windows 10/11)
* About 250 MB free disk space (mostly FFmpeg)

---

## Quick start

1. Extract the ZIP anywhere (for example `D:\Tools\AVI Converter`).
   Do not run it from inside the ZIP.
2. Double-click **`AVIConverter.exe`**.
3. Drag video files or whole folders onto the window.
4. Pick a preset (start with **Safe 480p**).
5. Press **Convert to AVI**.

Finished files are written next to each source file, or into the folder you
choose under "Save to". Files that are unticked are left alone. A running
conversion can be cancelled; the partial `.avi` is deleted.

When a batch finishes, a green **✓ Complete** appears next to the progress bar,
and each successful file is marked **✓ Done** in the list. If the window is in
the background, the taskbar button flashes and a small notification appears.
Failures show **! Finished with errors**; cancellation shows **Cancelled**.

### Drag and drop onto a file

Drop one or more videos (or folders) onto
**`tools\Convert to AVI (drag and drop).bat`**.

It converts them with the Safe 480p preset into the same folder as the source.
A console window shows the progress and stays open at the end.

### Command line

```bat
:: one file
avi-convert.exe "D:\Movies\Inception.mkv"

:: a whole folder, 720p, into another drive
avi-convert.exe "D:\Movies" -Preset hd -Recurse -OutDir "E:\USB"

:: shrink a big AVI
avi-convert.exe "D:\old\big movie.avi" -Preset small -Force

:: see all presets
avi-convert.exe -ListPresets

:: version and credits
avi-convert.exe -Version
```

Open Command Prompt in the app folder and run `avi-convert.exe ...`.
Both `-Option` and `--option` spellings work.

---

## Install on this PC (optional)

The app runs fine from any folder, USB stick or network share. If you want it
in the Start Menu and in **Settings > Apps**, install it for your user:

1. Double-click **`Install.bat`** (no administrator rights needed), or press
   **Install app...** inside the window.
2. It copies the app to
   `%LOCALAPPDATA%\Programs\FashionFlow\AVI Converter`, creates a
   **Start Menu** shortcut and a **Desktop** shortcut, and registers
   *Fashion Flow AVI Converter* in **Settings > Apps**.
3. Start it from the Start Menu: **Fashion Flow > AVI Converter**.
4. Uninstall it from **Settings > Apps**, from
   **Start Menu > Fashion Flow > Uninstall AVI Converter**, or by
   double-clicking **`Uninstall.bat`** in the app folder.

The install is per-user. The installed copy includes its own `bin\ffmpeg.exe`,
so the original download folder can be deleted afterwards.

To build a shareable ZIP package (app + FFmpeg + installer):

```bat
build\Build-Package.bat
```

That writes `dist\FashionFlow-AVI-Converter-1.0.0.zip`. Extract it on any
64-bit Windows 10/11 PC and run `Install.bat`, or just run the app without
installing.

---

## Presets

| Preset  | Frame     | Video                 | Bitrate  | Use it when |
|---------|-----------|-----------------------|----------|-------------|
| `safe`  | 854x480   | MPEG-4 (Xvid tag)     | ~2.2 Mbit/s | **Default.** Widest compatibility, small files. |
| `hd`    | 1280x720  | MPEG-4 (Xvid tag)     | ~4.5 Mbit/s | Projector says 720p/1080p, you want more detail. |
| `4x3`   | 720x540   | MPEG-4 (Xvid tag)     | ~3.0 Mbit/s | Old 4:3 projector or TV. |
| `mjpeg` | 640x480   | MJPEG                 | quality-based | **Fallback.** For players that reject everything else. |
| `small` | 640x360   | MPEG-4 (Xvid tag)     | ~0.9 Mbit/s | USB stick is nearly full, or the source AVI is huge. |

All presets use:

* AVI container, MP3 stereo audio at 44.1 kHz (or PCM with `-Audio pcm`)
* square pixels and a fixed frame size, so nothing is squashed
* frame rate capped at 30 fps (25 for MJPEG)
* no B-frames, keyframe every ~8 s - easy for cheap decoders
* 8-bit 4:2:0 color
* automatic deinterlacing for interlaced sources

16:9 sources on a 4:3 preset (or the other way around) get black bars by
default. Use `-Fit crop` to fill the frame instead (cuts the edges) or
`-Fit stretch` to distort it.

---

## If the projector still says "unsupported format"

Try these in order:

1. **Use the MJPEG preset.** It is the codec most old players understand.
   ```bat
   avi-convert.exe "D:\movie.mp4" -Preset mjpeg -Force
   ```
2. **Go smaller.** Some players only handle 640x480 or 720x480:
   ```bat
   avi-convert.exe "D:\movie.mp4" -Preset small -Width 640 -Height 480 -Force
   ```
3. **Try 4:3** if the projector is an older 4:3 model: `-Preset 4x3`.
4. **USB stick / SD card:** use FAT32, keep the file under 4 GB (some players
   stop at 2 GB), and put it in a simple folder. Avoid very long file names and
   unusual characters on very old firmware.
5. **No sound?** Some players cannot decode MP3 inside AVI. Re-run with
   uncompressed audio:
   ```bat
   avi-convert.exe "D:\movie.mp4" -Audio pcm -Force
   ```
6. **Plays but stutters?** Use the `small` preset or lower the resolution.

If none of that works, note the exact model of the projector - the supported
codec list decides which preset will work.

---

## Command line options

| Option | Meaning |
|--------|---------|
| `-Preset safe\|hd\|4x3\|mjpeg\|small` | Output quality/compatibility preset. |
| `-Fit pad\|crop\|stretch` | How to fit a different aspect ratio (default `pad`). |
| `-OutDir <folder>` | Where to write the .avi files. Default: next to the source. |
| `-Suffix <text>` | Add text to the output name, e.g. `_proj`. |
| `-Width`, `-Height` | Override the preset frame size (even numbers only). |
| `-VideoKbps <n>` | Override the video bitrate. |
| `-AudioKbps <n>` | Override the MP3 bitrate. |
| `-Audio mp3\|pcm` | Audio codec. `pcm` is uncompressed and much bigger. |
| `-NoAudio` | Drop the audio track completely. |
| `-Fps <n>` | Force a frame rate instead of using the source rate. |
| `-Recurse` | Include sub-folders when a folder is given. |
| `-Force` | Overwrite existing output files without asking. |
| `-NoClobber` | Never overwrite, just skip. |
| `-DryRun` | Show the ffmpeg command without encoding. |
| `-ListPresets` | Print the presets and exit. |
| `-Version` | Print the version, credits and exit. |

Exit codes: `0` success, `1` at least one file failed, `2` setup problem
(FFmpeg missing, bad preset).

Installer / maintenance commands:

```bat
avi-convert.exe --install [--quiet]
avi-convert.exe --uninstall [--quiet]
avi-convert.exe --setup-ffmpeg
avi-convert.exe --package
```

---

## Files in the app folder

The root is deliberately small: only the four things you normally need are
there. Everything else lives in its own folder.

```
AVIConverter.exe          run the window (portable, no install needed)
avi-convert.exe           command line converter, installer and packager
Install.bat               per-user installer (Start Menu, Desktop, Apps list)
Uninstall.bat             removes the installed copy

assets\                   logo.png, icon.ico and other artwork
bin\                      bundled FFmpeg 9.0 (ffmpeg.exe, ffprobe.exe)
bin\README-FFmpeg.txt     FFmpeg license note
build\                    Build.bat and Build-Package.bat
docs\README.md            this file
src\                      C# source code
tools\                    Convert to AVI (drag and drop).bat
dist\                     the distributable ZIP (created by Build-Package.bat)
```

In the ZIP package the README sits at the package root instead of `docs\`.

The app is portable: you can copy this folder anywhere and run it. Installing
is optional and only adds shortcuts and an entry in **Settings > Apps**.

---

## Notes and limits

* **Speed:** re-encoding is CPU work. A 2-hour movie usually takes 5-25 minutes
  depending on the preset and your CPU. The window shows percent and the speed
  factor (for example `18x` = faster than real time).
* **File size:** MPEG-4 at 480p is roughly 1 GB per hour. `small` is about
  0.4 GB per hour. MJPEG is much larger.
* **2 GB / 4 GB limits:** the converter warns when the result is above those
  sizes, because many projectors and FAT32 sticks cannot read bigger files.
  For long movies use the `small` preset or split the movie.
* **Interlaced sources** (DVDs, old TV captures) are deinterlaced automatically.
* **HDR sources** are converted to normal 8-bit video without tone mapping, so
  they can look a little flat/washed out.
* Subtitles, chapters and extra audio tracks are dropped - AVI projectors do
  not use them.
* Copy-protected / DRM files (iTunes, some streaming downloads) cannot be read
  by FFmpeg.
* Input files are never modified. If the source is already an .avi and the
  output would have the same name, the result is named `name.converted.avi`.

---

## Restoring or updating FFmpeg

The Git source repository does not store the FFmpeg binaries because each one
is larger than GitHub's 100 MB file limit. After cloning, run the command
below, or use the packaged ZIP which already includes them.

If `bin\` is empty or you want a newer FFmpeg:

```bat
avi-convert.exe --setup-ffmpeg
```

It downloads the "essentials" build from gyan.dev (about 110 MB) and puts
`ffmpeg.exe` / `ffprobe.exe` into `bin\`. The tool also works with an FFmpeg
that is already on your `PATH`.

---

## Build from source

The source is plain C# 5, compiled with the C# compiler that ships with
.NET Framework 4.8 (no Visual Studio or .NET SDK required):

```bat
build\Build.bat
```

This produces `AVIConverter.exe` and `avi-convert.exe` in the app folder. The
build embeds `assets\logo.png` and the Windows icon. To rebuild the
distributable ZIP:

```bat
build\Build-Package.bat
```

That writes `dist\FashionFlow-AVI-Converter-1.0.0.zip`.

---

## Credits

* **Fashion Flow** - product brand and visual identity. *Powered by Fashion Flow 2026.*
* **Enquzer Getachew** - design and development.
* **FFmpeg** - encoding engine (bundled build from gyan.dev). FFmpeg is
  licensed under the GPLv3; see `bin\README-FFmpeg.txt` and
  <https://ffmpeg.org>.

Version 1.0.0 | (c) 2026 Fashion Flow
