"""Leitura das animações do Mixamo (FBX) para a transferência de movimento (retarget.py).

Importa o FBX na cena aberta, amostra cada quadro (rotação no mundo de cada osso e posição do
quadril) e apaga o que importou. As rotações saem como "giro desde a pose de repouso" no mundo,
o mesmo formato da captura CMU, e com o nome do osso dos nossos atletas.
"""
import bpy
import numpy as np

# osso dos atletas -> osso do Mixamo (sem o prefixo "mixamorig:")
MAP = {
    "pelvis": "Hips", "spine_01": "Spine", "spine_02": "Spine1", "spine_03": "Spine2",
    "neck_01": "Neck", "head": "Head",
    "clavicle_l": "LeftShoulder", "upperarm_l": "LeftArm", "lowerarm_l": "LeftForeArm", "hand_l": "LeftHand",
    "clavicle_r": "RightShoulder", "upperarm_r": "RightArm", "lowerarm_r": "RightForeArm", "hand_r": "RightHand",
    "thigh_l": "LeftUpLeg", "calf_l": "LeftLeg", "foot_l": "LeftFoot", "ball_l": "LeftToeBase",
    "thigh_r": "RightUpLeg", "calf_r": "RightLeg", "foot_r": "RightFoot", "ball_r": "RightToeBase",
}

# Universal Animation Library (Quaternius): esqueleto do manequim da Unreal, os mesmos nomes dos nossos
UAL_MAP = {tb: ("Head" if tb == "head" else tb) for tb in MAP}

# ossos que guardam a postura de repouso do atleta (recebem só o giro do Mixamo desde o repouso)
KEEP_REST = {"pelvis", "spine_01", "spine_02", "spine_03", "neck_01", "head", "clavicle_l", "clavicle_r",
             "foot_l", "foot_r", "ball_l", "ball_r"}

MIRROR = np.diag([-1.0, 1.0, 1.0])   # espelho esquerda/direita (eixo X do Blender)


def _rot(m):
    """3x3 ortonormal (tira a escala 0,01 do FBX do Mixamo)."""
    q = m.to_quaternion()
    r = q.to_matrix()
    return np.array([[r[i][j] for j in range(3)] for i in range(3)])


def _swap(n):
    return n.replace("_l", "_X").replace("_r", "_l").replace("_X", "_r")


class Clip:
    """Quadros do clipe: G[i][osso] = giro no mundo desde o repouso, hips[i] = posição do quadril (m)."""

    def __init__(self, G, hips, rest_dir, hip_h, fps):
        self.G, self.hips, self.rest_dir, self.hip_h, self.fps = G, hips, rest_dir, hip_h, fps

    def mirrored(self):
        G = [{_swap(k): MIRROR @ v @ MIRROR for k, v in g.items()} for g in self.G]
        hips = self.hips * np.array([-1.0, 1.0, 1.0])
        rd = {_swap(k): MIRROR @ v for k, v in self.rest_dir.items()}
        return Clip(G, hips, rd, self.hip_h, self.fps)


def load(path, action=None):
    """Lê um FBX do Mixamo ou, com action, uma ação da Universal Animation Library (vários clipes num
    arquivo só). O resultado sai sempre com o boneco de frente para -Y, como o Mixamo."""
    before = set(bpy.data.objects)
    acts_before = set(bpy.data.actions)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o.type == "ARMATURE" and o not in before] + [o for o in bpy.data.objects if o.type != "ARMATURE" and o not in before]
    arm = new[0]
    names = {b.name for b in arm.data.bones}
    ual = "pelvis" in names
    mp = UAL_MAP if ual else MAP
    pre = "" if ual else (arm.data.bones[0].name.split(":")[0] + ":" if ":" in arm.data.bones[0].name else "")
    if action:
        act = next(a for a in bpy.data.actions if a not in acts_before and a.name.split("|")[-1] == action)
        arm.animation_data.action = act
    act = arm.animation_data.action
    f0, f1 = int(round(act.frame_range[0])), int(round(act.frame_range[1]))
    sc = bpy.context.scene
    fps = sc.render.fps / sc.render.fps_base
    MW = arm.matrix_world
    hipn = pre + ("pelvis" if ual else "Hips")
    # de frente para -Y (o manequim da Unreal olha para +Y): gira tudo 180° em torno de z se precisar
    fb, bb = arm.data.bones[pre + mp["foot_l"]], arm.data.bones[pre + mp["ball_l"]]
    fwd = (MW @ bb.head_local) - (MW @ fb.head_local)
    F = np.diag([-1.0, -1.0, 1.0]) if fwd.y > 0 else np.eye(3)
    rest, rest_dir = {}, {}
    for tb, sb in mp.items():
        b = arm.data.bones[pre + sb]
        rest[tb] = _rot(MW @ b.matrix_local)
        rest_dir[tb] = F @ np.array(MW @ b.tail_local - MW @ b.head_local)
    hb = arm.data.bones[hipn]
    hip_h = float((MW @ hb.head_local).z)
    G, hips = [], []
    for f in range(f0, f1 + 1):
        sc.frame_set(f)
        g = {}
        for tb, sb in mp.items():
            pb = arm.pose.bones[pre + sb]
            g[tb] = F @ (_rot(MW @ pb.matrix) @ rest[tb].T) @ F.T
        G.append(g)
        hips.append(F @ np.array(MW @ arm.pose.bones[hipn].head))
    for o in new:
        bpy.data.objects.remove(o, do_unlink=True)
    for a in list(bpy.data.actions):
        if a not in acts_before:
            bpy.data.actions.remove(a)
    return Clip(G, np.array(hips), rest_dir, hip_h, fps)
