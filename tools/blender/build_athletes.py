"""Gera os atletas do Craquez (FBX para o Unity) a partir do MakeHuman (CC0).

Uso:  python tools/blender/build_athletes.py <saida_dir> [--preview]

Para cada biotipo-base (africano, latino, europeu, asiático) cria um atleta
masculino de 25 anos, musculoso, com o esqueleto "game_engine" (53 ossos, nomes
estilo UE: pelvis, spine_01, thigh_l...). Todos os biotipos compartilham a
MESMA hierarquia de ossos, então um único conjunto de animações serve a todos.

O corpo recebe 5 materiais: Skin, Shirt, Shorts, Socks, Boots. As regiões do
uniforme são definidas pelo osso dominante de cada face, e os vértices de
roupa são "inflados" alguns milímetros para dar volume ao tecido. No Unity o
shader de uniforme pinta cores/padrões do clube em cada região.

Cada FBX leva também: olhos, sobrancelhas, cílios e 5 cortes de cabelo
(objetos Hair_*, o jogo liga só um), além de shape keys de rosto e de corpo
para variar cada jogador em tempo de execução (Face_*, Body_*).
"""
import math
import os
import sys

import bpy  # noqa: F401  (precisa vir antes de bmesh)
import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(__file__))
from mpfb_env import (AssetService, ExportService, HumanObjectProperties,  # noqa: E402
                      HumanService, ObjectService, TargetService)

OUT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else (sys.argv[1] if len(sys.argv) > 1 else "out")
PREVIEW = "--preview" in sys.argv
MH_KIT = "--mh-kit" in sys.argv   # camisa/calção das roupas do MakeHuman
ONLY = None
for a in sys.argv:
    if a.startswith("--only="):
        ONLY = a.split("=", 1)[1]

VARIANTS = {
    # nome: (african, asian, caucasian, pele, sobrancelha, altura, peso)
    "afro":    (0.85, 0.0, 0.15, "young_african_male.mhmat", "eyebrow001.mhclo", 0.58, 0.48),
    "latino":  (0.25, 0.10, 0.65, "young_caucasian_male2.mhmat", "eyebrow006.mhclo", 0.50, 0.50),
    "europeu": (0.0, 0.0, 1.0, "young_caucasian_male.mhmat", "eyebrow003.mhclo", 0.60, 0.46),
    "asiatico": (0.0, 0.90, 0.10, "young_asian_male.mhmat", "eyebrow009.mhclo", 0.45, 0.45),
}
HAIRS = ["short02.mhclo", "short01.mhclo", "short03.mhclo", "short04.mhclo", "afro01.mhclo",
         "long01.mhclo", "ponytail01.mhclo", "braid01.mhclo", "bob02.mhclo"]   # (mais estilos: longo, rabo de cavalo, tranças, chanel)

# Shape keys de variação (nome no FBX -> lista de (alvo MakeHuman, peso))
FACE_KEYS = {
    "Face_NoseWide": [("nose/nose-scale-horiz-incr.target.gz", 1.0), ("nose/nose-flaring-incr.target.gz", 0.6)],
    "Face_NoseLong": [("nose/nose-scale-vert-incr.target.gz", 1.0), ("nose/nose-hump-incr.target.gz", 0.5)],
    "Face_JawWide": [("chin/chin-width-incr.target.gz", 1.0), ("chin/chin-bones-incr.target.gz", 0.7)],
    "Face_ChinLong": [("chin/chin-height-incr.target.gz", 1.0), ("chin/chin-prominent-incr.target.gz", 0.6)],
    "Face_CheekBones": [("cheek/l-cheek-bones-incr.target.gz", 1.0), ("cheek/r-cheek-bones-incr.target.gz", 1.0)],
    "Face_Lean": [("head/head-fat-decr.target.gz", 1.0), ("cheek/l-cheek-inner-decr.target.gz", 0.6),
                  ("cheek/r-cheek-inner-decr.target.gz", 0.6)],
    "Face_Round": [("head/head-round.target.gz", 1.0), ("head/head-fat-incr.target.gz", 0.5)],
    "Face_EyesBig": [("eyes/l-eye-scale-incr.target.gz", 0.7), ("eyes/r-eye-scale-incr.target.gz", 0.7)],
    "Face_BrowHeavy": [("forehead/forehead-nubian-incr.target.gz", 1.0),
                       ("forehead/forehead-temple-incr.target.gz", 0.5)],
    "Face_LipsFull": [("mouth/mouth-scale-vert-incr.target.gz", 0.8), ("mouth/mouth-upperlip-volume-incr.target.gz", 0.6),
                      ("mouth/mouth-lowerlip-volume-incr.target.gz", 0.6)],
    # 2.21: o outro lado de cada traço (mais rostos diferentes) e formatos de cabeça
    "Face_NoseNarrow": [("nose/nose-scale-horiz-decr.target.gz", 1.0)],
    "Face_NoseShort": [("nose/nose-scale-vert-decr.target.gz", 1.0)],
    "Face_JawNarrow": [("chin/chin-width-decr.target.gz", 1.0)],
    "Face_ChinShort": [("chin/chin-height-decr.target.gz", 1.0)],
    "Face_EyesSmall": [("eyes/l-eye-scale-decr.target.gz", 0.7), ("eyes/r-eye-scale-decr.target.gz", 0.7)],
    "Face_LipsThin": [("mouth/mouth-scale-vert-decr.target.gz", 0.8)],
    "Face_MouthWide": [("mouth/mouth-scale-horiz-incr.target.gz", 0.8)],
    "Face_EarsBig": [("ears/l-ear-scale-incr.target.gz", 0.8), ("ears/r-ear-scale-incr.target.gz", 0.8)],
    "Face_EyesSlant": [("eyes/l-eye-eyefold-angle-up.target.gz", 0.8), ("eyes/r-eye-eyefold-angle-up.target.gz", 0.8)],
    "Face_ForeheadTall": [("forehead/forehead-scale-vert-incr.target.gz", 1.0)],
    "Face_Oval": [("head/head-oval.target.gz", 1.0)],
    "Face_Square": [("head/head-square.target.gz", 1.0)],
    "Face_Triangle": [("head/head-invertedtriangular.target.gz", 1.0)],
}

KIT_SLOTS = ["Skin", "Shirt", "Shorts", "Socks", "Boots"]
INFLATE = {"Shirt": 0.010, "Shorts": 0.016, "Socks": 0.004, "Boots": 0.007}


def log(*a):
    print("[athlete]", *a, flush=True)


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def asset(name, sub):
    p = AssetService.find_asset_absolute_path(name, asset_subdir=sub)
    if p is None:
        raise RuntimeError(f"asset {sub}/{name} não encontrado (instale makehuman_system_assets)")
    return p


def make_human(v):
    afr, asi, cau, skin, brow, height, weight = v
    h = HumanService.create_human()
    for k, val in [("gender", 1.0), ("age", 0.5), ("muscle", 0.6), ("weight", weight), ("height", height),
                   ("proportions", 0.75), ("african", afr), ("asian", asi), ("caucasian", cau)]:
        HumanObjectProperties.set_value(k, val, entity_reference=h)
    TargetService.reapply_macro_details(h)
    # físico de atleta: pernas e glúteos fortes, cintura fina
    for name, w in [("legs/l-upperleg-muscle-incr", 0.35), ("legs/r-upperleg-muscle-incr", 0.35),
                    ("legs/l-lowerleg-muscle-incr", 0.4), ("legs/r-lowerleg-muscle-incr", 0.4),
                    ("stomach/stomach-pregnant-decr", 0.45), ("torso/torso-vshape-incr", 0.06),
                    # tronco de jogador de futebol: ombros na largura natural, peitoral discreto, tórax menos fundo
                    # (antes os ombros eram apertados no jogo, empurrando a malha e estufando o peito)
                    ("torso/measure-shoulder-dist-decr", 0.5), ("torso/torso-muscle-pectoral-decr", 0.55),
                    ("torso/torso-scale-depth-decr", 0.3), ("torso/measure-bust-circ-decr", 0.3),
                    ("neck/neck-scale-horiz-incr", 0.1)]:
        p = TargetService.target_full_path(name.split("/")[-1])
        if p:
            TargetService.load_target(h, p, weight=w, name=name.split("/")[-1])
    HumanService.set_character_skin(asset(skin, "skins"), h, skin_type="GAMEENGINE")
    rig = HumanService.add_builtin_rig(h, "game_engine")
    for sub, fname, atype in [("eyes", "low-poly.mhclo", "Eyes"), ("eyebrows", brow, "Eyebrows"),
                              ("eyelashes", "eyelashes02.mhclo", "Eyelashes")]:
        HumanService.add_mhclo_asset(asset(fname, sub), h, asset_type=atype, material_type="GAMEENGINE")
    for hair in HAIRS:
        HumanService.add_mhclo_asset(asset(hair, "hair"), h, asset_type="Hair", material_type="GAMEENGINE")
    if MH_KIT:
        # camiseta e calça do MakeHuman (CC0), encaixadas no corpo: viram camisa e calção de jogo
        HumanService.add_mhclo_asset(asset("male_casualsuit04.mhclo", "clothes"), h, asset_type="Clothes", material_type="GAMEENGINE")
    return h, rig


def add_face_keys(basemesh):
    root = os.path.join(os.path.dirname(TargetService.target_full_path("nose-scale-horiz-incr")), "..")
    me = basemesh.data
    if me.shape_keys is None:
        basemesh.shape_key_add(name="Basis", from_mix=False)
    basis = me.shape_keys.key_blocks[0]
    import gzip
    for key, parts in FACE_KEYS.items():
        sk = basemesh.shape_key_add(name=key, from_mix=False)
        for rel, w in parts:
            path = os.path.normpath(os.path.join(root, rel))
            if not os.path.exists(path):
                log("alvo ausente", rel)
                continue
            with gzip.open(path, "rt") as f:
                for line in f:
                    line = line.strip()
                    if not line or line.startswith("#"):
                        continue
                    i, x, y, z = line.split()
                    i = int(i)
                    # alvos MakeHuman: (x, y, z) com y para cima -> Blender (x, -z, y)
                    d = Vector((float(x), -float(z), float(y))) * w * 0.1
                    sk.data[i].co = sk.data[i].co + d
        sk.value = 0.0
    log("shape keys de rosto:", len(FACE_KEYS))


def _bone_ws(rig, name):
    b = rig.data.bones[name]
    return rig.matrix_world @ b.head_local, rig.matrix_world @ b.tail_local


def _dominant(bm, deform, bone_of_group):
    dom = {}
    for v in bm.verts:
        best, bw = "", 0.0
        for gi, w in v[deform].items():
            n = bone_of_group.get(gi)
            if n and w > bw:
                best, bw = n, w
        dom[v] = best
    return dom


def _face_bones(f, dom):
    return {dom.get(v, "") for v in f.verts}


def _cut(bm, deform, bone_of_group, prefixes, co, no):
    """Corta (bisect) as faces dos ossos indicados por um plano: borda reta da roupa."""
    dom = _dominant(bm, deform, bone_of_group)
    faces = [f for f in bm.faces if any(b.startswith(prefixes) for b in _face_bones(f, dom))]
    if not faces:
        return
    verts = {v for f in faces for v in f.verts}
    edges = {e for f in faces for e in f.edges}
    bmesh.ops.bisect_plane(bm, geom=list(verts) + list(edges) + faces, dist=0.0005,
                           plane_co=co, plane_no=no, clear_inner=False, clear_outer=False)


def _side(p, co, no):
    return (p - co).dot(no)


def apply_kit(obj, rig):
    me = obj.data
    bones = set(rig.data.bones.keys())
    bone_of_group = {g.index: g.name for g in obj.vertex_groups if g.name in bones}
    mw = obj.matrix_world
    assert mw == mw.Identity(4) or True
    # planos de corte (coordenadas do objeto = mundo, o corpo não tem transformação)
    cuts = []
    for s_ in ("l", "r"):
        h, t = _bone_ws(rig, f"upperarm_{s_}")
        d = (t - h).normalized()
        cuts.append((f"sleeve_{s_}", ("upperarm_" + s_,), h + (t - h) * 0.46, d))
        h, t = _bone_ws(rig, f"thigh_{s_}")
        d = (t - h).normalized()
        cuts.append((f"hem_{s_}", ("thigh_" + s_,), h + (t - h) * 0.50, d))
        h, t = _bone_ws(rig, f"calf_{s_}")
        d = (t - h).normalized()
        cuts.append((f"sock_{s_}", ("calf_" + s_, "thigh_" + s_), h + (t - h) * 0.14, d))
        cuts.append((f"boot_{s_}", ("calf_" + s_, "foot_" + s_), h + (t - h) * 0.965, d))
    ph, _ = _bone_ws(rig, "pelvis")
    nh, nt = _bone_ws(rig, "neck_01")
    waist = (ph + Vector((0, 0, 0.085)), Vector((0, 0, 1)))
    # gola: plano inclinado (mais baixa na frente, -Y é a frente do MakeHuman)
    collar_no = Vector((0, -0.45, 1)).normalized()
    collar = (nh + Vector((0, -0.012, 0.004)), collar_no)
    cuts.append(("waist", ("pelvis", "spine_01"), waist[0], waist[1]))
    cuts.append(("collar", ("neck_01", "spine_03", "clavicle", "head"), collar[0], collar[1]))

    skin_mat = me.materials[0] if me.materials else bpy.data.materials.new("Skin")
    skin_mat.name = "Skin"
    me.materials.clear()
    for n in KIT_SLOTS:
        if n == "Skin":
            me.materials.append(skin_mat)
        else:
            me.materials.append(bpy.data.materials.get(n) or bpy.data.materials.new(n))
    bm = bmesh.new()
    bm.from_mesh(me)
    deform = bm.verts.layers.deform.verify()
    for _, pref, co, no in cuts:
        _cut(bm, deform, bone_of_group, pref, co, no)
    bm.verts.ensure_lookup_table()
    bm.faces.ensure_lookup_table()
    dom = _dominant(bm, deform, bone_of_group)
    planes = {n: (co, no) for n, _, co, no in cuts}

    def region(f):
        c = f.calc_center_median()
        bs = [dom[v] for v in f.verts]
        b = max(set(bs), key=bs.count)
        side = b[-1] if b.endswith(("_l", "_r")) else ""
        if b.startswith(("spine", "clavicle")):
            if b == "spine_01" and _side(c, *waist) <= 0:
                return "Shorts"
            return "Shirt" if _side(c, *collar) < 0 or not b.startswith("spine_03") else "Skin"
        if b == "neck_01" or b == "head":
            return "Shirt" if _side(c, *collar) < 0 else "Skin"
        if b.startswith("upperarm"):
            return "Shirt" if _side(c, *planes["sleeve_" + side]) < 0 else "Skin"
        if b.startswith(("lowerarm", "hand", "thumb", "index", "middle", "ring", "pinky")):
            return "Skin"
        if b == "pelvis":
            return "Shirt" if _side(c, *waist) > 0 else "Shorts"
        if b.startswith("thigh"):
            if _side(c, *planes["hem_" + side]) < 0:
                return "Shorts" if _side(c, *waist) <= 0 else "Shirt"
            return "Socks" if _side(c, *planes["sock_" + side]) > 0 else "Skin"
        if b.startswith("calf"):
            if _side(c, *planes["boot_" + side]) > 0:
                return "Boots"
            return "Socks" if _side(c, *planes["sock_" + side]) > 0 else "Skin"
        if b.startswith(("foot", "ball")):
            return "Boots"
        return "Skin"

    reg = {f: region(f) for f in bm.faces}
    idx = {n: i for i, n in enumerate(KIT_SLOTS)}
    for f, r in reg.items():
        f.material_index = idx[r]

    bm.to_mesh(me)
    bm.free()
    me.update()

    for g in [g for g in obj.vertex_groups if g.name not in bones]:
        obj.vertex_groups.remove(g)
    log("uniforme:", {n: sum(1 for p in me.polygons if p.material_index == i) for n, i in idx.items()},
        "verts", len(me.vertices))


def remove_helpers(obj):
    groups = [g for g in obj.vertex_groups if g.name in ("HelperGeometry", "JointCubes")]
    if not groups:
        return
    gi = {g.index for g in groups}
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    deform = bm.verts.layers.deform.active
    kill = [v for v in bm.verts if deform and any(k in gi and w > 0.5 for k, w in v[deform].items())]
    bmesh.ops.delete(bm, geom=kill, context="VERTS")
    bm.to_mesh(obj.data)
    bm.free()
    for g in groups:
        obj.vertex_groups.remove(g)
    for m in list(obj.modifiers):
        if m.type == "MASK":
            obj.modifiers.remove(m)
    log("helpers removidos:", len(kill), "restam", len(obj.data.vertices))


def build(name, v):
    reset_scene()
    h, rig = make_human(v)
    TargetService.bake_targets(h)
    add_face_keys(h)
    # proxies (cabelo etc.) já vêm com pesos; remove geometria auxiliar do corpo
    remove_helpers(h)
    for m in list(h.modifiers):
        if m.type == "SUBSURF":
            h.modifiers.remove(m)
    apply_kit(h, rig)
    import garments
    cloth = None
    if MH_KIT:
        cloth = next((c for c in ObjectService.get_list_of_children(rig) if "casualsuit04" in c.name.lower()), None)
    garments.make_all(h, rig, cloth)
    h.name = "Body"
    h.data.name = "Body"
    rig.name = "Armature"
    for c in ObjectService.get_list_of_children(rig):
        if c == h:
            continue
        base = c.name.split(".")[-1].lower()
        if c.name in ("Shirt", "Shorts", "Socks", "Boots"):
            continue
        if base + ".mhclo" in HAIRS:
            c.name = "Hair_" + base
        elif "eyebrow" in base:
            c.name = "Eyebrows"
        elif "eyelash" in base:
            c.name = "Eyelashes"
        elif "low-poly" in base:
            c.name = "Eyes"
        for m in list(c.modifiers):
            if m.type in ("SUBSURF", "MASK"):
                c.modifiers.remove(m)
    if FIT:
        fit_body(rig)
    names = sorted(o.name for o in ObjectService.get_list_of_children(rig))
    log(name, "objetos:", names)
    return h, rig


# ---------------------------------------------------------------- porte do corpo (2.22)
# Molde: modelo de jogador enviado pelo dono do jogo (medido em cortes horizontais e nos braços, ver
# tools/blender/README). Aplicada só METADE da diferença medida (parte dela é roupa folgada e caneleira),
# limitada a +30%. Tronco: fatores de largura (x) e profundidade (y) por altura; pernas e braços: raio
# em volta do osso. Cabeça, mãos e pés ficam iguais.
FIT = "--fit" in sys.argv   # (2.24: voltou ao corpo da 2.21 — o porte do molde fica desligado)
FIT_TORSO = [  # (altura, largura, profundidade na frente, profundidade atrás)
    # (frente do peito sem crescer — e um pouco mais plana: o peito ficava "saltado"; o volume vai para as
    # costas, a cintura e os lados)
    (0.98, 1.06, 1.05, 1.08), (1.06, 1.07, 1.06, 1.10), (1.15, 1.08, 1.08, 1.14), (1.22, 1.13, 1.08, 1.18),
    (1.30, 1.11, 1.0, 1.15), (1.36, 1.10, 0.96, 1.14), (1.42, 1.11, 0.95, 1.14), (1.50, 1.10, 0.98, 1.12),
    (1.58, 1.08, 1.0, 1.08), (1.66, 1.08, 1.0, 1.06), (1.72, 1.0, 1.0, 1.0),
]
FIT_LEG = [(1.02, 1.12), (0.95, 1.16), (0.80, 1.18), (0.65, 1.2), (0.52, 1.12), (0.40, 1.18), (0.30, 1.1), (0.18, 1.03), (0.10, 1.0)]
FIT_ARM = {"upperarm": 1.12, "lowerarm": 1.12}
TORSO_BONES = ("pelvis", "spine_01", "spine_02", "spine_03", "neck_01", "clavicle_l", "clavicle_r")


def _interp(table, z, cols):
    t = sorted(table)
    if z <= t[0][0]:
        return t[0][1:1 + cols]
    if z >= t[-1][0]:
        return t[-1][1:1 + cols]
    for a, b in zip(t, t[1:]):
        if a[0] <= z <= b[0]:
            k = (z - a[0]) / (b[0] - a[0])
            return tuple(a[i] + (b[i] - a[i]) * k for i in range(1, 1 + cols))


def fit_body(rig):
    """Engrossa o corpo e o uniforme no porte do molde (mesma deformação em todas as malhas e shape keys)."""
    mw = rig.matrix_world
    seg = {b.name: (mw @ b.head_local, mw @ b.tail_local) for b in rig.data.bones}
    # eixo do tronco (y do meio do corpo em cada altura): pelas cabeças dos ossos da coluna
    spine = sorted([(seg[n][0].z, seg[n][0].y) for n in ("pelvis", "spine_01", "spine_02", "spine_03", "neck_01", "head")])

    def axis_y(z):
        return _interp([(a, b) for a, b in spine], z, 1)[0]

    def radial(p, bone, f):
        a, b = seg[bone]
        ab = b - a
        t = max(0.0, min(1.0, (p - a).dot(ab) / max(1e-9, ab.length_squared)))
        c = a + ab * t
        return (p - c) * (f - 1.0)

    def delta(p, weights):
        d = Vector((0, 0, 0))
        tot = 0.0
        for bone, w in weights:
            if bone in TORSO_BONES:
                fw, ff, fb = _interp(FIT_TORSO, p.z, 3)
                yc = axis_y(p.z)
                fd = ff if p.y < yc else fb   # (a frente do corpo é -y)
                dd = Vector((p.x * (fw - 1.0), (p.y - yc) * (fd - 1.0), 0.0))
            elif bone.startswith(("thigh", "calf")):
                dd = radial(p, bone, _interp(FIT_LEG, p.z, 1)[0])
            elif bone.startswith("upperarm"):
                dd = radial(p, bone, FIT_ARM["upperarm"])
            elif bone.startswith("lowerarm"):
                dd = radial(p, bone, FIT_ARM["lowerarm"])
            else:
                dd = Vector((0, 0, 0))
            d += dd * w
            tot += w
        return d / tot if tot > 0 else d

    bones = set(rig.data.bones.keys())
    for o in ObjectService.get_list_of_children(rig):
        if o.type != "MESH" or o.name.startswith("Hair_") or o.name in ("Eyes", "Eyebrows", "Eyelashes"):
            continue
        me = o.data
        gn = {g.index: g.name for g in o.vertex_groups if g.name in bones}
        omw = o.matrix_world
        inv = omw.inverted()
        deltas = []
        for v in me.vertices:
            ws = [(gn[g.group], g.weight) for g in v.groups if g.group in gn and g.weight > 0.01]
            p = omw @ v.co
            deltas.append(inv.to_3x3() @ delta(p, ws) if ws else Vector((0, 0, 0)))
        if me.shape_keys:
            # todas as formas (rosto) andam junto; a malha segue a forma base
            for kb in me.shape_keys.key_blocks:
                for i, d in enumerate(deltas):
                    kb.data[i].co = kb.data[i].co + d
            basis = me.shape_keys.reference_key
            for i in range(len(deltas)):
                me.vertices[i].co = basis.data[i].co
        else:
            for i, d in enumerate(deltas):
                me.vertices[i].co = me.vertices[i].co + d
        me.update()
        log("porte:", o.name, len(deltas), "vértices")


def export(name, rig):
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    for c in ObjectService.get_list_of_children(rig):
        c.select_set(True)
    bpy.context.view_layer.objects.active = rig
    path = os.path.join(OUT, f"Athlete_{name}.fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, add_leaf_bones=False, bake_anim=False,
                             object_types={"ARMATURE", "MESH"}, mesh_smooth_type="FACE",
                             use_mesh_modifiers=True, axis_forward="-Z", axis_up="Y",
                             apply_scale_options="FBX_SCALE_ALL", path_mode="COPY", embed_textures=False,
                             use_tspace=True)
    log("exportado", path, os.path.getsize(path) // 1024, "KB")
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, f"Athlete_{name}.blend"))


def preview(name, rig):
    import preview_render
    preview_render.render_character(rig, os.path.join(OUT, f"preview_{name}.png"))


if __name__ == "__main__":
    for name, v in VARIANTS.items():
        if ONLY and name != ONLY:
            continue
        h, rig = build(name, v)
        export(name, rig)
        if PREVIEW:
            preview(name, rig)
