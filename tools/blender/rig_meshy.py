"""
Esqueleto no modelo original (proporções e pose em T dele, roupa e rosto como vieram) — para usar fora do
jogo ou reimportar. Usa os mesmos ossos do jogo (pelvis, spine_01..03, neck_01, head, clavicle_*,
upperarm_*, lowerarm_*, hand_* e dedos, thigh_*, calf_*, foot_*, ball_*), encaixados no corpo do modelo.

    python rig_meshy.py <modelo.fbx> <textura_cor.png> <saida_dir> [faces] [rig.blend]
"""
import os
import sys

import bpy  # noqa: F401  (precisa vir antes de bmesh)
import numpy as np
from mathutils import Matrix, Vector

SRC, TEX, OUT = sys.argv[1], sys.argv[2], sys.argv[3]
FACES = int(sys.argv[4]) if len(sys.argv) > 4 else 60000
RIG_BLEND = sys.argv[5] if len(sys.argv) > 5 else "/tmp/claude-0/athletes10/Athlete_europeu.blend"


def log(*a):
    print("[rig]", *a, flush=True)


bpy.ops.wm.open_mainfile(filepath=RIG_BLEND)
rig = bpy.data.objects["Armature"]
for o in list(bpy.data.objects):
    if o.type == "MESH":
        bpy.data.objects.remove(o, do_unlink=True)

before = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=SRC)
body = [o for o in bpy.data.objects if o not in before and o.type == "MESH"][0]
for o in [o for o in bpy.data.objects if o not in before and o != body]:
    bpy.data.objects.remove(o, do_unlink=True)
bpy.context.view_layer.objects.active = body
body.select_set(True)
body.parent = None
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
dec = body.modifiers.new("dec", "DECIMATE")
dec.ratio = min(1.0, FACES / max(1, len(body.data.polygons)))
bpy.ops.object.modifier_apply(modifier="dec")
me = body.data
co = np.zeros(len(me.vertices) * 3)
me.vertices.foreach_get("co", co)
co = co.reshape(-1, 3)
# pés no chão e centro em x (o mesmo ajuste que o jogo usa)
shift = Vector((-(co[:, 0].min() + co[:, 0].max()) / 2, 0, -co[:, 2].min()))
co += np.array(shift)
me.vertices.foreach_set("co", co.ravel())
me.update()
log("malha:", len(me.polygons), "faces")

# ---- mesmo mapa de proporções do jogo, ao contrário: leva o esqueleto do jogo para o corpo do modelo
CROTCH_M, CROTCH_G = 0.73, 0.885
SHOULDER_M, SHOULDER_G = 1.585, 1.59
NECK_G = SHOULDER_G + 0.03
HEAD_K = 0.88


def z_inv(z):
    if z <= CROTCH_G:
        return z * CROTCH_M / CROTCH_G
    if z <= SHOULDER_G:
        return CROTCH_M + (z - CROTCH_G) * (SHOULDER_M - CROTCH_M) / (SHOULDER_G - CROTCH_G)
    return z - (SHOULDER_G - SHOULDER_M)


xs = np.arange(0.45, 0.80, 0.05)
cz = []
for x in xs:
    m = (np.abs(np.abs(co[:, 0]) - x) < 0.01) & (co[:, 2] > 1.0)
    cz.append([co[m, 1].mean(), co[m, 2].mean()])
cz = np.array(cz)
slope_z = np.polyfit(xs, cz[:, 1], 1)
slope_y = np.polyfit(xs, cz[:, 0], 1)
SH = rig.matrix_world @ rig.data.bones["upperarm_l"].head_local
arm_dz = SH.z - np.polyval(slope_z, SH.x)

# esqueleto em T sobre os braços do modelo (como na conversão do jogo)
bpy.ops.object.select_all(action="DESELECT")
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
rig.data.pose_position = "POSE"
bpy.ops.object.mode_set(mode="POSE")
arm_dir = Vector((1.0, slope_y[0], slope_z[0])).normalized()
for side in ("l", "r"):
    d = Vector((arm_dir.x * (1 if side == "l" else -1), arm_dir.y, arm_dir.z))
    for bn in ("upperarm_", "lowerarm_", "hand_"):
        pb = rig.pose.bones[bn + side]
        bpy.context.view_layer.update()
        cur = (rig.matrix_world @ pb.matrix).to_3x3() @ Vector((0, 1, 0))
        q = cur.rotation_difference(d)
        m = rig.matrix_world @ pb.matrix
        loc = m.to_translation()
        pb.matrix = rig.matrix_world.inverted() @ (Matrix.Translation(loc) @ q.to_matrix().to_4x4() @ Matrix.Translation(-loc) @ m)
        bpy.context.view_layer.update()
bpy.ops.pose.armature_apply(selected=False)
bpy.ops.object.mode_set(mode="OBJECT")


def to_model(p, is_arm, head_c):
    p = Vector(p)
    if p.z > NECK_G and not is_arm:
        # cabeça: o jogo reduziu por igual; aqui volta ao tamanho do modelo
        p = Vector((p.x / HEAD_K, head_c.y + (p.y - head_c.y) / HEAD_K, NECK_G + (p.z - NECK_G) / HEAD_K))
    if is_arm:
        return Vector((p.x, p.y, p.z - arm_dz))
    return Vector((p.x, p.y, z_inv(p.z)))


bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode="EDIT")
eb = rig.data.edit_bones
head_c = eb["head"].head.copy()
ARM = ("upperarm", "lowerarm", "hand", "index", "middle", "ring", "pinky", "thumb")
new = {}
for b in eb:
    is_arm = b.name.startswith(ARM)
    new[b.name] = (to_model(b.head, is_arm, head_c), to_model(b.tail, is_arm, head_c))
for b in eb:
    h, t = new[b.name]
    b.head, b.tail = h, t
bpy.ops.object.mode_set(mode="OBJECT")

# pesos (calor dos ossos)
bpy.ops.object.select_all(action="DESELECT")
body.select_set(True)
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.parent_set(type="ARMATURE_AUTO")
log("pesos:", len([g for g in body.vertex_groups]), "grupos;", sum(1 for v in me.vertices if len(v.groups) == 0), "vértices sem peso")
body.name = "Body"
rig.name = "Armature"

# textura de cor ligada ao material (vai junto no FBX)
mat = me.materials[0] if me.materials else bpy.data.materials.new("Body")
if not me.materials:
    me.materials.append(mat)
mat.use_nodes = True
nt = mat.node_tree
bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
img = bpy.data.images.load(TEX)
tex = nt.nodes.new("ShaderNodeTexImage")
tex.image = img
if bsdf is not None:
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])

os.makedirs(OUT, exist_ok=True)
bpy.ops.object.select_all(action="DESELECT")
rig.select_set(True)
body.select_set(True)
bpy.context.view_layer.objects.active = rig
path = os.path.join(OUT, "Craquez_corpo_com_esqueleto.fbx")
bpy.ops.export_scene.fbx(filepath=path, use_selection=True, add_leaf_bones=False, bake_anim=False,
                         object_types={"ARMATURE", "MESH"}, mesh_smooth_type="FACE", use_mesh_modifiers=True,
                         axis_forward="-Z", axis_up="Y", apply_scale_options="FBX_SCALE_ALL",
                         path_mode="COPY", embed_textures=True, use_tspace=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "Craquez_corpo_com_esqueleto.blend"))
log("exportado", path, os.path.getsize(path) // 1024, "KB")
