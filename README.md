# MikaUI

The repository has three project directories:

- `MikaUI.Core/` contains the only source for `MikaUI.Core` and its .NET project.
- `MikaUI.Core.Test/` contains the independent NUnit tests.
- `MikaUI.Unity/` contains the Unity project and the installable package at `MikaUI.Unity/Assets/MikaUI/`.

Build Core in Release mode to update the DLL inside the Unity package:

```sh
dotnet build MikaUI.Core/MikaUI.Core.csproj --configuration Release
```

For a single Unity Package Manager installation, use this repository's Git URL
with `?path=/MikaUI.Unity/Assets/MikaUI`.

The Unity package identity is `com.owensun.mika-ui` (version `1.0.0`). Core targets
.NET Standard 2.1 and ships as `Runtime/Plugins/MikaUI.Core.dll` in the package.

[繁體中文文件](MikaUI.Unity/Assets/MikaUI/Documentation/01-Introduction.zh-TW.md)

[English Documentation](MikaUI.Unity/Assets/MikaUI/Documentation/01-Introduction.md)
