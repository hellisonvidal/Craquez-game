"""Roupas do atleta como peças próprias (camisa, calção, meião, chuteira).

Parte do corpo já classificado por apply_kit (material_index = Skin/Shirt/Shorts/
Socks/Boots). Cada região é copiada para um objeto novo que herda os pesos dos
ossos e os UVs do corpo; a peça é alisada (tecido não marca o músculo), afastada
do corpo com folga variável, ganha barra/punho/gola e dobras na geometria. A pele
por baixo é recolhida para não atravessar a roupa nas animações.
"""
import math

import bmesh
import bpy
from mathutils import Vector, noise

SLOTS = ["Skin", "Shirt", "Shorts", "Socks", "Boots"]


def log(*a):
    print("[roupa]", *a, flush=True)


def _bone(rig, n):
    b = rig.data.bones[n]
    return rig.matrix_world @ b.head_local, rig.matrix_world @ b.tail_local


def _t_along(rig, bone, co):
    h, t = _bone(rig, bone)
    ax = t - h
    return (co - h).dot(ax) / max(ax.length_squared, 1e-9)


def _dist_axis(rig, bone, co):
    h, t = _bone(rig, bone)
    ax = (t - h).normalized()
    p = co - h
    return (p - ax * p.dot(ax))


def _dominant(obj, rig):
    bones = set(rig.data.bones.keys())
    names = {g.index: g.name for g in obj.vertex_groups if g.name in bones}
    out = []
    for v in obj.data.vertices:
        best, bw = "", 0.0
        for g in v.groups:
            if g.group in names and g.weight > bw:
                best, bw = names[g.group], g.weight
        out.append(best)
    return out


def _copy_region(body, slot_index, name):
    ob = body.copy()
    ob.data = body.data.copy()
    ob.name = name
    ob.data.name = name
    for c in body.users_collection:
        c.objects.link(ob)
    if ob.data.shape_keys:
        ob.shape_key_clear()
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    kill = [f for f in bm.faces if f.material_index != slot_index]
    bmesh.ops.delete(bm, geom=kill, context="FACES")
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")
    for f in bm.faces:
        f.material_index = 0
        f.smooth = True
    bm.to_mesh(ob.data)
    bm.free()
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    ob.data.materials.clear()
    ob.data.materials.append(mat)
    return ob


def _taubin(bm, verts, iters):
    for it in range(iters * 2):
        fac = 0.5 if it % 2 == 0 else -0.53
        new = {}
        for v in verts:
            nb = [e.other_vert(v).co for e in v.link_edges]
            if not nb:
                continue
            avg = sum(nb, Vector()) / len(nb)
            new[v] = v.co + (avg - v.co) * fac
        for v, c in new.items():
            v.co = c


def _drape(verts, center_fn, ang_win=0.35, h_win=0.04, keep=0.99):
    """Tecido esticado: em coordenadas cilíndricas em volta do eixo do tronco, cada
    ponto sobe até o raio dos vizinhos mais salientes (cobre vãos entre músculos)."""
    data = []
    for v in verts:
        c = center_fn(v.co)
        d = Vector((v.co.x - c.x, v.co.y - c.y))
        data.append((v, math.atan2(d.y, d.x), v.co.z, d.length, c))
    out = {}
    for v, a, z, r, c in data:
        best = r
        for v2, a2, z2, r2, _ in data:
            if abs(z2 - z) > h_win:
                continue
            da = abs((a2 - a + math.pi) % (2 * math.pi) - math.pi)
            if da < ang_win:
                w = 1.0 - (da / ang_win) ** 2
                best = max(best, r + (r2 - r) * w)
        if best > r:
            k = (best * keep) if best * keep > r else r
            out[v] = c + Vector((math.cos(a) * k, math.sin(a) * k, 0)) + Vector((0, 0, z - c.z))
    for v, co in out.items():
        v.co = co


def _boundary_loops(bm):
    edges = [e for e in bm.edges if e.is_boundary]
    seen, loops = set(), []
    adj = {}
    for e in edges:
        for v in e.verts:
            adj.setdefault(v, []).append(e)
    for e in edges:
        if e in seen:
            continue
        loop_e, stack = [], [e]
        while stack:
            x = stack.pop()
            if x in seen:
                continue
            seen.add(x)
            loop_e.append(x)
            for v in x.verts:
                stack.extend(y for y in adj[v] if y not in seen)
        loops.append(loop_e)
    return loops


def _extrude_loop(bm, loop_edges, offset_fn):
    """Extruda uma borda; offset_fn(vert) -> deslocamento do novo vértice."""
    res = bmesh.ops.extrude_edge_only(bm, edges=loop_edges)
    new_verts = [g for g in res["geom"] if isinstance(g, bmesh.types.BMVert)]
    for v in new_verts:
        v.co = v.co + offset_fn(v)
    for g in res["geom"]:
        if isinstance(g, bmesh.types.BMFace):
            g.smooth = True
    return new_verts


def _finish(ob, bm):
    bm.normal_update()
    bm.to_mesh(ob.data)
    bm.free()
    ob.data.update()


def make_shirt(body, rig, slot):
    ob = _copy_region(body, slot, "Shirt")
    dom = _dominant(ob, rig)
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bm.verts.ensure_lookup_table()
    bdom = {v: dom[v.index] for v in bm.verts}
    interior = [v for v in bm.verts if not v.is_boundary]
    # tronco (fora das mangas): tecido esticado sobre peitoral/abdome/costas
    def on_arm(co):
        b = "upperarm_l" if co.x > 0 else "upperarm_r"
        return _t_along(rig, b, co) > 0.05 and _dist_axis(rig, b, co).length < 0.12
    torso = [v for v in interior if not on_arm(v.co) and abs(v.co.x) < 0.13]
    ys = [v.co.y for v in torso] or [0.0]
    cy = (max(ys) + min(ys)) / 2
    _drape(torso, lambda co: Vector((0.0, cy, co.z)), ang_win=0.2, keep=0.97)   # (menos "barril": segue o tronco)
    _taubin(bm, interior, 24)
    # uma subdivisão: dobras com mais definição
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=1, use_grid_fill=True, smooth=0.0)
    bm.verts.ensure_lookup_table()
    bm.normal_update()
    pelvis_z = _bone(rig, "pelvis")[0].z
    neck_z = _bone(rig, "neck_01")[0].z
    torso_h = max(0.2, neck_z - pelvis_z)
    for v in bm.verts:
        p = v.co.copy()
        side_l = p.x > 0
        arm_b = "upperarm_l" if side_l else "upperarm_r"
        t_arm = _t_along(rig, arm_b, p)
        on_arm = t_arm > 0.08 and _dist_axis(rig, arm_b, p).length < 0.11
        if on_arm:
            # manga: mais larga perto da boca
            d = 0.005 + 0.006 * min(1.0, max(0.0, (t_arm - 0.1) / 0.4))
            # dobras transversais na manga
            d += 0.0012 * math.sin(t_arm * 60.0 + noise.noise(p * 9.0) * 3.0)
        else:
            h = (p.z - pelvis_z) / torso_h            # 0 cintura .. 1 pescoço
            loose = 0.005 + 0.007 * max(0.0, 1.0 - h * 1.3)    # corte justo (camisa moderna, colada no peito)
            if h > 0.85:
                loose = 0.006                          # gola rente ao pescoço
            # dobras horizontais na cintura e nas laterais
            side = abs(p.x) / 0.18
            fold = math.sin(p.z * 70.0 + noise.noise(p * 6.0) * 4.0) * (0.0022 * max(0.0, 1.0 - h * 1.6)) * (0.5 + 0.5 * min(1.0, side))
            d = loose + fold
        v.co = p + v.normal * d
    # barra da camisa, punhos e gola
    loops = _boundary_loops(bm)
    for lp in loops:
        vs = {v for e in lp for v in e.verts}
        cz = sum(v.co.z for v in vs) / len(vs)
        cx = sum(v.co.x for v in vs) / len(vs)
        if cz < pelvis_z + 0.15:          # barra (para fora do calção)
            def off(v, cx=cx):
                outward = Vector((v.co.x, v.co.y, 0.0)).normalized() * 0.012
                return Vector((0, 0, -0.055)) + outward * 0.4
            _extrude_loop(bm, lp, off)
        elif cz > neck_z - 0.03:          # gola: pequena faixa levantada
            _extrude_loop(bm, lp, lambda v: Vector((0, 0, 0.006)) + Vector((v.co.x, v.co.y, 0)).normalized() * -0.002)
        else:                             # manga: punho levemente aberto
            arm_b = "upperarm_l" if cx > 0 else "upperarm_r"
            h, t = _bone(rig, arm_b)
            ax = (t - h).normalized()
            _extrude_loop(bm, lp, lambda v, ax=ax: ax * 0.012)
    _finish(ob, bm)
    log("camisa", len(ob.data.vertices), "vértices")
    return ob


def make_shorts(body, rig, slot):
    ob = _copy_region(body, slot, "Shorts")
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    interior = [v for v in bm.verts if not v.is_boundary]
    _taubin(bm, interior, 10)
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=1, use_grid_fill=True, smooth=0.0)
    bm.normal_update()
    for v in bm.verts:
        p = v.co.copy()
        thigh = "thigh_l" if p.x > 0 else "thigh_r"
        t = _t_along(rig, thigh, p)
        d = 0.009 + 0.011 * max(0.0, min(1.0, t / 0.45))     # perna do calção levemente solta
        d += 0.003 * math.sin(math.atan2(p.y, p.x - (0.09 if p.x > 0 else -0.09)) * 9.0 + noise.noise(p * 7.0) * 2.0) * max(0.0, t)
        v.co = p + v.normal * d
    pelvis_z = _bone(rig, "pelvis")[0].z
    for lp in _boundary_loops(bm):
        vs = {v for e in lp for v in e.verts}
        cz = sum(v.co.z for v in vs) / len(vs)
        if cz < pelvis_z - 0.05:     # boca das pernas
            def off(v):
                thigh = "thigh_l" if v.co.x > 0 else "thigh_r"
                h, t = _bone(rig, thigh)
                ax = (t - h).normalized()
                rad = _dist_axis(rig, thigh, v.co).normalized()
                return ax * 0.02 + rad * 0.002
            _extrude_loop(bm, lp, off)
    _finish(ob, bm)
    log("calção", len(ob.data.vertices), "vértices")
    return ob


def make_socks(body, rig, slot):
    ob = _copy_region(body, slot, "Socks")
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    interior = [v for v in bm.verts if not v.is_boundary]
    _taubin(bm, interior, 5)
    bm.normal_update()
    for v in bm.verts:
        v.co = v.co + v.normal * 0.005
    knee = max(_bone(rig, "calf_l")[0].z, 0.3)
    for lp in _boundary_loops(bm):
        vs = {v for e in lp for v in e.verts}
        cz = sum(v.co.z for v in vs) / len(vs)
        if cz > knee - 0.2:   # cano: dobra para fora
            calf = "calf_l" if sum(v.co.x for v in vs) > 0 else "calf_r"
            _extrude_loop(bm, lp, lambda v, c=calf: _dist_axis(rig, c, v.co).normalized() * 0.006 + Vector((0, 0, -0.025)))
    _finish(ob, bm)
    log("meião", len(ob.data.vertices), "vértices")
    return ob


def _foot_frame(rig, side):
    ah, at = _bone(rig, "foot_" + side)
    bh, bt = _bone(rig, "ball_" + side)
    f = Vector((bt.x - ah.x, bt.y - ah.y, 0.0)).normalized()
    up = Vector((0, 0, 1))
    lat = f.cross(up).normalized()
    return ah, bh, bt, f, lat, up


def make_boots(body, rig, slot):
    """Chuteira modelada: loft de perfis (calcanhar -> bico) sobre as medidas do pé,
    sola com travas, contraforte e gola acolchoada. UV próprio: u = comprimento
    (0 calcanhar .. 1 bico), v = volta do perfil (0 = centro da sola, 0.5 = peito do pé)."""
    mw = body.matrix_world
    inv = mw.inverted()
    me = body.data
    foot_pts = {"l": [], "r": []}
    for poly in me.polygons:
        if poly.material_index != slot:
            continue
        for vi in poly.vertices:
            co = mw @ me.vertices[vi].co
            foot_pts["l" if co.x > 0 else "r"].append(co)
    mesh = bpy.data.meshes.new("Boots")
    ob = bpy.data.objects.new("Boots", mesh)
    for c in body.users_collection:
        c.objects.link(ob)
    ob.parent = body.parent
    ob.matrix_world = mw.copy()
    for m in body.modifiers:
        if m.type == "ARMATURE":
            am = ob.modifiers.new("Armature", "ARMATURE")
            am.object = m.object
    bm = bmesh.new()
    uv_layer = bm.loops.layers.uv.new("UVMap")
    deform = bm.verts.layers.deform.verify()
    groups = {}

    def gidx(name):
        if name not in groups:
            groups[name] = ob.vertex_groups.new(name=name).index
        return groups[name]

    NS, NP = 22, 20   # seções ao longo do pé, pontos por perfil
    for side in ("l", "r"):
        pts = foot_pts[side]
        if not pts:
            continue
        ankle, ball, toe, f, lat, up = _foot_frame(rig, side)
        fs = [(p - ankle).dot(f) for p in pts]
        ls = [(p - ankle).dot(lat) for p in pts]
        f0, f1 = min(fs) - 0.008, max(fs) + 0.012
        sole = min(p.z for p in pts) - 0.004
        ball_u = ((ball - ankle).dot(f) - f0) / (f1 - f0)
        rings = []
        for i in range(NS):
            u = i / (NS - 1)
            fx = f0 + (f1 - f0) * u
            # meia-largura e altura medidas no pé (fatia), com folga
            sl = [l for l, ff in zip(ls, fs) if abs(ff - fx) < 0.02]
            sz = [p.z for p, ff in zip(pts, fs) if abs(ff - fx) < 0.02]
            mid = (max(sl) + min(sl)) / 2 if sl else 0.0
            hw = ((max(sl) - min(sl)) / 2 + 0.008) if sl else 0.03
            top = (max(sz) + 0.008) if sz else sole + 0.05
            # formato: calcanhar arredondado, bico afinado e baixo
            taper = math.sin(min(1.0, u * 4.0) * math.pi / 2) ** 0.6 * (1.0 - max(0.0, u - 0.86) / 0.14 * 0.3)
            hw = max(0.012, hw * (0.75 + 0.25 * taper) * (0.55 + 0.45 * taper))
            height = (top - sole) * (1.0 if u < 0.85 else (1.0 - (u - 0.85) / 0.15 * 0.55))
            if u < 0.35:
                height = max(height, 0.085 - u * 0.05)   # cano/contraforte
            ring = []
            for j in range(NP):
                a = j / NP * 2 * math.pi            # 0 = embaixo (centro da sola)
                cx, cz = -math.sin(a), -math.cos(a)
                # superelipse com base reta
                ex = math.copysign(abs(cx) ** 0.55, cx)
                ez = math.copysign(abs(cz) ** (0.35 if cz < 0 else 0.8), cz)
                zz = sole + (ez + 1.0) * 0.5 * height
                p = ankle + f * fx + lat * (mid + ex * hw)
                p.z = zz
                v = bm.verts.new(inv @ p)
                w_ball = min(1.0, max(0.0, (u - ball_u + 0.12) / 0.18))
                v[deform][gidx("foot_" + side)] = 1.0 - w_ball
                if w_ball > 0:
                    v[deform][gidx("ball_" + side)] = w_ball
                ring.append((v, u, j / NP))
            rings.append(ring)
        for i in range(NS - 1):
            for j in range(NP):
                a0, a1 = rings[i][j], rings[i][(j + 1) % NP]
                b0, b1 = rings[i + 1][j], rings[i + 1][(j + 1) % NP]
                try:
                    face = bm.faces.new((a0[0], b0[0], b1[0], a1[0]))
                except ValueError:
                    continue
                face.smooth = True
                for loop, (vv, uu, vv2) in zip(face.loops, (a0, b0, b1, a1)):
                    vfix = vv2 if not (j == NP - 1 and vv2 == 0.0) else 1.0
                    loop[uv_layer].uv = (uu, vfix if (vv2 > 0 or j < NP - 1) else 1.0)
        # tampas: calcanhar e bico (leque)
        for ring, uu in ((rings[0], 0.0), (rings[-1], 1.0)):
            c = sum((r[0].co for r in ring), Vector()) / NP
            cv = bm.verts.new(c)
            for g, w in ring[0][0][deform].items():
                cv[deform][g] = w
            for j in range(NP):
                a0, a1 = ring[j][0], ring[(j + 1) % NP][0]
                try:
                    face = bm.faces.new((a0, a1, cv) if uu == 0.0 else (a1, a0, cv))
                except ValueError:
                    continue
                face.smooth = True
                for loop in face.loops:
                    loop[uv_layer].uv = (uu, 0.02)
        # travas (6) sob a sola
        for k, (uu, ll) in enumerate([(0.12, -0.5), (0.12, 0.5), (0.55, -0.6), (0.55, 0.6), (0.75, -0.55), (0.75, 0.55)]):
            fx = f0 + (f1 - f0) * uu
            c = ankle + f * fx + lat * (ll * 0.026)
            c.z = sole
            r_ = 0.0065
            base = []
            for j in range(6):
                a = j / 6 * 2 * math.pi
                for dz in (0.0, -0.011):
                    p = c + f * math.cos(a) * r_ + lat * math.sin(a) * r_
                    p.z += dz
                    vv = bm.verts.new(inv @ p)
                    vv[deform][gidx(("foot_" if uu < 0.6 else "ball_") + side)] = 1.0
                    base.append(vv)
            for j in range(6):
                t0, b0_ = base[j * 2], base[j * 2 + 1]
                t1, b1_ = base[((j + 1) % 6) * 2], base[((j + 1) % 6) * 2 + 1]
                face = bm.faces.new((t0, t1, b1_, b0_))
                for loop in face.loops:
                    loop[uv_layer].uv = (0.99, 0.01)
            bot = bm.faces.new([base[j * 2 + 1] for j in range(6)][::-1])
            for loop in bot.loops:
                loop[uv_layer].uv = (0.99, 0.01)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(mesh)
    bm.free()
    mat = bpy.data.materials.get("Boots") or bpy.data.materials.new("Boots")
    mesh.materials.append(mat)
    mesh.update()
    log("chuteira", len(mesh.vertices), "vértices")
    return ob


def shrink_body(body, rig):
    """Recolhe a pele que fica sob a roupa e apaga os pés (dentro da chuteira)."""
    me = body.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.normal_update()
    covered = {}
    for f in bm.faces:
        if f.material_index != 0:
            for v in f.verts:
                covered[v] = max(covered.get(v, 0), f.material_index)
    skin_touch = {v for f in bm.faces if f.material_index == 0 for v in f.verts}
    shape_layers = list(bm.verts.layers.shape.values())
    for v, m in covered.items():
        if v in skin_touch:
            continue
        d = -0.007 if m in (1, 2) else -0.004
        off = v.normal * d
        v.co += off
        for lay in shape_layers:
            v[lay] = v[lay] + off
    feet = [f for f in bm.faces if f.material_index == 4]
    bmesh.ops.delete(bm, geom=feet, context="FACES")
    for f in bm.faces:
        f.material_index = 0
    bm.to_mesh(me)
    bm.free()
    skin = me.materials[0]
    me.materials.clear()
    me.materials.append(skin)
    me.update()


def make_all(body, rig, mh_cloth=None):
    slots = {n: i for i, n in enumerate(SLOTS)}
    if mh_cloth is not None:
        # camisa e calção vindos das roupas do MakeHuman (tecido de verdade: gola, mangas, caimento)
        import mh_kit
        mats = {n: body.data.materials[slots[n]] for n in ("Shirt", "Shorts")}
        shirt, shorts = mh_kit.make(mh_cloth, body, rig, mats)
    else:
        shirt, shorts = make_shirt(body, rig, slots["Shirt"]), make_shorts(body, rig, slots["Shorts"])
    parts = [shirt, shorts, make_socks(body, rig, slots["Socks"]), make_boots(body, rig, slots["Boots"])]
    shrink_body(body, rig)
    bones = set(rig.data.bones.keys())
    for ob in parts:
        for g in [g for g in ob.vertex_groups if g.name not in bones]:
            ob.vertex_groups.remove(g)
        ob.parent = rig
    return parts
