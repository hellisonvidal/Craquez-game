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
ICON="$HOME/.local/share/icons/hicolor/256x256/apps/craquez.png"
# caminho completo do ícone (não depende do cache de temas, que mantinha o ícone antigo)
sed -e "s|^Exec=.*|Exec=\"$BIN\"|" -e "s|^Icon=.*|Icon=$ICON|" "$DIR/craquez.desktop" > "$HOME/.local/share/applications/craquez.desktop"
chmod +x "$HOME/.local/share/applications/craquez.desktop"
command -v gtk-update-icon-cache >/dev/null 2>&1 && gtk-update-icon-cache -q -t "$HOME/.local/share/icons/hicolor" 2>/dev/null || true
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database -q "$HOME/.local/share/applications" 2>/dev/null || true
echo "Craquez instalado: procure \"Craquez\" no menu de aplicativos."
