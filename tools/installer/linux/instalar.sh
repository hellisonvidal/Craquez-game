#!/bin/sh
# Instala o Craquez para o usuário atual: atalho no menu de aplicativos e ícone.
# uso: ./instalar.sh   (rode dentro da pasta extraída do Craquez-Linux.tar.gz)
set -e
DIR="$(cd "$(dirname "$0")" && pwd)"
BIN="$DIR/Craquez"
[ -x "$BIN" ] || BIN="$DIR/Craquez.x86_64"
chmod +x "$BIN"
mkdir -p "$HOME/.local/share/applications" "$HOME/.local/share/icons/hicolor/256x256/apps"
cp "$DIR/craquez.png" "$HOME/.local/share/icons/hicolor/256x256/apps/craquez.png"
sed "s|^Exec=.*|Exec=\"$BIN\"|" "$DIR/craquez.desktop" > "$HOME/.local/share/applications/craquez.desktop"
chmod +x "$HOME/.local/share/applications/craquez.desktop"
echo "Craquez instalado: procure \"Craquez\" no menu de aplicativos."
