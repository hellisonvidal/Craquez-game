"""
Corpo único dos atletas a partir de um modelo 3D de jogador (Meshy, pose em T, malha única com a roupa
pintada) — o arquivo original NÃO fica no repositório; é passado na linha de comando.

    python build_meshy.py <modelo.fbx> <textura_cor.png> <saida_dir> [rig.blend]

Passos:
  1. reduz a malha (~26 mil triângulos) e separa as peças pela cor da textura e pela altura:
     Body (pele, rosto e cabelo), Shirt, Shorts, Socks, Boots — o jogo pinta o uniforme de cada time;
  2. acerta as proporções para o esqueleto das animações (o modelo é estilizado: pernas curtas e tronco
     longo) — pernas alongadas, tronco encaixado, cabeça e braços inteiros (sem achatar o rosto);
  3. prende ao esqueleto do jogo: o esqueleto é posto em T por cima dos braços do modelo, os pesos são
     calculados (calor dos ossos) e o corpo volta à pose de repouso do esqueleto (braços em A);
  4. formas de corpo para variar os atletas (Body_Lean, Body_Strong) e exporta Athlete_base.fbx.
"""
import math
import os
import sys

import bpy  # noqa: F401  (precisa vir antes de bmesh)
import bmesh
import numpy as np
from mathutils import Matrix, Vector

SRC, TEX, OUT = sys.argv[1], sys.argv[2], sys.argv[3]
RIG_BLEND = sys.argv[4] if len(sys.argv) > 4 else "/tmp/claude-0/athletes10/Athlete_europeu.blend"
TARGET_FACES = 26000


def log(*a):
    print("[meshy]", *a, flush=True)


# ------------------------------------------------------------------ esqueleto do jogo (pose A)
bpy.ops.wm.open_mainfile(filepath=RIG_BLEND)
rig = bpy.data.objects["Armature"]
for o in list(bpy.data.objects):
    if o.type == "MESH":
        bpy.data.objects.remove(o, do_unlink=True)
rig.data.pose_position = "REST"
rest_mat = {b.name: (rig.matrix_world @ b.matrix_local).copy() for b in rig.data.bones}
SH = rig.matrix_world @ rig.data.bones["upperarm_l"].head_local   # ombro (articulação)

# ------------------------------------------------------------------ modelo
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
dec.ratio = min(1.0, TARGET_FACES / max(1, len(body.data.polygons)))
bpy.ops.object.modifier_apply(modifier="dec")
me = body.data
log("faces", len(me.polygons), "vértices", len(me.vertices))

co = np.zeros(len(me.vertices) * 3, np.float64)
me.vertices.foreach_get("co", co)
co = co.reshape(-1, 3)
co[:, 2] -= co[:, 2].min()
co[:, 0] -= (co[:, 0].min() + co[:, 0].max()) / 2

# ------------------------------------------------------------------ 1) peças pela textura e altura
from PIL import Image  # noqa: E402

img = np.asarray(Image.open(TEX).convert("RGB")).astype(np.float32) / 255.0
H, W = img.shape[:2]
uvl = me.uv_layers.active.data
nl = len(me.loops)
luv = np.zeros(nl * 2, np.float32)
uvl.foreach_get("uv", luv)
luv = luv.reshape(-1, 2)
lvi = np.zeros(nl, np.int32)
me.loops.foreach_get("vertex_index", lvi)
cls = np.zeros(len(me.polygons), np.int32)   # 0 corpo, 1 camisa, 2 calção, 3 meião, 4 chuteira


def skin_like(c):
    r, g, b = c
    mx, mn = max(c), min(c)
    s = (mx - mn) / mx if mx > 0 else 0
    return r > g >= b * 0.9 and mx > 0.33 and 0.12 < s < 0.7 and r - b > 0.08


for p in me.polygons:
    ls = range(p.loop_start, p.loop_start + p.loop_total)
    u = np.mean([luv[i] for i in ls], axis=0)
    c = img[min(H - 1, max(0, int((1 - u[1]) * H))), min(W - 1, max(0, int(u[0] * W)))]
    z = np.mean([co[lvi[i], 2] for i in ls])
    ax = abs(np.mean([co[lvi[i], 0] for i in ls]))
    sk = skin_like(c)
    # pele só onde pode haver pele: braços, pescoço/cabeça e joelhos (no tronco e no calção, cor parecida com
    # pele é listra ou dobra do tecido)
    if 0.93 < z <= 1.52 and ax < 0.24:
        sk = False
    if 0.66 < z <= 0.93:
        sk = False
    if 0.93 < z <= 1.62 and not sk:
        cls[p.index] = 1          # gola: o tecido do pescoço é da camisa (a gola original não aparece)
    elif sk or z > 1.62:
        cls[p.index] = 0
    elif z > 0.6:
        cls[p.index] = 2
    elif z > 0.105:
        cls[p.index] = 3
    else:
        cls[p.index] = 4
# suaviza (ilhas soltas viram a peça da vizinhança)
bm = bmesh.new()
bm.from_mesh(me)
bm.faces.ensure_lookup_table()
for _ in range(3):
    new = cls.copy()
    for f in bm.faces:
        votes = {}
        for e in f.edges:
            for g in e.link_faces:
                if g is not f:
                    votes[cls[g.index]] = votes.get(cls[g.index], 0) + 1
        if votes:
            best = max(votes, key=votes.get)
            if votes[best] >= 2 and best != cls[f.index]:
                new[f.index] = best
    cls = new
bm.free()
log("peças (faces):", {n: int((cls == i).sum()) for i, n in enumerate(["Body", "Shirt", "Shorts", "Socks", "Boots"])})

# ------------------------------------------------------------------ 2) proporções
CROTCH_M, CROTCH_G = 0.73, 0.885      # virilha: modelo -> jogo
SHOULDER_M, SHOULDER_G = 1.585, 1.59  # topo dos ombros


def zmap(z):
    if z <= CROTCH_M:
        return z * CROTCH_G / CROTCH_M
    if z <= SHOULDER_M:
        return CROTCH_G + (z - CROTCH_M) * (SHOULDER_G - CROTCH_G) / (SHOULDER_M - CROTCH_M)
    return z + (SHOULDER_G - SHOULDER_M)


# braços (em T): centro da seção a cada x, para achar a linha do braço
arm = np.abs(co[:, 0]) > 0.30
xs = np.arange(0.45, 0.80, 0.05)
cz = []
for x in xs:
    m = (np.abs(np.abs(co[:, 0]) - x) < 0.01) & (co[:, 2] > 1.0)
    cz.append([co[m, 1].mean(), co[m, 2].mean()])
cz = np.array(cz)
slope_z = np.polyfit(xs, cz[:, 1], 1)
slope_y = np.polyfit(xs, cz[:, 0], 1)
shoulder_line_z = np.polyval(slope_z, SH.x)
arm_dz = SH.z - shoulder_line_z          # braço inteiro sobe/desce até a linha passar pelo ombro do esqueleto
log("braço: inclinação %.3f, ajuste vertical %.3f" % (slope_z[0], arm_dz))
new = co.copy()
for i in range(len(co)):
    x, y, z = co[i]
    zt = zmap(z)
    w = min(1.0, max(0.0, (abs(x) - 0.26) / 0.10)) if z > 1.0 else 0.0   # 0 tronco .. 1 braço
    za = z + arm_dz
    new[i, 2] = zt * (1 - w) + za * w
# cabeça um pouco menor por igual (o modelo estilizado tem cabeça grande), em volta da base do pescoço
NECK = SHOULDER_G + 0.03
hd = new[:, 2] > NECK
k = 0.88
new[hd, 0] *= k
new[hd, 1] = (new[hd, 1] - new[hd, 1].mean()) * k + new[hd, 1].mean()
new[hd, 2] = NECK + (new[hd, 2] - NECK) * k
blend = (new[:, 2] > NECK - 0.04) & ~hd
t = (new[blend, 2] - (NECK - 0.04)) / 0.04
new[blend, 0] *= 1 - (1 - k) * t
me.vertices.foreach_set("co", new.ravel())
me.update()
co = new

# ------------------------------------------------------------------ 3) esqueleto em T sobre o modelo, pesos, volta para A
bpy.ops.object.select_all(action="DESELECT")
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.duplicate()
trig = bpy.context.view_layer.objects.active
trig.data.pose_position = "POSE"
bpy.ops.object.mode_set(mode="POSE")
arm_dir = Vector((1.0, slope_y[0], slope_z[0])).normalized()
for side in ("l", "r"):
    d = Vector((arm_dir.x * (1 if side == "l" else -1), arm_dir.y, arm_dir.z))
    for bn in ("upperarm_", "lowerarm_", "hand_"):
        pb = trig.pose.bones[bn + side]
        bpy.context.view_layer.update()
        cur = (trig.matrix_world @ pb.matrix).to_3x3() @ Vector((0, 1, 0))
        q = cur.rotation_difference(d)
        m = trig.matrix_world @ pb.matrix
        loc = m.to_translation()
        m2 = Matrix.Translation(loc) @ q.to_matrix().to_4x4() @ Matrix.Translation(-loc) @ m
        pb.matrix = trig.matrix_world.inverted() @ m2
        bpy.context.view_layer.update()
bpy.ops.pose.armature_apply(selected=False)   # a pose em T vira o repouso deste esqueleto auxiliar
bpy.ops.object.mode_set(mode="OBJECT")

# separa as peças (mesmo objeto por enquanto: materiais), prende ao esqueleto em T
names = ["Body", "Shirt", "Shorts", "Socks", "Boots"]
me.materials.clear()
for n in names:
    me.materials.append(bpy.data.materials.new(n))
me.polygons.foreach_set("material_index", cls)
me.update()
bpy.ops.object.select_all(action="DESELECT")
body.select_set(True)
trig.select_set(True)
bpy.context.view_layer.objects.active = trig
bpy.ops.object.parent_set(type="ARMATURE_AUTO")
log("pesos:", len(body.vertex_groups), "grupos")

# volta à pose do jogo: cada osso na matriz de repouso do esqueleto original
bpy.context.view_layer.objects.active = trig
bpy.ops.object.mode_set(mode="POSE")
order = []


def walk(b):
    order.append(b.name)
    for c in b.children:
        walk(c)


for b in trig.data.bones:
    if b.parent is None:
        walk(b)
for n in order:
    trig.pose.bones[n].matrix = trig.matrix_world.inverted() @ rest_mat[n]
    bpy.context.view_layer.update()
bpy.ops.object.mode_set(mode="OBJECT")
bpy.context.view_layer.objects.active = body
for m in body.modifiers:
    if m.type == "ARMATURE":
        bpy.ops.object.modifier_apply(modifier=m.name)
body.parent = None
bpy.data.objects.remove(trig, do_unlink=True)
mod = body.modifiers.new("Armature", "ARMATURE")
mod.object = rig
body.parent = rig
body.matrix_parent_inverse = rig.matrix_world.inverted()

# ------------------------------------------------------------------ 4) formas de corpo (variação)
mw = rig.matrix_world
seg = {b.name: (mw @ b.head_local, mw @ b.tail_local) for b in rig.data.bones}
TORSO = ("pelvis", "spine_01", "spine_02", "spine_03", "neck_01", "clavicle_l", "clavicle_r")


def radial(p, bone, f):
    a, b = seg[bone]
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(1e-9, ab.length_squared)))
    return (p - (a + ab * t)) * (f - 1.0)


def deltas(obj, ft, fl):
    gn = {g.index: g.name for g in obj.vertex_groups if g.name in seg}
    out = []
    for v in obj.data.vertices:
        p = obj.matrix_world @ v.co
        d = Vector((0, 0, 0))
        tot = 0.0
        for g in v.groups:
            n = gn.get(g.group)
            if not n or g.weight < 0.01:
                continue
            if n in TORSO:
                dd = Vector((p.x * (ft - 1), p.y * (ft - 1) * 0.8, 0))
            elif n.startswith(("thigh", "calf", "upperarm", "lowerarm")):
                dd = radial(p, n, fl)
            else:
                dd = Vector((0, 0, 0))
            d += dd * g.weight
            tot += g.weight
        out.append(obj.matrix_world.inverted().to_3x3() @ (d / tot if tot > 0 else d))
    return out


bpy.ops.object.select_all(action="DESELECT")
body.select_set(True)
bpy.context.view_layer.objects.active = body
bpy.ops.mesh.separate(type="MATERIAL")
parts = [o for o in rig.children if o.type == "MESH"]
for o in parts:
    mat = o.data.materials[0].name if o.data.materials else "Body"
    o.name = mat.split(".")[0]
    o.data.name = o.name
    o.shape_key_add(name="Basis", from_mix=False)
    for key, ft, fl in (("Body_Lean", 0.92, 0.9), ("Body_Strong", 1.07, 1.12)):
        sk = o.shape_key_add(name=key, from_mix=False)
        for i, d in enumerate(deltas(o, ft, fl)):
            sk.data[i].co = sk.data[i].co + d
    log("peça", o.name, len(o.data.vertices), "vértices")

# ------------------------------------------------------------------ exporta
os.makedirs(OUT, exist_ok=True)
bpy.ops.object.select_all(action="DESELECT")
rig.select_set(True)
for o in parts:
    o.select_set(True)
bpy.context.view_layer.objects.active = rig
path = os.path.join(OUT, "Athlete_base.fbx")
bpy.ops.export_scene.fbx(filepath=path, use_selection=True, add_leaf_bones=False, bake_anim=False,
                         object_types={"ARMATURE", "MESH"}, mesh_smooth_type="FACE",
                         use_mesh_modifiers=True, axis_forward="-Z", axis_up="Y",
                         apply_scale_options="FBX_SCALE_ALL", path_mode="COPY", embed_textures=False,
                         use_tspace=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "Athlete_base.blend"))
Image.open(TEX).convert("RGB").resize((2048, 2048), Image.LANCZOS).save(os.path.join(OUT, "athlete_base.jpg"), quality=92)
log("exportado", path, os.path.getsize(path) // 1024, "KB")
