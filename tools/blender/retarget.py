"""Transfere a captura CMU para o esqueleto game_engine dos atletas e exporta
as animações (FBX, só o esqueleto) para o Unity.

Uso: python tools/blender/retarget.py <athlete.blend> <mocap_dir> <saida_dir> [--preview]

Cada clipe sai "no lugar" (sem deslocamento horizontal e sem girar o corpo
como um todo): a simulação do jogo move e orienta o atleta; a animação só
dá o movimento do corpo. A inclinação nas curvas, o balanço do quadril etc.
são mantidos. Um JSON acompanha com velocidade média e se o clipe é cíclico.
"""
import json
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(__file__))
import cmu_mocap as cmu  # noqa: E402

BLEND, MOCAP, OUT = sys.argv[1:4] if "--" not in sys.argv else sys.argv[sys.argv.index("--") + 1:][:3]
PREVIEW = "--preview" in sys.argv
ONLY = [a.split("=", 1)[1] for a in sys.argv if a.startswith("--only=")]
FPS = 30

# osso alvo -> segmento CMU
MAP = {
    "pelvis": "root", "spine_01": "lowerback", "spine_02": "upperback", "spine_03": "thorax",
    "neck_01": "lowerneck", "head": "head",
    "clavicle_l": "lclavicle", "upperarm_l": "lhumerus", "lowerarm_l": "lradius", "hand_l": "lhand",
    "clavicle_r": "rclavicle", "upperarm_r": "rhumerus", "lowerarm_r": "rradius", "hand_r": "rhand",
    "thigh_l": "lfemur", "calf_l": "ltibia", "foot_l": "lfoot", "ball_l": "ltoes",
    "thigh_r": "rfemur", "calf_r": "rtibia", "foot_r": "rfoot", "ball_r": "rtoes",
}

# nome, arquivo, início (s), fim (s), cíclico, espelhar
# (inícios/fins ajustados olhando as pré-visualizações; ciclos são refinados
#  automaticamente para o melhor ponto de emenda)
CLIPS = [
    ("Idle", "141_20", 2.0, 6.0, True, False),
    ("Jog", "35_17", 0.3, 1.6, True, False),
    ("Run", "102_05", 0.0, 0.9, True, False),
    ("Sprint", "09_01", 0.0, 1.25, True, False),
    ("RunTurnL", "102_02", 0.5, 2.5, False, False),
    ("RunTurnR", "102_01", 0.5, 2.5, False, False),
    ("CutL", "102_03", 0.3, 2.0, False, False),
    ("CutR", "102_04", 0.3, 2.0, False, False),
    ("SideStepL", "127_13", 0.3, 2.0, False, False),
    ("SideStepR", "127_14", 0.3, 2.0, False, False),
    ("Backpedal", "141_31", 1.0, 3.0, True, False),
    ("Jockey", "102_27", 0.5, 3.5, False, False),
    ("FeintL", "102_19", 0.3, 2.3, False, False),
    ("FeintR", "102_18", 0.3, 2.3, False, False),
    ("Spin", "102_11", 0.3, 2.3, False, False),
    ("QuickStop", "127_05", 0.5, 3.0, False, False),
    ("Kick", "10_02", 0.0, 99.0, False, False),
    ("KickPower", "10_01", 0.0, 99.0, False, False),
    ("Pass", "10_03", 0.0, 99.0, False, False),
    ("Shot", "10_05", 0.0, 99.0, False, False),
    ("ShotLeft", "10_06", 0.0, 99.0, False, False),
    ("Volley", "11_01", 0.0, 99.0, False, False),
    ("Walk", "35_01", 0.5, 2.5, True, False),
]


def log(*a):
    print("[retarget]", *a, flush=True)


def to_np(m):
    return np.array([[m[i][j] for j in range(3)] for i in range(3)])


def to_mat3(a):
    return Matrix([list(r) for r in a])


def rot_between(a, b):
    a = a / np.linalg.norm(a)
    b = b / np.linalg.norm(b)
    v = np.cross(a, b)
    c = float(np.dot(a, b))
    if c < -0.999999:
        ax = np.cross(a, [1, 0, 0])
        if np.linalg.norm(ax) < 1e-6:
            ax = np.cross(a, [0, 1, 0])
        ax /= np.linalg.norm(ax)
        return 2 * np.outer(ax, ax) - np.eye(3)
    vx = np.array([[0, -v[2], v[1]], [v[2], 0, -v[0]], [-v[1], v[0], 0]])
    return np.eye(3) + vx + vx @ vx * (1.0 / (1.0 + c))


def rz(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])


def smooth(x, sigma):
    if sigma <= 0:
        return x.copy()
    r = int(sigma * 3)
    k = np.exp(-0.5 * (np.arange(-r, r + 1) / sigma) ** 2)
    k /= k.sum()
    pad = np.pad(x, ((r, r),) + ((0, 0),) * (x.ndim - 1), mode="edge")
    out = np.zeros_like(x)
    for i in range(len(x)):
        out[i] = np.tensordot(k, pad[i:i + 2 * r + 1], axes=(0, 0))
    return out


class Target:
    def __init__(self, rig):
        self.rig = rig
        self.bones = [b.name for b in rig.data.bones]
        self.rest = {b.name: b.matrix_local.copy() for b in rig.data.bones}
        self.rest_rot = {n: to_np(m.to_3x3()) for n, m in self.rest.items()}
        self.parent = {b.name: (b.parent.name if b.parent else None) for b in rig.data.bones}
        self.order = []
        todo = [b.name for b in rig.data.bones if b.parent is None]
        while todo:
            n = todo.pop(0)
            self.order.append(n)
            todo[0:0] = [c.name for c in rig.data.bones[n].children]
        b = rig.data.bones
        self.dir = {n: np.array(b[n].tail_local - b[n].head_local) for n in self.bones}
        self.pelvis_h = b["pelvis"].head_local.z


def retarget_clip(tgt, skel, frames_src, src_fps=120):
    # quadros a 30 fps
    step = src_fps / FPS
    idx = [int(round(i * step)) for i in range(int(len(frames_src) / step))]
    fkd = [cmu.fk(skel, frames_src[i]) for i in idx]
    # altura do quadril da fonte (pose de repouso) para escalar a translação
    rest_frame = {"root": [0, 0, 0, 0, 0, 0]}
    _, pos0, tail0 = cmu.fk(skel, rest_frame)
    src_hip = -min(tail0[n][2] for n in tail0)
    scale = tgt.pelvis_h / max(src_hip, 1e-6)
    # rumo (yaw) do quadril em cada quadro, suavizado
    fw = np.array([G["root"] @ np.array([0, -1.0, 0]) for G, _, _ in fkd])
    yaw = np.unwrap(np.arctan2(fw[:, 0], -fw[:, 1]))
    yaw_s = smooth(yaw, FPS * 0.25)
    root = np.array([p["root"] for _, p, _ in fkd])
    out = []
    # alinhamentos de repouso
    align = {}
    for tb, sb in MAP.items():
        if sb == "root":
            align[tb] = np.eye(3)
        else:
            align[tb] = rot_between(tgt.dir[tb], skel.rest_dir(sb))
    unrot = [rz(-y) for y in yaw_s]   # gira o mundo para tirar o rumo
    root_l = np.array([unrot[i] @ root[i] for i in range(len(root))])
    traj = smooth(root_l, FPS * 0.4)
    speeds = np.linalg.norm(np.diff(root[:, :2], axis=0), axis=1) * FPS * scale
    for i, (G, P_, T_) in enumerate(fkd):
        U = unrot[i]
        world = {}
        for tb, sb in MAP.items():
            world[tb] = U @ G[sb] @ align[tb] @ tgt.rest_rot[tb]
        hip = root_l[i] - np.array([traj[i][0], traj[i][1], 0])
        hip = np.array([hip[0], hip[1], root_l[i][2]]) * scale
        out.append((world, hip))
    return out, float(np.mean(speeds)) if len(speeds) else 0.0


def build_action(tgt, name, poses):
    rig = tgt.rig
    act = bpy.data.actions.new(name)
    rig.animation_data_create()
    rig.animation_data.action = act
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
    for f, (world, hip) in enumerate(poses):
        pose_rot = {}
        for n in tgt.order:
            p = tgt.parent[n]
            rest = tgt.rest_rot[n]
            if p is None:
                pose_rot[n] = rest
                basis = np.eye(3)
            else:
                rel = tgt.rest_rot[p].T @ rest       # repouso relativo ao pai
                if n in world:
                    pose_rot[n] = world[n]
                    basis = rest.T @ tgt.rest_rot[p] @ pose_rot[p].T @ world[n]
                else:
                    pose_rot[n] = pose_rot[p] @ rel
                    basis = np.eye(3)
            pb = rig.pose.bones[n]
            q = to_mat3(basis).to_quaternion()
            pb.rotation_quaternion = q
            if n in world:
                pb.keyframe_insert("rotation_quaternion", frame=f + 1, group=n)
            if n == "pelvis":
                rest_head = np.array(tgt.rest[n].translation)
                loc = tgt.rest_rot[n].T @ (hip - rest_head)
                pb.location = Vector(loc)
                pb.keyframe_insert("location", frame=f + 1, group=n)
    # quaternions contínuos (evita giro de 360° na interpolação)
    for fc in act.fcurves if hasattr(act, "fcurves") else []:
        pass
    act.use_fake_user = True
    return act


def kick_window(skel, src, before=1.1, after=0.9):
    """Acha o instante do chute (pico de velocidade do pé em relação ao quadril)."""
    best, bt = 0.0, 0.0
    prev = None
    for i in range(0, len(src), 4):
        _, pos, tail = cmu.fk(skel, src[i])
        p = np.concatenate([tail["rtoes"] - pos["root"], tail["ltoes"] - pos["root"]])
        if prev is not None:
            v = max(np.linalg.norm(p[:3] - prev[:3]), np.linalg.norm(p[3:] - prev[3:])) * 30
            if v > best:
                best, bt = v, i / 120.0
        prev = p
    total = len(src) / 120.0
    return max(0.0, bt - before), min(total, bt + after)


def best_loop(poses, lo, hi):
    """Escolhe o trecho [a, b) cuja emenda (pose b ~ pose a) é a mais suave."""
    def feat(p):
        w, h = p
        return np.concatenate([w[k].ravel() for k in ("thigh_l", "thigh_r", "calf_l", "calf_r", "upperarm_l",
                                                       "upperarm_r", "pelvis")] + [h[2:3] * 4])
    F = np.array([feat(p) for p in poses])
    n = len(F)
    best = (1e9, 0, n)
    for a in range(0, max(1, n // 3)):
        for b in range(a + lo, min(n, a + hi)):
            d = np.linalg.norm(F[a] - F[b])
            if d < best[0]:
                best = (d, a, b)
    return best


def main():
    bpy.ops.wm.open_mainfile(filepath=BLEND)
    rig = bpy.data.objects["Armature"]
    tgt = Target(rig)
    meta = {}
    os.makedirs(OUT, exist_ok=True)
    skels = {}
    for name, f, t0, t1, loop, mirror in CLIPS:
        if ONLY and name not in ONLY:
            continue
        subj = f.split("_")[0]
        if subj not in skels:
            skels[subj] = cmu.Skeleton(os.path.join(MOCAP, f"{subj}.asf"))
        src = cmu.read_amc(os.path.join(MOCAP, f + ".amc"))
        if t1 >= 99:
            t0, t1 = kick_window(skels[subj], src)
            log(name, "janela do chute", round(t0, 2), round(t1, 2))
        a, b = int(t0 * 120), min(len(src), int(t1 * 120))
        poses, speed = retarget_clip(tgt, skels[subj], src[a:b])
        if loop:
            d, la, lb = best_loop(poses, int(FPS * 0.55), int(FPS * 1.4))
            poses = poses[la:lb + 1]
            log(name, "ciclo", la, lb, "erro", round(d, 3))
        act = build_action(tgt, name, poses)
        meta[name] = {"frames": len(poses), "fps": FPS, "loop": loop, "speed": round(speed, 3), "source": f}
        log(name, len(poses), "quadros", "vel", round(speed, 2), "m/s")
        if PREVIEW:
            import preview_render
            rig.animation_data.action = act
            sc = bpy.context.scene
            sc.frame_start, sc.frame_end = 1, len(poses)
            n = len(poses)
            frames = sorted({1, max(1, n // 4), max(1, n // 2), max(1, 3 * n // 4), n})
            preview_render.kit_colors()
            preview_render.show_hair(rig, "Hair_short02")
            preview_render.render_frames(rig, os.path.join(OUT, f"anim_{name}_%03d.png"), frames)
            for o in list(bpy.data.objects):
                if o.type in ("CAMERA", "LIGHT"):
                    bpy.data.objects.remove(o)
    json.dump(meta, open(os.path.join(OUT, "anims.json"), "w"), indent=1)
    # exporta só o esqueleto com todas as ações
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    rig.animation_data.action = None
    for pb in rig.pose.bones:
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.location = (0, 0, 0)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "Athlete_Anims.fbx"), use_selection=True,
                             object_types={"ARMATURE"}, add_leaf_bones=False, bake_anim=True,
                             bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
                             bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
                             bake_anim_step=1.0, axis_forward="-Z", axis_up="Y",
                             apply_scale_options="FBX_SCALE_ALL")
    log("FBX de animações exportado")


main()
