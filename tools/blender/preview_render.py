"""Renderização de pré-visualização (Cycles CPU) para conferir atletas e animações sem o Unity."""
import math

import bpy
from mathutils import Vector


def _setup(res=(720, 720), samples=24):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = samples
    sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.film_transparent = False
    if sc.world is None:
        sc.world = bpy.data.worlds.new("W")
    sc.world.use_nodes = True
    bg = sc.world.node_tree.nodes.get("Background")
    bg.inputs[0].default_value = (0.55, 0.62, 0.72, 1)
    bg.inputs[1].default_value = 0.6
    return sc


def _light(name, loc, energy, size=2.0):
    ld = bpy.data.lights.new(name, "AREA")
    ld.energy = energy
    ld.size = size
    o = bpy.data.objects.new(name, ld)
    bpy.context.scene.collection.objects.link(o)
    o.location = loc
    d = Vector((0, 0, 1.0)) - Vector(loc)
    o.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    return o


def _cam(loc, target, lens=55):
    cd = bpy.data.cameras.new("Cam")
    cd.lens = lens
    o = bpy.data.objects.new("Cam", cd)
    bpy.context.scene.collection.objects.link(o)
    o.location = loc
    o.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = o
    return o


def kit_colors(shirt=(0.75, 0.05, 0.08), shorts=(0.95, 0.95, 0.95), socks=(0.75, 0.05, 0.08), boots=(0.02, 0.02, 0.02)):
    for n, c in [("Shirt", shirt), ("Shorts", shorts), ("Socks", socks), ("Boots", boots)]:
        m = bpy.data.materials.get(n)
        if m is None:
            continue
        m.use_nodes = True
        nt = m.node_tree
        p = nt.nodes.get("Principled BSDF") or nt.nodes.new("ShaderNodeBsdfPrincipled")
        p.inputs["Base Color"].default_value = (*c, 1)
        p.inputs["Roughness"].default_value = 0.55 if n != "Boots" else 0.3
        out = nt.nodes.get("Material Output") or nt.nodes.new("ShaderNodeOutputMaterial")
        nt.links.new(p.outputs[0], out.inputs[0])


def show_hair(rig, keep):
    for c in rig.children_recursive:
        if c.name.startswith("Hair_"):
            c.hide_render = not c.name.startswith(keep)


def render_character(rig, path, hair="Hair_short02"):
    sc = _setup()
    kit_colors()
    show_hair(rig, hair)
    _light("Key", (2.5, -3.0, 3.0), 600)
    _light("Fill", (-3.0, -2.0, 2.0), 250)
    _light("Rim", (0.0, 3.0, 3.0), 400)
    shots = [((0, -4.6, 1.0), (0, 0, 0.92), 60), ((2.6, -3.6, 1.3), (0, 0, 0.92), 62), ((0.35, -0.9, 1.7), (0, 0, 1.66), 60)]
    import os
    base, ext = os.path.splitext(path)
    for i, (loc, tgt, lens) in enumerate(shots):
        _cam(loc, tgt, lens)
        sc.render.filepath = f"{base}_{i}{ext}"
        bpy.ops.render.render(write_still=True)


def render_frames(rig, path_pattern, frames, cam_loc=(3.2, -3.2, 1.4), target=(0, 0, 0.9), res=(480, 480)):
    sc = _setup(res=res, samples=12)
    _light("Key", (2.5, -3.0, 3.0), 600)
    _light("Fill", (-3.0, -2.0, 2.0), 250)
    _cam(cam_loc, target, 40)
    for f in frames:
        sc.frame_set(f)
        sc.render.filepath = path_pattern % f
        bpy.ops.render.render(write_still=True)
