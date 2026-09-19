#!/usr/bin/env bash
# Compiles the Unity client scripts with Roslyn against the assemblies a Unity
# editor install ships. This is a stand-in for opening the project in the editor,
# for machines that have the editor unpacked but no Unity licence (the editor
# refuses to open a project without one, exiting 198).
#
# It is an approximation, not a substitute: the define set below is a hand-built
# match for a Linux standalone player build, so conditional code could still
# differ from a real editor compile.
#
# Requirements:
#   - the .NET SDK (any 8.x), for Roslyn
#   - an unpacked Unity editor of the version in ProjectSettings/ProjectVersion.txt
#   - the Input System package sources
#   - the SpacetimeDB C# SDK sources
#
# Override any of these if they are not where the defaults expect:
#   UNITY_DATA    default $HOME/unity-editor/Editor/Data
#   INPUTSYSTEM   default $HOME/upm/package          (com.unity.inputsystem-<ver>.tgz, extracted)
#   STDB_SDK      default $HOME/sdk                  (com.clockworklabs.spacetimedbsdk checkout)
#
# uGUI is read from the editor's own built-in package, so it needs no download.
set -euo pipefail

PROJECT="${PROJECT:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}"
UNITY_DATA="${UNITY_DATA:-$HOME/unity-editor/Editor/Data}"
INPUTSYSTEM="${INPUTSYSTEM:-$HOME/upm/package}"
STDB_SDK="${STDB_SDK:-$HOME/sdk}"
OUT="${OUT:-$PROJECT/Temp/compile-client}"

PLAYER="$UNITY_DATA/PlaybackEngines/LinuxStandaloneSupport/Variations/mono/Managed"
UGUI="$UNITY_DATA/Resources/PackageManager/BuiltInPackages/com.unity.ugui"
BSATN_DIR="$STDB_SDK/packages/spacetimedb.bsatn.runtime"

for required in "$PLAYER" "$UGUI" "$INPUTSYSTEM/InputSystem" "$STDB_SDK/src" "$BSATN_DIR"; do
  if [[ ! -d "$required" ]]; then
    echo "missing: $required" >&2
    echo "see the header of $0 for the expected layout" >&2
    exit 1
  fi
done

CSC="$(ls -d "${DOTNET_ROOT:-$HOME/.dotnet}"/sdk/8.*/Roslyn/bincore/csc.dll 2>/dev/null | head -1)"
if [[ -z "$CSC" ]]; then
  CSC="$(ls -d /usr/share/dotnet/sdk/8.*/Roslyn/bincore/csc.dll 2>/dev/null | head -1)"
fi
if [[ -z "$CSC" ]]; then
  echo "could not find Roslyn csc.dll under a .NET 8 SDK" >&2
  exit 1
fi

CODEGEN="$(ls "$BSATN_DIR"/*/analyzers/dotnet/cs/SpacetimeDB.BSATN.Codegen.dll | head -1)"
BSATN="$(ls "$BSATN_DIR"/*/lib/netstandard2.1/SpacetimeDB.BSATN.Runtime.dll | head -1)"

rm -rf "$OUT"
mkdir -p "$OUT"

# Unity compiles player scripts against the netstandard 2.1 reference assemblies
# plus the engine modules from the target platform's playback engine.
ENGINE_REFS=()
for dll in "$UNITY_DATA/NetStandard/ref/2.1.0/netstandard.dll" \
           "$PLAYER/UnityEngine.dll" \
           "$PLAYER"/UnityEngine.*Module.dll \
           "$PLAYER"/Unity.*.dll; do
  [[ -f "$dll" ]] && ENGINE_REFS+=("-r:$dll")
done
for dll in "$UNITY_DATA/NetStandard/compat/2.1.0/shims"/*/*.dll; do
  [[ -f "$dll" ]] && ENGINE_REFS+=("-r:$dll")
done

DEFINES="UNITY_5_3_OR_NEWER;UNITY_2021_1_OR_NEWER;UNITY_2022_1_OR_NEWER;UNITY_2023_1_OR_NEWER"
DEFINES="$DEFINES;UNITY_6000_0_OR_NEWER;UNITY_STANDALONE;UNITY_STANDALONE_LINUX;UNITY_64"
DEFINES="$DEFINES;ENABLE_INPUT_SYSTEM;ENABLE_LEGACY_INPUT_MANAGER;PACKAGE_INPUTSYSTEM"
DEFINES="$DEFINES;PACKAGE_PHYSICS;PACKAGE_PHYSICS2D;PACKAGE_ANIMATION;PACKAGE_UITOOLKIT;PACKAGE_TILEMAP"
DEFINES="$DEFINES;UNITY_INPUT_SYSTEM_ENABLE_UI;UNITY_INPUT_SYSTEM_ENABLE_XR"
DEFINES="$DEFINES;UNITY_INPUT_SYSTEM_ENABLE_PHYSICS;UNITY_INPUT_SYSTEM_ENABLE_PHYSICS2D"
DEFINES="$DEFINES;UNITY_INPUT_SYSTEM_ENABLE_ANALYTICS;UNITY_INPUT_SYSTEM_PLATFORM_SCROLL_DELTA"
DEFINES="$DEFINES;UNITY_INPUT_SYSTEM_INPUT_MODULE_SCROLL_DELTA"
DEFINES="$DEFINES;UNITY_INPUT_SYSTEM_INPUT_MODULE_NAVIGATION_DEVICE_TYPE"
DEFINES="$DEFINES;UNITY_INPUT_SYSTEM_SENDPOINTERHOVERTOPARENT"
DEFINES="$DEFINES;UNITY_INPUT_SYSTEM_PLATFORM_POLLING_FREQUENCY"
DEFINES="$DEFINES;UNITY_INPUTSYSTEM_SUPPORTS_MOUSE_SCRIPT_EVENTS;UNITY_INPUTSYSTEM_SUPPORTS_FOCUS_EVENTS"

compile() {
  local name="$1"
  shift
  echo "### $name"
  dotnet "$CSC" -nologo -target:library -langversion:9 -nostdlib+ -noconfig \
    -define:"$DEFINES" -out:"$OUT/$name.dll" "$@"
}

sources() { find "$@" -name '*.cs' -not -path '*/Tests/*' -not -path '*Tests~*'; }

compile Unity.InternalAPIEngineBridge.004 "${ENGINE_REFS[@]}" \
  $(sources "$UGUI/Runtime/InternalBridge")

compile UnityEngine.UI "${ENGINE_REFS[@]}" \
  -r:"$OUT/Unity.InternalAPIEngineBridge.004.dll" \
  $(sources "$UGUI/Runtime/UGUI")

compile Unity.InputSystem -unsafe+ "${ENGINE_REFS[@]}" \
  -r:"$OUT/UnityEngine.UI.dll" -r:"$OUT/Unity.InternalAPIEngineBridge.004.dll" \
  $(sources "$INPUTSYSTEM/InputSystem" | grep -v '/Editor/')

compile com.clockworklabs.spacetimedbsdk -unsafe+ -nullable:enable "${ENGINE_REFS[@]}" \
  -r:"$BSATN" -analyzer:"$CODEGEN" \
  $(sources "$STDB_SDK/src")

compile Assembly-CSharp "${ENGINE_REFS[@]}" \
  -r:"$OUT/UnityEngine.UI.dll" \
  -r:"$OUT/Unity.InputSystem.dll" \
  -r:"$OUT/Unity.InternalAPIEngineBridge.004.dll" \
  -r:"$OUT/com.clockworklabs.spacetimedbsdk.dll" \
  -r:"$BSATN" -analyzer:"$CODEGEN" \
  $(sources "$PROJECT/Assets/Scripts" "$PROJECT/Assets/module_bindings")

echo
echo "all assemblies compiled into $OUT"
