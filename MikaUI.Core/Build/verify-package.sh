#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
package_dll="MikaUI.Unity/Assets/MikaUI/Runtime/Plugins/MikaUI.Core.dll"

dotnet test "$repo_root/MikaUI.Core.Test/MikaUI.Core.Test.csproj" \
  --configuration Release --nologo -p:NuGetAudit=false

if ! git -C "$repo_root" diff --quiet HEAD -- "$package_dll"; then
  echo "The packaged Core DLL differs from the committed DLL. Rebuild and commit it." >&2
  exit 1
fi
