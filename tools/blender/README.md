# Atletas e animações (Blender headless)

```bash
uv venv bpyenv --python 3.13 && VIRTUAL_ENV=bpyenv uv pip install bpy==5.2.3
git clone --depth 1 https://github.com/makehumancommunity/mpfb2
mkdir -p ~/.config/blender/5.2/extensions/user_default && cp -r mpfb2/src/mpfb ~/.config/blender/5.2/extensions/user_default/mpfb
# assets CC0: https://files2.makehumancommunity.org/asset_packs/makehuman_system_assets/makehuman_system_assets_cc0.zip
# descompacte em ~/.config/blender/5.2/extensions/.user/user_default/mpfb/data/
bpyenv/bin/python tools/blender/build_athletes.py out --mh-kit --preview   # 4 biotipos -> out/Athlete_*.fbx
# (--mh-kit: camisa e calção feitos da camiseta e da calça "male_casualsuit04" do MakeHuman, CC0)
# captura CMU (ASF/AMC) de mocap.cs.cmu.edu: sujeitos 9, 10, 11, 35, 102, 127, 141
# Mixamo (FBX sem pele, 30 fps, T-pose): "Soccer Game Pack" em mx/soccer e "Locomotion Pack" em mx/loco
# (os arquivos do Mixamo não vão para o repositório: só o resultado convertido)
bpyenv/bin/python tools/blender/retarget.py out/Athlete_afro.blend mocap out --mixamo=mx --sheet   # -> Athlete_Anims.fbx + anims.json
```

Copie os FBX, `anims.json` e as texturas para `Assets/Craquez/Resources/Athletes/`.

## Corpo único (modelo Meshy)

```bash
# rig.blend = só o "Armature" do Athlete_europeu.fbx. Modelo em pose A (braços retos a ~45-60°), FBX + textura de cor
# do Meshy; proporções já iguais às do esqueleto (sem remapear). O braço é achado sozinho (ombro -> punho -> mão)
MESHY_APOSE=1 MESHY_NAME=base MESHY_CROTCH=0.885 MESHY_SHOULDER=1.59 MESHY_HEAD=1 \
  bpyenv/bin/python tools/blender/build_meshy.py jogador.fbx jogador.png out rig.blend    # -> out/Athlete_base.fbx
MESHY_APOSE=1 MESHY_NAME=base_gk MESHY_CROTCH=0.885 MESHY_SHOULDER=1.59 MESHY_HEAD=1 \
  bpyenv/bin/python tools/blender/build_meshy.py goleiro.fbx goleiro.png out rig.blend    # -> out/Athlete_base_gk.fbx
# (modelo em pose T: sem MESHY_APOSE; com braço dobrado, MESHY_ELBOW_X / MESHY_WRIST_X em metros do centro)
```

Copie `Athlete_base*.fbx` para `Assets/Craquez/Resources/Athletes/` e `athlete_base*.jpg` para `.../Athletes/Textures/`
(o jogo usa `base_gk` nos goleiros, se existir).

## Cenário (Quaternius, CC0)

```bash
# "Stylized Nature MegaKit" + casas/sobrados (variante _Mat, cores lisas) + as texturas da natureza
bpyenv/bin/python tools/blender/props.py qt/kit qt/tex out --sheet   # -> out/props.bytes + out/Tex/*.png
```

Copie `props.bytes` e `Tex/` para `Assets/Craquez/Resources/Props/` (lidos por `PropLib.cs`). Os
arquivos originais do pacote não vão para o repositório: só o resultado convertido.
