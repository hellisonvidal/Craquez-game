; Instalador do Craquez para Windows (gerado no CI com o NSIS)
; uso: makensis -DSRC=<pasta do build StandaloneWindows64> -DOUT=<Craquez-Setup.exe> -DVER=<versão> craquez.nsi
Unicode true
!include "MUI2.nsh"

!ifndef VER
  !define VER "1.0"
!endif

Name "Craquez"
OutFile "${OUT}"
InstallDir "$PROGRAMFILES64\Craquez"
InstallDirRegKey HKCU "Software\Craquez" "InstallDir"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
BrandingText "Craquez ${VER} - feito com IA - gratuito"

!define MUI_ICON "craquez.ico"
!define MUI_UNICON "craquez.ico"
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\Craquez.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Jogar Craquez agora"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "PortugueseBR"

Section "Craquez" SecMain
  SetOutPath "$INSTDIR"
  File /r "${SRC}/*.*"
  File "craquez.ico"
  WriteRegStr HKCU "Software\Craquez" "InstallDir" "$INSTDIR"
  WriteUninstaller "$INSTDIR\Desinstalar.exe"
  CreateDirectory "$SMPROGRAMS\Craquez"
  CreateShortcut "$SMPROGRAMS\Craquez\Craquez.lnk" "$INSTDIR\Craquez.exe" "" "$INSTDIR\craquez.ico"
  CreateShortcut "$SMPROGRAMS\Craquez\Desinstalar Craquez.lnk" "$INSTDIR\Desinstalar.exe"
  CreateShortcut "$DESKTOP\Craquez.lnk" "$INSTDIR\Craquez.exe" "" "$INSTDIR\craquez.ico"
  ; entrada em "Aplicativos instalados"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Craquez" "DisplayName" "Craquez"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Craquez" "DisplayVersion" "${VER}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Craquez" "Publisher" "Craquez (feito com IA)"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Craquez" "DisplayIcon" "$INSTDIR\craquez.ico"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Craquez" "UninstallString" "$\"$INSTDIR\Desinstalar.exe$\""
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Craquez" "NoModify" 1
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Craquez" "NoRepair" 1
SectionEnd

Section "Uninstall"
  Delete "$DESKTOP\Craquez.lnk"
  RMDir /r "$SMPROGRAMS\Craquez"
  RMDir /r "$INSTDIR"
  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Craquez"
  DeleteRegKey HKCU "Software\Craquez"
SectionEnd
