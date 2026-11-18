"""Clipes complementares (Athlete_Anims2.fbx + anims2.json): dribles e fintas da captura CMU que não
estão no Athlete_Anims.fbx, transferidos para o mesmo esqueleto com retarget.py.

O esqueleto vem do próprio Athlete_Anims.fbx do jogo (importado de volta no Blender), então não
precisa refazer os atletas nem ter os arquivos do Mixamo. O processo foi conferido refazendo o
clipe "Spin" (102_11) e comparando com o original: diferença de 0,0°.

Também traz clipes da Universal Animation Library (Quaternius, CC0, versão gratuita "Standard"):
um sprint de verdade (o Sprint do Athlete_Anims.fbx era o mesmo clipe da corrida) e reações curtas
a choques. Com o mesmo nome de um clipe do Athlete_Anims.fbx, o do Anims2 o substitui no jogo.

uso:
  python tools/blender/extra_clips.py Assets/Craquez/Resources/Athletes/Athlete_Anims.fbx <pasta CMU> <saída> \
      [--ual=<UAL1_Standard_RM.fbx>] [--sheet]
  (pasta CMU: 102.asf e 102_12/15/20/21/26/29.amc de http://mocap.cs.cmu.edu/subjects/102/;
   UAL: https://quaternius.itch.io/universal-animation-library, arquivo Unity/UAL1_Standard_RM.fbx —
   a versão com deslocamento, para medir a velocidade da passada; o pacote não vai para o repositório)
"""
import os
import sys

_argv = list(sys.argv)
UAL = next((a.split("=", 1)[1] for a in _argv if a.startswith("--ual=")), None)
_pos = [a for a in _argv[1:] if not a.startswith("--")]
SRC, MOCAP, OUT = _pos[:3]
sys.argv = ["retarget.py", "-", MOCAP, OUT] + (["--sheet"] if "--sheet" in _argv else [])
sys.path.insert(0, os.path.dirname(__file__))
import json  # noqa: E402

import bpy  # noqa: E402

import cmu_mocap as cmu  # noqa: E402
import mixamo  # noqa: E402
import retarget as R  # noqa: E402

# nome, arquivo, início (s), fim (s), cíclico, espelhar
CLIPS = [
    ("SpinR", "102_15", 0.0, 99, False, False),    # giro para a direita (roleta)
    ("Spin2", "102_12", 0.0, 99, False, False),    # outro giro para a esquerda, mais baixo
    ("BreakR", "102_20", 2.75, 4.9, False, False),  # abaixa e arranca para a direita (sem a finta de basquete)
    ("BreakL", "102_21", 2.25, 4.3, False, False),
    ("Pivot", "102_29", 0.4, 1.85, False, False),   # giro sobre o pé de apoio (puxada)
    ("ZigZag", "102_26", 0.3, 3.0, False, False),   # balanço baixo de um lado para o outro (pedalada)
]


# Universal Animation Library: nome no jogo, ação no FBX, cíclico
UAL_CLIPS = [
    ("Sprint", "Sprint_Loop", True),   # tronco à frente e passada longa (substitui o Sprint = Run)
    ("HitChest", "Hit_Chest", False),  # tranco curto no peito (choque médio)
    ("HitHead", "Hit_Head", False),    # cabeça vai para trás (choque de ombro alto / bola no rosto)
]


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=SRC, axis_forward="-Z", axis_up="Y", automatic_bone_orientation=False, use_anim=False)
    rig = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    rig.name = "Armature"
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)
    tgt = R.Target(rig)
    os.makedirs(OUT, exist_ok=True)
    skels, meta = {}, {}
    for name, f, t0, t1, loop, _mirror in CLIPS:
        subj = f.split("_")[0]
        if subj not in skels:
            skels[subj] = cmu.Skeleton(os.path.join(MOCAP, f"{subj}.asf"))
        src = cmu.read_amc(os.path.join(MOCAP, f + ".amc"))
        a, b = int(t0 * 120), min(len(src), int(t1 * 120))
        poses, speed = R.retarget_clip(tgt, skels[subj], src[a:b])
        act = R.build_action(tgt, name, poses)
        meta[name] = {"frames": len(poses), "fps": R.FPS, "loop": loop, "speed": round(speed, 3), "source": f}
        R.log(name, len(poses), "quadros", round(speed, 2), "m/s")
        R.finish(rig, act, name, len(poses))
    for name, action, loop in (UAL_CLIPS if UAL else []):
        clip = mixamo.load(UAL, action=action)
        poses, speed = R.retarget_mixamo(tgt, clip, loop=loop, keep_yaw=not loop, inplace=not loop)
        act = R.build_action(tgt, name, poses)
        meta[name] = {"frames": len(poses), "fps": R.FPS, "loop": loop, "speed": round(speed, 3), "source": "ual:" + action}
        R.log(name, len(poses), "quadros", round(speed, 2), "m/s (UAL)")
        R.finish(rig, act, name, len(poses))
    json.dump(meta, open(os.path.join(OUT, "anims2.json"), "w"), indent=1)
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    rig.animation_data.action = None
    for pb in rig.pose.bones:
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.location = (0, 0, 0)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "Athlete_Anims2.fbx"), use_selection=True,
                             object_types={"ARMATURE"}, add_leaf_bones=False, bake_anim=True,
                             bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
                             bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
                             bake_anim_step=1.0, axis_forward="-Z", axis_up="Y",
                             apply_scale_options="FBX_SCALE_ALL")
    R.log("Athlete_Anims2.fbx exportado")


if __name__ == "__main__":
    main()
