# Atletas e animações (Blender headless)

```bash
uv venv bpyenv --python 3.13 && VIRTUAL_ENV=bpyenv uv pip install bpy==5.2.3
git clone --depth 1 https://github.com/makehumancommunity/mpfb2
mkdir -p ~/.config/blender/5.2/extensions/user_default && cp -r mpfb2/src/mpfb ~/.config/blender/5.2/extensions/user_default/mpfb
# assets CC0: https://files2.makehumancommunity.org/asset_packs/makehuman_system_assets/makehuman_system_assets_cc0.zip
# descompacte em ~/.config/blender/5.2/extensions/.user/user_default/mpfb/data/
bpyenv/bin/python tools/blender/build_athletes.py out --preview          # 4 biotipos -> out/Athlete_*.fbx
# captura CMU (ASF/AMC) de mocap.cs.cmu.edu: sujeitos 9, 10, 11, 35, 102, 127, 141
# Mixamo (FBX sem pele, 30 fps, T-pose): "Soccer Game Pack" em mx/soccer e "Locomotion Pack" em mx/loco
# (os arquivos do Mixamo não vão para o repositório: só o resultado convertido)
bpyenv/bin/python tools/blender/retarget.py out/Athlete_afro.blend mocap out --mixamo=mx --sheet   # -> Athlete_Anims.fbx + anims.json
```

Copie os FBX, `anims.json` e as texturas para `Assets/Craquez/Resources/Athletes/`.

## Cenário (Quaternius, CC0)

```bash
# "Stylized Nature MegaKit" + casas/sobrados (variante _Mat, cores lisas) + as texturas da natureza
bpyenv/bin/python tools/blender/props.py qt/kit qt/tex out --sheet   # -> out/props.bytes + out/Tex/*.png
```

Copie `props.bytes` e `Tex/` para `Assets/Craquez/Resources/Props/` (lidos por `PropLib.cs`). Os
arquivos originais do pacote não vão para o repositório: só o resultado convertido.
