# MikaUI

The repository has three project directories:

- `Core/` contains the only source for `MikaUI.Core` and its .NET project.
- `Core.Test/` contains the independent NUnit tests.
- `Unity/` contains the Unity project and the installable package at `Unity/Assets/MikaUI/`.

Build Core in Release mode to update the DLL inside the Unity package:

```sh
dotnet build Core/MikaUI.Core.csproj --configuration Release
```

Run the Core tests with:

```sh
dotnet test Core.Test/MikaUI.Core.Test.csproj --configuration Release
```

Before publishing, run `Core/Build/verify-package.sh`. It runs the Core tests,
rebuilds the DLL, and fails if the committed Unity package contains a different DLL.
Commit the updated DLL whenever Core source changes. Unity-specific tests remain in
the Unity Test Runner.

For a single Unity Package Manager installation, use this repository's Git URL
with `?path=/Unity/Assets/MikaUI`.
