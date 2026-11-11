"""Camisa e calção a partir das roupas do MakeHuman (CC0), no lugar das peças tiradas do corpo.

A camiseta de manga curta da "male_casualsuit04" vira a camisa de jogo (gola redonda, mangas com volume,
caimento solto) e a calça do mesmo conjunto é cortada no meio da coxa e vira o calção, com barra. As peças
vêm encaixadas pelo MPFB no corpo de cada biotipo e com os pesos dos ossos do mesmo esqueleto.
"""
import bmesh
import bpy
from mathutils import Vector

import garments

ASSET = "male_casualsuit04.mhclo"


def _islands(bm):
    seen, out = set(), []
    for f in bm.faces:
        if f in seen:
            continue
        stack, isl = [f], []
        seen.add(f)
        while stack:
            g = stack.pop()
            isl.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h not in seen:
                        seen.add(h)
                        stack.append(h)
        out.append(isl)
    return out


def _clean(cloth):
    for m in list(cloth.modifiers):
        if m.type in ("SUBSURF", "MASK"):
            cloth.modifiers.remove(m)


def _split(cloth, name, keep_faces_fn, rig):
    ob = cloth.copy()
    ob.data = cloth.data.copy()
    ob.name = ob.data.name = name
    for c in cloth.users_collection:
        c.objects.link(ob)
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bm.faces.ensure_lookup_table()
    kill = [f for f in bm.faces if not keep_faces_fn(f)]
    bmesh.ops.delete(bm, geom=kill, context="FACES")
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")
    return ob, bm


def make(cloth, body, rig, slot_mats):
    """Devolve (camisa, calção) feitos da roupa do MakeHuman já encaixada (objeto cloth)."""
    _clean(cloth)
    pelvis_z = garments._bone(rig, "pelvis")[0].z
    bm0 = bmesh.new()
    bm0.from_mesh(cloth.data)
    isl = _islands(bm0)
    # cada ilha (camiseta, calça, cinto...) pela altura média: acima do quadril = camiseta
    upper = set()
    for faces in isl:
        z = sum(f.calc_center_median().z for f in faces) / len(faces)
        if z > pelvis_z + 0.08:
            upper.update(f.index for f in faces)
    bm0.free()
    shirt, bm = _split(cloth, "Shirt", lambda f: f.index in upper, rig)
    garments._finish(shirt, bm)

    def lower_keep(f):
        if f.index in upper:
            return False
        c = f.calc_center_median()
        if c.z > pelvis_z + 0.12:
            return False
        thigh = "thigh_l" if c.x > 0 else "thigh_r"
        return garments._t_along(rig, thigh, c) < 0.5     # calção: até o meio da coxa
    shorts, bm = _split(cloth, "Shorts", lower_keep, rig)
    # barra nas bocas das pernas
    for lp in garments._boundary_loops(bm):
        vs = {v for e in lp for v in e.verts}
        cz = sum(v.co.z for v in vs) / len(vs)
        if cz < pelvis_z - 0.1:
            def off(v):
                thigh = "thigh_l" if v.co.x > 0 else "thigh_r"
                h, t = garments._bone(rig, thigh)
                return (t - h).normalized() * 0.018 + garments._dist_axis(rig, thigh, v.co).normalized() * 0.004
            garments._extrude_loop(bm, lp, off)
    garments._finish(shorts, bm)
    for ob, slot in ((shirt, "Shirt"), (shorts, "Shorts")):
        # malha da roupa do MakeHuman é leve (proxy): uma subdivisão suaviza a silhueta (aplicada no export)
        for m in list(ob.modifiers):
            if m.type == "SUBSURF":
                ob.modifiers.remove(m)
        sub = ob.modifiers.new("suave", "SUBSURF")
        sub.levels = sub.render_levels = 1
        ob.data.materials.clear()
        ob.data.materials.append(slot_mats[slot])
        for p in ob.data.polygons:
            p.material_index = 0
        ob.data.update()
    bpy.data.objects.remove(cloth, do_unlink=True)
    garments.log("camisa (MakeHuman)", len(shirt.data.vertices), "vértices; calção", len(shorts.data.vertices))
    return shirt, shorts
