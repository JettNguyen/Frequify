# Frequify

_by [Jett2Fly](https://jett2fly.com)_

A Windows desktop app for easy, transparent, and adjustable audio mastering.

## What it does

- Loads WAV and MP3 files
- Analyzes loudness and peak metrics
- Applies a mastering chain (EQ, compression, saturation, stereo, limiter, loudness)
- Lets you A/B preview original vs mastered audio
- Exports the mastered result as WAV

## Install (Recommended)

1. Open the [latest release](https://github.com/JettNguyen/Frequify/releases/latest)
2. Download `Frequify-win-x64-portable.zip`
3. Extract the ZIP to your desired folder (e.g.: `Desktop` or `Documents`)
4. Run `Frequify.exe`

No internet connection is required after download.

## System requirements

- Windows 10 or Windows 11 (64-bit)
- Audio output device (speakers/headphones)

## Quick start

1. Launch `Frequify.exe`
2. Load a track using either:
   - **Load Audio** button, or
   - Drag and drop a `.wav` or `.mp3` file into the window
3. Click **Analyze** to inspect loudness/peak metrics
4. Select a mastering preset or use the sliders for precise adjustments
5. Click **Apply Mastering** to create a mastered version
6. Use **Original View** / **Mastered View** and playback controls to A/B compare
7. Click **Export WAV** to save the mastered file

## Notes

- Input formats: `.wav`, `.mp3`
- Export format: `.wav`
- Processing is fully local (offline)

## Troubleshooting

- **App does not open**
  - Re-extract the ZIP and run from a normal folder (not inside Downloads ZIP preview)
- **Cannot load audio**
  - Confirm the file is a valid `.wav` or `.mp3`
- **No sound during preview**
  - Check Windows output device and app volume
- **Export fails**
  - Try a different save location (for example Desktop) and ensure the file is not open elsewhere
