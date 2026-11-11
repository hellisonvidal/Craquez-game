"""Cenário do Quaternius (CC0) -> pacote leve para o jogo (Resources/Props).

    bpyenv/bin/python tools/blender/props.py <pasta dos FBX> <pasta das texturas> <saída> [--sheet]

Lê as árvores, arbustos, flores, pedras (Stylized Nature MegaKit) e as casas/prédios (variante
"_Mat", cores lisas), reduz os triângulos, passa para o eixo da Unity (y para cima) e grava tudo
num arquivo binário só (props.bytes), lido em tempo de jogo por PropLib.cs. As peças de cor lisa
apontam o UV para uma célula de uma paleta 8x8 (o jogo monta a paleta, com as cores que quiser);
as texturadas guardam o UV e o nome da textura (redimensionada e copiada para Tex/).

Formato (little-endian): "CQP1", int nPaleta, [nome], int nModelos, por modelo: nome, 6 floats
(caixa), int nPartes, por parte: chave, int nv, int ni, nv*(3 floats pos, 3 sbytes normal, 2
halfs uv), ni índices (ushort).
"""
import bpy, bmesh, os, sys, struct, glob
import numpy as np

PALETTE = ["Main", "Bricks", "Light", "White", "Glass", "Windows", "Dark", "Black", "Wood", "DarkWood", "DarkBrown"]

# (arquivo sem .fbx, nome no jogo, alvo de triângulos)
NATURE = [
    ("CommonTree_1", "Tree1", 2400), ("CommonTree_2", "Tree2", 2400), ("CommonTree_3", "Tree3", 2000),
    ("CommonTree_4", "Tree4", 2200), ("CommonTree_5", "Tree5", 2000),
    ("Pine_1", "Pine1", 1800), ("Pine_2", "Pine2", 1800), ("Pine_3", "Pine3", 2000), ("Pine_4", "Pine4", 1800), ("Pine_5", "Pine5", 1500),
    ("DeadTree_1", "Dead1", 1600), ("DeadTree_3", "Dead3", 1600),
    ("TwistedTree_1", "Twisted1", 2600), ("TwistedTree_3", "Twisted3", 2600),
    ("Bush_Common", "Bush", 900), ("Bush_Common_Flowers", "BushFlowers", 1000),
    ("Fern_1", "Fern", 400), ("Plant_1_Big", "Plant1", 400), ("Plant_7_Big", "Plant7", 200),
    ("Flower_3_Group", "Flowers3", 800), ("Flower_4_Group", "Flowers4", 1000),
    ("Grass_Common_Tall", "Grass1", 400), ("Grass_Wispy_Tall", "Grass2", 500),
    ("Rock_Medium_1", "Rock1", 400), ("Rock_Medium_2", "Rock2", 300), ("Rock_Medium_3", "Rock3", 500),
]
# textura de cada material (arquivo, tamanho final)
TEX = {
    "Bark_NormalTree": ("Bark_NormalTree.png", 512), "Bark_DeadTree": ("Bark_DeadTree.png", 512),
    "Bark_TwistedTree": ("Bark_TwistedTree.png", 512), "Leaves_NormalTree": ("Leaves_NormalTree_C.png", 512),
    "Leaves_TwistedTree": ("Leaves_TwistedTree_C.png", 512), "Leaves_Pine": ("Leaf_Pine_C.png", 512),
    "Leaves": ("Leaves.png", 512), "Flowers": ("Flowers.png", 512), "Grass": ("Grass.png", 256),
    "Rocks": ("Rocks_Diffuse.png", 512),
}


def import_join(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    ms = [o for o in bpy.data.objects if o.type == "MESH"]
    for o in bpy.data.objects:
        o.select_set(o in ms)
    bpy.context.view_layer.objects.active = ms[0]
    if len(ms) > 1:
        bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    o.data.transform(o.matrix_world)   # (posição também, no próprio mesh)
    o.matrix_world.identity()
    return o


def decimate(o, target, planar):
    tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
    if planar:
        m = o.modifiers.new("d", "DECIMATE")
        m.decimate_type = "DISSOLVE"
        m.angle_limit = np.radians(2.0)
        m.delimit = {"MATERIAL", "SEAM", "SHARP", "UV"}
        bpy.ops.object.modifier_apply(modifier="d")
    elif tris > target:
        m = o.modifiers.new("d", "DECIMATE")
        m.ratio = target / tris
        m.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier="d")
    m = o.modifiers.new("t", "TRIANGULATE")
    bpy.ops.object.modifier_apply(modifier="t")


def mat_key(m):
    n = m.name.split(".")[0]
    if n in TEX:
        return n
    if n in PALETTE:
        return "pal"
    return None


def extract(o, center_base):
    me = o.data
    me.calc_loop_triangles() if hasattr(me, "calc_loop_triangles") else None
    uvl = me.uv_layers.active.data if me.uv_layers.active else None
    co = np.array([v.co[:] for v in me.vertices])
    if center_base:
        mn, mx = co.min(0), co.max(0)
        off = np.array([(mn[0] + mx[0]) / 2, (mn[1] + mx[1]) / 2, mn[2]])
    else:
        off = np.array([0.0, 0.0, 0.0])
    parts = {}
    for tri in me.loop_triangles:
        mat = me.materials[tri.material_index] if tri.material_index < len(me.materials) else None
        key = mat_key(mat) if mat else None
        if key is None:
            continue
        pal = PALETTE.index(mat.name.split(".")[0]) if key == "pal" else -1
        P = parts.setdefault(key, {"v": [], "i": [], "map": {}})
        idx = []
        for li in tri.loops:
            vi = me.loops[li].vertex_index
            n = tri.normal if not tri.use_smooth else me.loops[li].normal if hasattr(me.loops[li], "normal") else me.vertices[vi].normal
            if key == "pal":
                uv = ((pal % 8 + 0.5) / 8.0, (pal // 8 + 0.5) / 8.0)
            else:
                uv = tuple(uvl[li].uv) if uvl else (0.0, 0.0)
            p = co[vi] - off
            # Blender (z para cima, mão direita) -> Unity (y para cima, mão esquerda): (x, z, y)
            k = (round(p[0], 4), round(p[2], 4), round(p[1], 4), round(n[0], 2), round(n[2], 2), round(n[1], 2), round(uv[0], 4), round(uv[1], 4))
            j = P["map"].get(k)
            if j is None:
                j = len(P["v"])
                P["map"][k] = j
                P["v"].append(k)
            idx.append(j)
        # (o espelho do eixo inverte o sentido: anti-horário do Blender vira horário, a frente na Unity)
        P["i"].extend(idx)
    return parts


def wstr(f, s):
    b = s.encode("utf-8")
    f.write(struct.pack("<B", len(b)))
    f.write(b)


def main():
    a = [x for x in sys.argv[sys.argv.index("--") + 1:]] if "--" in sys.argv else sys.argv[1:]
    sheet = "--sheet" in a
    a = [x for x in a if not x.startswith("--")]
    src, texdir, out = a[0], a[1], a[2]
    os.makedirs(os.path.join(out, "Tex"), exist_ok=True)
    files = {os.path.basename(p)[:-4]: p for p in glob.glob(os.path.join(src, "**", "*.fbx"), recursive=True)}
    models = []
    for fn, name, target in NATURE:
        o = import_join(files[fn])
        decimate(o, target, False)
        models.append((name, extract(o, False)))
    for fn in sorted(k for k in files if k.endswith("_Mat")):
        o = import_join(files[fn])
        decimate(o, 0, True)
        models.append(("House_" + fn[:-4], extract(o, True)))
    used = set()
    total = 0
    with open(os.path.join(out, "props.bytes"), "wb") as f:
        f.write(b"CQP1")
        f.write(struct.pack("<i", len(PALETTE)))
        for p in PALETTE:
            wstr(f, p)
        f.write(struct.pack("<i", len(models)))
        for name, parts in models:
            allv = np.array([v[:3] for P in parts.values() for v in P["v"]])
            wstr(f, name)
            f.write(struct.pack("<6f", *allv.min(0), *allv.max(0)))
            f.write(struct.pack("<i", len(parts)))
            nt = 0
            for key, P in parts.items():
                used.add(key)
                assert len(P["v"]) < 65535, (name, key)
                wstr(f, key)
                f.write(struct.pack("<ii", len(P["v"]), len(P["i"])))
                for v in P["v"]:
                    nx, ny, nz = (max(-127, min(127, int(round(c * 127)))) for c in v[3:6])
                    f.write(struct.pack("<3f3b2e", v[0], v[1], v[2], nx, ny, nz, v[6], v[7]))
                f.write(struct.pack("<%dH" % len(P["i"]), *P["i"]))
                nt += len(P["i"]) // 3
            total += nt
            sz = allv.max(0) - allv.min(0)
            print("%-28s %6d tris  %5.1f x %5.1f x %5.1f m  %s" % (name, nt, sz[0], sz[1], sz[2], ",".join(parts)))
    print("total", total, "tris;", os.path.getsize(os.path.join(out, "props.bytes")) // 1024, "KB")
    from PIL import Image
    for key in sorted(used - {"pal"}):
        fn, size = TEX[key]
        im = Image.open(os.path.join(texdir, fn))
        im = im.convert("RGBA" if im.mode in ("RGBA", "LA", "P") else "RGB").resize((size, size), Image.LANCZOS)
        im.save(os.path.join(out, "Tex", key + ".png"), optimize=True)
    if sheet:
        make_sheet(models, out)


def make_sheet(models, out):
    """Folha de conferência: silhueta de frente (x, y) de cada modelo."""
    from PIL import Image, ImageDraw
    W, cols = 160, 8
    rows = (len(models) + cols - 1) // cols
    im = Image.new("RGB", (W * cols, W * rows), (235, 235, 230))
    d = ImageDraw.Draw(im)
    for k, (name, parts) in enumerate(models):
        ox, oy = (k % cols) * W, (k // cols) * W
        allv = np.array([v[:3] for P in parts.values() for v in P["v"]])
        s = (W - 24) / max(1e-3, (allv.max(0) - allv.min(0))[[0, 1]].max())
        cx = (allv[:, 0].min() + allv[:, 0].max()) / 2
        for key, P in parts.items():
            col = (60, 120, 50) if "Lea" in key or "Grass" in key or key == "Leaves" else (120, 90, 60) if "Bark" in key else (130, 130, 140)
            V = P["v"]
            for i in range(0, len(P["i"]), 3):
                tri = [(ox + W / 2 + (V[j][0] - cx) * s, oy + W - 14 - (V[j][1] - allv[:, 1].min()) * s) for j in P["i"][i:i + 3]]
                d.polygon(tri, outline=col)
        d.text((ox + 4, oy + 2), name, fill=(0, 0, 0))
    im.save(os.path.join(out, "props_sheet.png"))


if __name__ == "__main__":
    main()
