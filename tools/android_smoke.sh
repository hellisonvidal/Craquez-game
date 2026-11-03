#!/usr/bin/env bash
# Teste no emulador Android (CI): instala o APK, abre, aperta JOGAR, tira prints e guarda o logcat.
set -x
OUT=android_shots
mkdir -p $OUT
PKG=com.hellisonvidal.craquez
APK=$(ls apk/*.apk | head -1)
adb wait-for-device
# espera o Android terminar de iniciar (temas/serviços recriam apps abertos cedo demais)
until [ "$(adb shell getprop sys.boot_completed | tr -d '\r')" = "1" ]; do sleep 2; done
sleep 60
adb shell getprop ro.product.cpu.abilist
adb shell wm size
adb install -r -g "$APK" || { echo "FALHA NA INSTALAÇÃO"; exit 1; }
adb shell settings put secure immersive_mode_confirmations confirmed
adb logcat -c
adb shell monkey -p $PKG -c android.intent.category.LAUNCHER 1
sleep 30
adb exec-out screencap -p > $OUT/a1_abertura.png
adb shell pidof $PKG && echo "app rodando (menu)" || echo "APP FECHOU NO MENU"
# JOGAR já vem com foco: Enter dispara o botão (mesmo caminho do toque)
adb shell input keyevent KEYCODE_ENTER
sleep 25
adb exec-out screencap -p > $OUT/a2_partida.png
sleep 20
adb exec-out screencap -p > $OUT/a3_partida.png
# toque no meio da tela (controle virtual) para garantir que toque não derruba
adb shell input tap 300 800
sleep 5
adb exec-out screencap -p > $OUT/a4_toque.png
adb shell pidof $PKG && echo "app rodando (partida)" || echo "APP FECHOU NA PARTIDA"
adb logcat -d > $OUT/logcat.txt
grep -iE "godot|craquez|FATAL|AndroidRuntime|SCRIPT ERROR|libc" $OUT/logcat.txt | tail -400 > $OUT/logcat_godot.txt
tail -80 $OUT/logcat_godot.txt
exit 0
