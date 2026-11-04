"""Leitor ASF/AMC (captura de movimento CMU) com cinemática direta em numpy.

Dados: CMU Graphics Lab Motion Capture Database (mocap.cs.cmu.edu), livre para
uso, inclusive em produtos comerciais (não pode ser revendido como dado bruto).

Convenções ASF: para cada osso, C = rotação do "axis" (graus, ordem XYZ);
movimento local M = produto das rotações dos DOFs na ordem listada;
L = C · M · C⁻¹ ;  G_filho = G_pai · L_filho ; na pose de repouso G = I.
A saída é convertida para o espaço do Blender (Z para cima, frente = -Y,
esquerda do personagem = +X).
"""
import math

import numpy as np

# CMU (X esquerda, Y cima, Z frente) -> Blender (X esquerda do personagem, Z cima, frente -Y)
P = np.array([[1, 0, 0], [0, 0, -1], [0, 1, 0]], dtype=float)
UNIT = 0.0254 / 0.45   # comprimentos ASF -> metros


def rx(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])


def ry(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])


def rz(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])




def euler_xyz(deg):
    x, y, z = (math.radians(d) for d in deg)
    return rz(z) @ ry(y) @ rx(x)


class Bone:
    def __init__(self):
        self.name = ""
        self.direction = np.zeros(3)
        self.length = 0.0
        self.axis = np.eye(3)
        self.dof = []
        self.children = []
        self.parent = None


class Skeleton:
    def __init__(self, path):
        self.bones = {}
        self.root_order = ["tx", "ty", "tz", "rx", "ry", "rz"]
        root = Bone()
        root.name = "root"
        self.bones["root"] = root
        lines = [l.rstrip() for l in open(path, encoding="latin1")]
        i = 0
        section = None
        cur = None
        while i < len(lines):
            l = lines[i].strip()
            i += 1
            if not l or l.startswith("#"):
                continue
            if l.startswith(":"):
                section = l.split()[0]
                if section == ":root":
                    pass
                continue
            tok = l.split()
            if section == ":root" and tok[0] == "order":
                self.root_order = [t.lower() for t in tok[1:]]
            elif section == ":bonedata":
                if tok[0] == "begin":
                    cur = Bone()
                elif tok[0] == "end":
                    self.bones[cur.name] = cur
                    cur = None
                elif tok[0] == "name":
                    cur.name = tok[1]
                elif tok[0] == "direction":
                    cur.direction = np.array([float(t) for t in tok[1:4]])
                elif tok[0] == "length":
                    cur.length = float(tok[1]) * UNIT
                elif tok[0] == "axis":
                    cur.axis = euler_xyz([float(t) for t in tok[1:4]])
                elif tok[0] == "dof":
                    cur.dof = [t.lower() for t in tok[1:]]
            elif section == ":hierarchy":
                if tok[0] in ("begin", "end"):
                    continue
                parent = tok[0]
                for c in tok[1:]:
                    self.bones[parent].children.append(c)
                    self.bones[c].parent = parent
        self.order = []
        stack = ["root"]
        while stack:
            b = stack.pop(0)
            self.order.append(b)
            stack[0:0] = self.bones[b].children

    def rest_dir(self, name):
        """Direção do osso na pose de repouso, já no espaço do Blender."""
        return P @ self.bones[name].direction


def read_amc(path):
    frames = []
    cur = None
    for l in open(path, encoding="latin1"):
        l = l.strip()
        if not l or l.startswith("#") or l.startswith(":"):
            continue
        tok = l.split()
        if tok[0].isdigit() and len(tok) == 1:
            cur = {}
            frames.append(cur)
            continue
        if cur is not None:
            cur[tok[0]] = [float(t) for t in tok[1:]]
    return frames


def fk(skel, frame):
    """Rotações globais (3x3, espaço Blender) e posições (cabeça de cada osso) de um quadro."""
    G = {}
    pos = {}
    tail = {}
    rv = frame["root"]
    vals = dict(zip(skel.root_order, rv))
    root_rot = euler_xyz([vals.get("rx", 0), vals.get("ry", 0), vals.get("rz", 0)])
    root_pos = np.array([vals.get("tx", 0), vals.get("ty", 0), vals.get("tz", 0)]) * UNIT
    G["root"] = root_rot
    pos["root"] = root_pos
    tail["root"] = root_pos
    for name in skel.order[1:]:
        b = skel.bones[name]
        ang = {"rx": 0.0, "ry": 0.0, "rz": 0.0}
        if name in frame:
            for d, v in zip(b.dof, frame[name]):
                ang[d] = v
        M = euler_xyz([ang["rx"], ang["ry"], ang["rz"]])
        L = b.axis @ M @ b.axis.T
        Gp = G[b.parent]
        G[name] = Gp @ L
        pos[name] = tail[b.parent]
        tail[name] = pos[name] + G[name] @ (b.direction * b.length)
    out_G = {k: P @ v @ P.T for k, v in G.items()}
    out_pos = {k: P @ v for k, v in pos.items()}
    out_tail = {k: P @ v for k, v in tail.items()}
    return out_G, out_pos, out_tail
