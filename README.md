# MikaUI

MikaUI is a Unity UGUI framework for creating UI and managing its lifetime, including cleanup of child UI.

### Install with Package Manager

In Unity, open **Window > Package Manager**, click **+**, and select
**Add package from git URL**. Paste the following URL:

```text
https://github.com/ttesttes93405/MikaUI?path=/MikaUI.Unity/Assets/MikaUI#1.0.0
```

### Install with `manifest.json`

Alternatively, add the following entry to the existing `dependencies` object in
your Unity project's `Packages/manifest.json`:

```json
{
  "com.owensun.mika-ui": "https://github.com/ttesttes93405/MikaUI?path=/MikaUI.Unity/Assets/MikaUI#1.0.0"
}
```

### Project Structure

The repository has three project directories:

- `MikaUI.Core/` contains the only source for `MikaUI.Core` and its .NET project.
- `MikaUI.Core.Test/` contains the independent NUnit tests.
- `MikaUI.Unity/` contains the Unity project and the installable package at `MikaUI.Unity/Assets/MikaUI/`.

The Unity package identity is `com.owensun.mika-ui`. Core targets
.NET Standard 2.1 and ships as `Runtime/Plugins/MikaUI.Core.dll` in the package.

### Guide

- [English](MikaUI.Unity/Assets/MikaUI/Documentation/01-Introduction.md)
- [繁體中文](MikaUI.Unity/Assets/MikaUI/Documentation/01-Introduction.zh-TW.md)
