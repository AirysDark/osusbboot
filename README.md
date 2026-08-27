# AirysDark WinISO USB Creator

Visual Studio 2022 / .NET 8 WPF project.

## What it does
- Requires Administrator elevation.
- Detects USB disks.
- Uses DiskPart to erase the selected USB.
- Creates a 4 GB FAT32 `BOOT` partition.
- Creates a remaining-space exFAT `WINISO` partition.
- Copies the selected ISO as `I:\Windows10.iso`.
- Copies prepared WinPE files from a `WinPE` folder beside the executable to the boot partition.

## Important
Windows setup cannot natively boot an arbitrary ISO just because the ISO sits on a second partition. For a fully bootable installer, prepare a WinPE boot environment with the Windows ADK + WinPE Add-on, then place the resulting WinPE media files in a `WinPE` folder beside the built executable.

## Build
Open `WinIsoUsbCreator.sln` in Visual Studio 2022 and build/run.
