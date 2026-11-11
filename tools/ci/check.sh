#!/bin/bash
# Checagem sem licença Unity: compila todo o C# do jogo contra as DLLs do editor
# (UnityEngine + uGUI + Input System) e roda os testes da simulação com o .NET do editor.
# uso: tools/ci/check.sh [pasta Editor/Data]   (padrão: /opt/unity/Editor/Data, imagem unityci/editor)
set -euo pipefail
E=${1:-/opt/unity/Editor/Data}
ROOT=$(cd "$(dirname "$0")/../.." && pwd)
W=$(mktemp -d)
trap "rm -rf $W" EXIT
DOTNET=$E/NetCoreRuntime/dotnet
CSC="$DOTNET $E/DotNetSdkRoslyn/csc.dll -nologo -noconfig -nostdlib -target:library -langversion:9 -nowarn:1701,1702,0649,0414,0618 -define:UNITY_2023_1_OR_NEWER -define:UNITY_6000_0_OR_NEWER"
NS=$E/NetStandard/ref/2.1.0/netstandard.dll
REFS="-r:$NS"
for d in $E/Managed/UnityEngine/UnityEngine*.dll; do REFS="$REFS -r:$d"; done
PKG=$E/Resources/PackageManager
echo "== uGUI"
$CSC $REFS -define:PACKAGE_PHYSICS -define:PACKAGE_ANIMATION -out:$W/UnityEngine.UI.dll $(find $PKG/BuiltInPackages/com.unity.ugui/Runtime/UGUI -name "*.cs")
echo "== Input System"
mkdir -p $W/is && tar xzf $(ls $PKG/Editor/com.unity.inputsystem-*.tgz | head -1) -C $W/is
$CSC $REFS -r:$W/UnityEngine.UI.dll -unsafe -define:UNITY_INPUT_SYSTEM_ENABLE_PHYSICS -define:UNITY_INPUT_SYSTEM_ENABLE_UI -define:UNITY_INPUT_SYSTEM_PLATFORM_SCROLL_DELTA -out:$W/Unity.InputSystem.dll $(find $W/is/package/InputSystem -name "*.cs" | grep -v "/Editor/")
echo "== Craquez.Sim"
$CSC $REFS -out:$W/Sim.dll $(find $ROOT/Assets/Craquez/Scripts/Sim -name "*.cs")
echo "== Craquez.Runtime (sem UrpSetup, que depende do URP)"
cat > $W/UrpStub.cs <<'CS'
namespace Craquez { public static class UrpSetup {
 public static void Camera(UnityEngine.Camera c, int q){} public static void Quality(int q){}
 public static void Grade(bool n, float b, float e, float s, float c, float v){} } }
CS
$CSC $REFS -r:$W/Sim.dll -r:$W/UnityEngine.UI.dll -r:$W/Unity.InputSystem.dll -define:UNITY_ANDROID -out:$W/Runtime.dll $(find $ROOT/Assets/Craquez/Scripts/Runtime -name "*.cs" | grep -v UrpSetup) $W/UrpStub.cs
echo "== Craquez.Online (contra stubs dos pacotes da Unity Gaming Services)"
$CSC $REFS -r:$W/Sim.dll -r:$W/Runtime.dll -r:$W/UnityEngine.UI.dll -define:UNITY_ANDROID -out:$W/UgsStubs.dll $ROOT/tools/ci/UgsStubs.cs
$CSC $REFS -r:$W/Sim.dll -r:$W/Runtime.dll -r:$W/UnityEngine.UI.dll -r:$W/UgsStubs.dll -define:UNITY_ANDROID -out:$W/Online.dll $(find $ROOT/Assets/Craquez/Scripts/Online -name "*.cs")
echo "== Craquez.Editor"
EREFS=""
for d in $E/Managed/UnityEngine/UnityEditor*.dll; do EREFS="$EREFS -r:$d"; done
$CSC $REFS $EREFS -r:$W/Sim.dll -r:$W/Runtime.dll -define:UNITY_EDITOR -out:$W/Editor.dll $(find $ROOT/Assets/Craquez/Editor -name "*.cs")
echo "== Testes"
NU=$PKG/BuiltInPackages/com.unity.ext.nunit/net40/unity-custom/nunit.framework.dll
MS=$E/NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll
$CSC $REFS -r:$W/Sim.dll -r:$NU -r:$MS -out:$W/Tests.dll $(find $ROOT/Assets/Craquez/Tests -name "*.cs")
$DOTNET $E/DotNetSdkRoslyn/csc.dll -nologo -noconfig -nostdlib -target:exe -r:$NS -r:$W/Sim.dll -out:$W/run.dll $ROOT/tools/ci/RunTests.cs
cp $NU $E/Managed/UnityEngine/UnityEngine.CoreModule.dll $E/Managed/UnityEngine/UnityEngine.ImageConversionModule.dll $W/
FW=$(ls $E/NetCoreRuntime/shared/Microsoft.NETCore.App | head -1)
echo "{\"runtimeOptions\":{\"tfm\":\"net6.0\",\"framework\":{\"name\":\"Microsoft.NETCore.App\",\"version\":\"$FW\"}}}" > $W/run.runtimeconfig.json
[ -n "${KEEP:-}" ] && cp $W/Sim.dll $W/Runtime.dll "$KEEP"/
cd $W && $DOTNET run.dll $ROOT/Assets/Craquez/Resources/Data/leagues.json
