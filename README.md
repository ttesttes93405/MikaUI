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
