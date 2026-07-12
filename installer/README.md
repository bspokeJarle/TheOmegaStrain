# The Omega Strain Installer

This folder contains the installer build setup for The Omega Strain.

## Requirements

- .NET SDK matching the project target
- Inno Setup 6, unless you only want to publish with `-SkipInno`
- `%APPDATA%\OmegaStrain\secrets.json`

The secrets file is copied into the installer payload and installed to:

```text
%APPDATA%\OmegaStrain\secrets.json
```

The installer uses `onlyifdoesntexist`, so an existing local secrets file is not overwritten during upgrades.

## Build

From the repository root:

```powershell
.\installer\Build-Installer.ps1 -Version 1.0.0
```

To publish and stage files without compiling the Inno installer:

```powershell
.\installer\Build-Installer.ps1 -Version 1.0.0 -SkipInno
```

Output is written under:

```text
artifacts\installer\
```

## Artwork

The installer uses `Assets\InstallerWizardBackImage.bmp` as an opaque, pre-faded
background. Keep `InstallerWizardBackImage.png` as the source artwork, but do not
point Inno directly at it with runtime opacity. The baked BMP avoids repainting
artifacts on some Windows DPI/theme combinations.
