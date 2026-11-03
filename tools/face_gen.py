#!/usr/bin/env python3
"""Craquez - gerador procedural de faces 2D vetoriais (SVG).

Gera rostos de jogadores como SVG (vetorial = nítido em qualquer resolução,
de 128px no celular simples até 2048px+ em telas 4K). O jogo carrega esses
SVGs e rasteriza na resolução do preset de qualidade escolhido
(ver scripts/art/face_library.gd).

Uso:
    python3 tools/face_gen.py                # gera assets/faces/face_000..047.svg
    python3 tools/face_gen.py --count 96 --seed 7
    python3 tools/face_gen.py --sheet docs/img/faces_sheet.html
"""
from __future__ import annotations

import argparse
import json
import math
import random
from dataclasses import dataclass, asdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

SKIN_TONES = [
    ("#f6d7c3", "#d9a98e"), ("#efc3a4", "#c98f6c"), ("#e0ac86", "#b47a54"),
    ("#c98e62", "#9a6440"), ("#a8714a", "#7a4c2e"), ("#8a5634", "#5e3720"),
    ("#6b3f26", "#452616"), ("#4e2c1a", "#2f190e"),
]
HAIR_COLORS = ["#1a1210", "#2b1d16", "#4a2f1f", "#6e4a2c", "#a0703f", "#d9b26a", "#e8d3a0", "#8c2f1b", "#b9b9b9"]
EYE_COLORS = ["#3b2416", "#5a3a22", "#2f5d3a", "#3d6e9e", "#6f7d4a", "#20140d"]
HAIR_STYLES = ["buzz", "short", "fade", "curly", "afro", "long", "mohawk", "bald", "dreads", "fringe", "bun"]
BEARDS = ["none", "none", "stubble", "goatee", "full", "mustache", "chin"]
JERSEYS = [("#d62828", "#ffffff"), ("#1d4ed8", "#ffffff"), ("#facc15", "#14532d"), ("#16a34a", "#ffffff"),
           ("#111827", "#f97316"), ("#ffffff", "#1e3a8a"), ("#7c3aed", "#fde68a"), ("#ea580c", "#111827")]


@dataclass
class FaceParams:
    seed: int
    skin: int
    hair_color: int
    hair_style: str
    beard: str
    eye_color: int
    jaw: float          # 0 = fino, 1 = largo
    face_len: float     # 0 = curto, 1 = alongado
    eye_size: float
    eye_gap: float
    brow_tilt: float    # -1 bravo .. 1 suave
    nose_w: float
    mouth_w: float
    smile: float        # -1 sério .. 1 sorriso
    ear: float
    jersey: int
    headband: bool


def random_params(seed: int) -> FaceParams:
    r = random.Random(seed)
    skin = r.randrange(len(SKIN_TONES))
    hc = r.randrange(len(HAIR_COLORS) - 1) if r.random() > 0.06 else len(HAIR_COLORS) - 1
    if skin >= 4 and r.random() < 0.7:
        hc = r.choice([0, 1])
    style = r.choice(HAIR_STYLES)
    if style in ("afro", "dreads") and skin < 3 and r.random() < 0.6:
        style = r.choice(["short", "curly", "fade"])
    return FaceParams(
        seed=seed, skin=skin, hair_color=hc, hair_style=style, beard=r.choice(BEARDS),
        eye_color=r.randrange(len(EYE_COLORS)), jaw=r.random(), face_len=r.random(),
        eye_size=r.uniform(0.85, 1.15), eye_gap=r.uniform(0.9, 1.1), brow_tilt=r.uniform(-1, 1),
        nose_w=r.uniform(0.85, 1.25), mouth_w=r.uniform(0.85, 1.15), smile=r.uniform(-0.6, 1.0),
        ear=r.uniform(0.9, 1.1), jersey=r.randrange(len(JERSEYS)), headband=r.random() < 0.12,
    )


def _f(v: float) -> str:
    return f"{v:.1f}"


def _circle_cloud(r: random.Random, cx, cy, rx, ry, n, rad_min, rad_max, fill, min_y=None):
    out = []
    for _ in range(n):
        a = r.uniform(0, math.tau)
        d = math.sqrt(r.random())
        x = cx + math.cos(a) * rx * d
        y = cy + math.sin(a) * ry * d
        if min_y is not None and y > min_y:
            continue
        out.append(f'<circle cx="{_f(x)}" cy="{_f(y)}" r="{_f(r.uniform(rad_min, rad_max))}" fill="{fill}"/>')
    return "".join(out)


def build_svg(p: FaceParams, size: int = 512, card: bool = False) -> str:
    r = random.Random(p.seed * 7919)
    skin, skin_dk = SKIN_TONES[p.skin]
    hair = HAIR_COLORS[p.hair_color]
    eye = EYE_COLORS[p.eye_color]
    j1, j2 = JERSEYS[p.jersey]
    uid = f"f{p.seed}"

    cx = 256.0
    head_w = 150 + p.jaw * 22            # semi-largura nas têmporas
    jaw_w = 92 + p.jaw * 40              # semi-largura no queixo
    top = 92 - p.face_len * 10
    chin = 392 + p.face_len * 26
    eye_y = 250 + p.face_len * 4
    gap = 58 * p.eye_gap
    es = p.eye_size

    s = []
    s.append(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" width="{size}" height="{size}">')
    s.append("<defs>")
    s.append(f'<radialGradient id="{uid}sk" cx="45%" cy="38%" r="70%"><stop offset="0" stop-color="{skin}"/>'
             f'<stop offset="0.7" stop-color="{skin}"/><stop offset="1" stop-color="{skin_dk}"/></radialGradient>')
    s.append(f'<linearGradient id="{uid}nk" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="{skin_dk}"/>'
             f'<stop offset="0.45" stop-color="{skin}"/></linearGradient>')
    s.append(f'<radialGradient id="{uid}ir" cx="50%" cy="45%" r="55%"><stop offset="0" stop-color="{eye}" stop-opacity="0.75"/>'
             f'<stop offset="0.65" stop-color="{eye}"/><stop offset="1" stop-color="#0b0705"/></radialGradient>')
    s.append(f'<linearGradient id="{uid}hr" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="{hair}" stop-opacity="1"/>'
             f'<stop offset="1" stop-color="{hair}" stop-opacity="0.92"/></linearGradient>')
    s.append(f'<linearGradient id="{uid}js" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="{j1}"/>'
             f'<stop offset="1" stop-color="{j1}" stop-opacity="0.85"/></linearGradient>')
    s.append(f'<linearGradient id="{uid}lp" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="{skin_dk}"/>'
             f'<stop offset="1" stop-color="#9c4a3c" stop-opacity="0.85"/></linearGradient>')
    if card:
        s.append(f'<linearGradient id="{uid}bg" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#1f2937"/>'
                 f'<stop offset="1" stop-color="#0b1220"/></linearGradient>')
    s.append("</defs>")
    if card:
        s.append(f'<rect width="512" height="512" rx="36" fill="url(#{uid}bg)"/>')

    # ---- cabelo de trás (longo / coque / afro / dreads) ----
    if p.hair_style == "long":
        s.append(f'<path d="M{_f(cx-head_w-12)} {_f(top+90)} Q{_f(cx-head_w-30)} {_f(chin+10)} {_f(cx-head_w+20)} {_f(chin+60)} '
                 f'L{_f(cx+head_w-20)} {_f(chin+60)} Q{_f(cx+head_w+30)} {_f(chin+10)} {_f(cx+head_w+12)} {_f(top+90)} Z" fill="{hair}"/>')
    if p.hair_style == "afro":
        s.append(_circle_cloud(r, cx, top + 70, head_w + 40, 120, 120, 26, 44, hair))
    if p.hair_style == "dreads":
        for i in range(16):
            x = cx - head_w + 10 + i * (head_w * 2 - 20) / 15
            ln = r.uniform(140, 230)
            s.append(f'<path d="M{_f(x)} {_f(top+30)} q{_f(r.uniform(-14,14))} {_f(ln/2)} {_f(r.uniform(-10,10))} {_f(ln)}" '
                     f'stroke="{hair}" stroke-width="18" stroke-linecap="round" fill="none"/>')
    if p.hair_style == "bun":
        s.append(f'<circle cx="{_f(cx)}" cy="{_f(top-18)}" r="44" fill="{hair}"/>')

    # ---- pescoço e ombros com camisa ----
    s.append(f'<path d="M{_f(cx-62)} {_f(chin-60)} L{_f(cx-66)} {_f(chin+40)} L{_f(cx+66)} {_f(chin+40)} L{_f(cx+62)} {_f(chin-60)} Z" fill="url(#{uid}nk)"/>')
    s.append(f'<path d="M20 512 Q30 {_f(chin+40)} {_f(cx-70)} {_f(chin+28)} Q{_f(cx)} {_f(chin+70)} {_f(cx+70)} {_f(chin+28)} '
             f'Q482 {_f(chin+40)} 492 512 Z" fill="url(#{uid}js)"/>')
    s.append(f'<path d="M{_f(cx-72)} {_f(chin+28)} Q{_f(cx)} {_f(chin+84)} {_f(cx+72)} {_f(chin+28)}" stroke="{j2}" stroke-width="14" fill="none" stroke-linecap="round"/>')
    s.append(f'<path d="M60 512 L90 {_f(chin+70)}" stroke="{j2}" stroke-width="10" opacity="0.7"/>'
             f'<path d="M452 512 L422 {_f(chin+70)}" stroke="{j2}" stroke-width="10" opacity="0.7"/>')

    # ---- orelhas ----
    ear_r = 30 * p.ear
    for side in (-1, 1):
        ex = cx + side * (head_w - 6)
        s.append(f'<ellipse cx="{_f(ex)}" cy="{_f(eye_y+18)}" rx="{_f(ear_r*0.8)}" ry="{_f(ear_r*1.25)}" fill="{skin_dk}"/>')
        s.append(f'<ellipse cx="{_f(ex+side*2)}" cy="{_f(eye_y+18)}" rx="{_f(ear_r*0.45)}" ry="{_f(ear_r*0.8)}" fill="{skin}" opacity="0.6"/>')

    # ---- cabeça ----
    head = (f'M{_f(cx)} {_f(top)} C{_f(cx+head_w*0.9)} {_f(top)} {_f(cx+head_w)} {_f(top+70)} {_f(cx+head_w)} {_f(eye_y)} '
            f'C{_f(cx+head_w)} {_f(eye_y+80)} {_f(cx+jaw_w+10)} {_f(chin-50)} {_f(cx+jaw_w*0.55)} {_f(chin-12)} '
            f'Q{_f(cx)} {_f(chin+12)} {_f(cx-jaw_w*0.55)} {_f(chin-12)} '
            f'C{_f(cx-jaw_w-10)} {_f(chin-50)} {_f(cx-head_w)} {_f(eye_y+80)} {_f(cx-head_w)} {_f(eye_y)} '
            f'C{_f(cx-head_w)} {_f(top+70)} {_f(cx-head_w*0.9)} {_f(top)} {_f(cx)} {_f(top)} Z')
    s.append(f'<path d="{head}" fill="url(#{uid}sk)"/>')
    # sombreamento lateral e bochechas
    s.append(f'<path d="{head}" fill="none" stroke="{skin_dk}" stroke-width="3" opacity="0.5"/>')
    for side in (-1, 1):
        s.append(f'<ellipse cx="{_f(cx+side*(head_w*0.62))}" cy="{_f(eye_y+70)}" rx="34" ry="22" fill="#e07a6a" opacity="0.12"/>')

    # ---- barba ----
    bc = hair if p.hair_color != len(HAIR_COLORS) - 1 else "#8f8f8f"
    mouth_y = chin - 62 - p.face_len * 6
    if p.beard == "stubble":
        s.append(f'<path d="M{_f(cx-jaw_w-4)} {_f(eye_y+70)} C{_f(cx-jaw_w)} {_f(chin-20)} {_f(cx-30)} {_f(chin+8)} {_f(cx)} {_f(chin+8)} '
                 f'C{_f(cx+30)} {_f(chin+8)} {_f(cx+jaw_w)} {_f(chin-20)} {_f(cx+jaw_w+4)} {_f(eye_y+70)} '
                 f'Q{_f(cx)} {_f(mouth_y-40)} {_f(cx-jaw_w-4)} {_f(eye_y+70)} Z" fill="{bc}" opacity="0.22"/>')
    elif p.beard == "full":
        s.append(f'<path d="M{_f(cx-head_w+6)} {_f(eye_y+40)} C{_f(cx-jaw_w-14)} {_f(chin)} {_f(cx-40)} {_f(chin+30)} {_f(cx)} {_f(chin+30)} '
                 f'C{_f(cx+40)} {_f(chin+30)} {_f(cx+jaw_w+14)} {_f(chin)} {_f(cx+head_w-6)} {_f(eye_y+40)} '
                 f'L{_f(cx+jaw_w-6)} {_f(eye_y+64)} Q{_f(cx+40)} {_f(mouth_y-34)} {_f(cx)} {_f(mouth_y-30)} '
                 f'Q{_f(cx-40)} {_f(mouth_y-34)} {_f(cx-jaw_w+6)} {_f(eye_y+64)} Z" fill="{bc}" opacity="0.95"/>')
        s.append(f'<ellipse cx="{_f(cx)}" cy="{_f(mouth_y+4)}" rx="{_f(38*p.mouth_w)}" ry="18" fill="url(#{uid}sk)"/>')
    elif p.beard == "goatee":
        s.append(f'<path d="M{_f(cx-34)} {_f(mouth_y+16)} Q{_f(cx)} {_f(chin+24)} {_f(cx+34)} {_f(mouth_y+16)} Q{_f(cx)} {_f(mouth_y+30)} {_f(cx-34)} {_f(mouth_y+16)} Z" fill="{bc}"/>')
    elif p.beard == "chin":
        s.append(f'<path d="M{_f(cx-jaw_w*0.6)} {_f(chin-26)} Q{_f(cx)} {_f(chin+22)} {_f(cx+jaw_w*0.6)} {_f(chin-26)} Q{_f(cx)} {_f(chin-4)} {_f(cx-jaw_w*0.6)} {_f(chin-26)} Z" fill="{bc}" opacity="0.9"/>')
    if p.beard in ("mustache", "full", "goatee"):
        s.append(f'<path d="M{_f(cx-44*p.mouth_w)} {_f(mouth_y-4)} Q{_f(cx)} {_f(mouth_y-30)} {_f(cx+44*p.mouth_w)} {_f(mouth_y-4)} '
                 f'Q{_f(cx)} {_f(mouth_y-14)} {_f(cx-44*p.mouth_w)} {_f(mouth_y-4)} Z" fill="{bc}"/>')

    # ---- olhos ----
    for side in (-1, 1):
        ex = cx + side * gap
        w, h = 30 * es, 15 * es
        s.append(f'<path d="M{_f(ex-w)} {_f(eye_y)} Q{_f(ex)} {_f(eye_y-h*1.6)} {_f(ex+w)} {_f(eye_y)} Q{_f(ex)} {_f(eye_y+h*1.2)} {_f(ex-w)} {_f(eye_y)} Z" fill="#fbf7f2"/>')
        s.append(f'<clipPath id="{uid}e{side}"><path d="M{_f(ex-w)} {_f(eye_y)} Q{_f(ex)} {_f(eye_y-h*1.6)} {_f(ex+w)} {_f(eye_y)} Q{_f(ex)} {_f(eye_y+h*1.2)} {_f(ex-w)} {_f(eye_y)} Z"/></clipPath>')
        s.append(f'<g clip-path="url(#{uid}e{side})"><circle cx="{_f(ex)}" cy="{_f(eye_y-1)}" r="{_f(14*es)}" fill="url(#{uid}ir)"/>'
                 f'<circle cx="{_f(ex)}" cy="{_f(eye_y-1)}" r="{_f(6*es)}" fill="#050302"/>'
                 f'<rect x="{_f(ex-w)}" y="{_f(eye_y-h*1.6)}" width="{_f(w*2)}" height="{_f(h*0.55)}" fill="{skin_dk}" opacity="0.35"/></g>')
        s.append(f'<circle cx="{_f(ex+4*es)}" cy="{_f(eye_y-6*es)}" r="{_f(3.6*es)}" fill="#fff" opacity="0.95"/>')
        s.append(f'<path d="M{_f(ex-w-2)} {_f(eye_y+1)} Q{_f(ex)} {_f(eye_y-h*1.75)} {_f(ex+w+2)} {_f(eye_y+1)}" stroke="#1b1210" stroke-width="4" fill="none" stroke-linecap="round"/>')
        s.append(f'<path d="M{_f(ex-w*0.7)} {_f(eye_y+h*0.95)} Q{_f(ex)} {_f(eye_y+h*1.4)} {_f(ex+w*0.7)} {_f(eye_y+h*0.95)}" stroke="{skin_dk}" stroke-width="2" fill="none" opacity="0.6"/>')
        # sobrancelha
        by = eye_y - 34 * es
        inner = by + 6 - p.brow_tilt * 8
        outer = by + p.brow_tilt * 4
        x_in, x_out = ex - side * w * 0.9, ex + side * w * 1.15
        s.append(f'<path d="M{_f(x_in)} {_f(inner)} Q{_f(ex)} {_f(by-12)} {_f(x_out)} {_f(outer)} Q{_f(ex)} {_f(by-2)} {_f(x_in)} {_f(inner+9)} Z" fill="{bc}"/>')

    # ---- nariz ----
    nw = 22 * p.nose_w
    ny = eye_y + 74
    s.append(f'<path d="M{_f(cx-6)} {_f(eye_y+10)} Q{_f(cx-10)} {_f(ny-24)} {_f(cx-nw)} {_f(ny)} Q{_f(cx)} {_f(ny+14)} {_f(cx+nw)} {_f(ny)}" '
             f'stroke="{skin_dk}" stroke-width="4" fill="none" stroke-linecap="round" opacity="0.8"/>')
    s.append(f'<ellipse cx="{_f(cx-nw*0.5)}" cy="{_f(ny+2)}" rx="6" ry="4" fill="{skin_dk}" opacity="0.8"/>'
             f'<ellipse cx="{_f(cx+nw*0.5)}" cy="{_f(ny+2)}" rx="6" ry="4" fill="{skin_dk}" opacity="0.8"/>')
    s.append(f'<ellipse cx="{_f(cx+3)}" cy="{_f(ny-14)}" rx="6" ry="10" fill="#fff" opacity="0.12"/>')

    # ---- boca ----
    mw = 40 * p.mouth_w
    sm = p.smile * 12
    s.append(f'<path d="M{_f(cx-mw)} {_f(mouth_y-sm*0.3)} Q{_f(cx)} {_f(mouth_y-10)} {_f(cx+mw)} {_f(mouth_y-sm*0.3)} '
             f'Q{_f(cx)} {_f(mouth_y+16+sm)} {_f(cx-mw)} {_f(mouth_y-sm*0.3)} Z" fill="url(#{uid}lp)"/>')
    s.append(f'<path d="M{_f(cx-mw)} {_f(mouth_y-sm*0.3)} Q{_f(cx)} {_f(mouth_y+2+sm*0.6)} {_f(cx+mw)} {_f(mouth_y-sm*0.3)}" stroke="#4a1f18" stroke-width="3.5" fill="none" stroke-linecap="round"/>')
    if p.smile > 0.55:
        s.append(f'<path d="M{_f(cx-mw*0.75)} {_f(mouth_y+1)} Q{_f(cx)} {_f(mouth_y+6+sm*0.6)} {_f(cx+mw*0.75)} {_f(mouth_y+1)} Q{_f(cx)} {_f(mouth_y+3)} {_f(cx-mw*0.75)} {_f(mouth_y+1)} Z" fill="#fff" opacity="0.9"/>')

    # ---- cabelo da frente ----
    hs = p.hair_style
    cap = (f'M{_f(cx-head_w-4)} {_f(eye_y-30)} C{_f(cx-head_w-8)} {_f(top-82)} {_f(cx+head_w+8)} {_f(top-82)} {_f(cx+head_w+4)} {_f(eye_y-30)} ')
    if hs == "buzz":
        s.append(f'<path d="{cap}Q{_f(cx+head_w*0.8)} {_f(top+40)} {_f(cx)} {_f(top+36)} Q{_f(cx-head_w*0.8)} {_f(top+40)} {_f(cx-head_w-4)} {_f(eye_y-30)} Z" fill="{hair}" opacity="0.55"/>')
    elif hs == "fade":
        s.append(f'<path d="{cap}Q{_f(cx+head_w*0.8)} {_f(top+40)} {_f(cx)} {_f(top+36)} Q{_f(cx-head_w*0.8)} {_f(top+40)} {_f(cx-head_w-4)} {_f(eye_y-30)} Z" fill="{hair}" opacity="0.35"/>')
        s.append(f'<path d="M{_f(cx-head_w*0.8)} {_f(top+50)} C{_f(cx-head_w*0.8)} {_f(top-46)} {_f(cx+head_w*0.8)} {_f(top-46)} {_f(cx+head_w*0.8)} {_f(top+50)} '
                 f'Q{_f(cx)} {_f(top+20)} {_f(cx-head_w*0.8)} {_f(top+50)} Z" fill="url(#{uid}hr)"/>')
    elif hs in ("short", "long", "fringe", "bun"):
        fr = 84 if hs == "fringe" else 52
        s.append(f'<path d="{cap}C{_f(cx+head_w*0.7)} {_f(top+fr)} {_f(cx+30)} {_f(top+fr-14)} {_f(cx-10)} {_f(top+fr+8)} '
                 f'C{_f(cx-60)} {_f(top+fr-6)} {_f(cx-head_w*0.8)} {_f(top+fr+10)} {_f(cx-head_w-4)} {_f(eye_y-30)} Z" fill="url(#{uid}hr)"/>')
        for i in range(7):
            x = cx - head_w * 0.7 + i * head_w * 0.23
            s.append(f'<path d="M{_f(x)} {_f(top+6)} q{_f(r.uniform(-20,20))} 20 {_f(r.uniform(-8,8))} 42" stroke="#000" stroke-opacity="0.14" stroke-width="3" fill="none"/>')
    elif hs == "curly":
        s.append(f'<path d="{cap}Q{_f(cx)} {_f(top+50)} {_f(cx-head_w-4)} {_f(eye_y-30)} Z" fill="{hair}"/>')
        s.append(_circle_cloud(r, cx, top + 10, head_w + 6, 50, 70, 14, 22, hair))
        s.append(_circle_cloud(r, cx, top + 10, head_w, 46, 30, 4, 7, "#000", None).replace('fill="#000"', 'fill="#000" opacity="0.15"'))
    elif hs == "afro":
        s.append(f'<path d="{cap}Q{_f(cx)} {_f(top+40)} {_f(cx-head_w-4)} {_f(eye_y-30)} Z" fill="{hair}"/>')
        s.append(_circle_cloud(r, cx, top + 6, head_w + 10, 44, 50, 20, 30, hair))
    elif hs == "mohawk":
        s.append(f'<path d="{cap}Q{_f(cx)} {_f(top+40)} {_f(cx-head_w-4)} {_f(eye_y-30)} Z" fill="{hair}" opacity="0.3"/>')
        s.append(f'<path d="M{_f(cx-34)} {_f(top+40)} C{_f(cx-40)} {_f(top-60)} {_f(cx+40)} {_f(top-60)} {_f(cx+34)} {_f(top+40)} Q{_f(cx)} {_f(top+26)} {_f(cx-34)} {_f(top+40)} Z" fill="url(#{uid}hr)"/>')
    elif hs == "dreads":
        s.append(f'<path d="{cap}Q{_f(cx)} {_f(top+36)} {_f(cx-head_w-4)} {_f(eye_y-30)} Z" fill="{hair}"/>')
    elif hs == "bald":
        s.append(f'<ellipse cx="{_f(cx-30)}" cy="{_f(top+40)}" rx="54" ry="26" fill="#fff" opacity="0.18"/>')

    if p.headband:
        s.append(f'<path d="M{_f(cx-head_w-2)} {_f(top+64)} Q{_f(cx)} {_f(top+22)} {_f(cx+head_w+2)} {_f(top+64)} L{_f(cx+head_w+2)} {_f(top+88)} '
                 f'Q{_f(cx)} {_f(top+46)} {_f(cx-head_w-2)} {_f(top+88)} Z" fill="{j2}"/>')

    s.append("</svg>")
    return "".join(s)


def write_library(count: int, seed: int, out_dir: Path) -> list[FaceParams]:
    out_dir.mkdir(parents=True, exist_ok=True)
    faces = []
    for i in range(count):
        p = random_params(seed * 100003 + i)
        (out_dir / f"face_{i:03d}.svg").write_text(build_svg(p), encoding="utf-8")
        # o jogo lê o SVG cru e rasteriza no tamanho do preset: não importar como textura
        (out_dir / f"face_{i:03d}.svg.import").write_text('[remap]\n\nimporter="skip"\n', encoding="utf-8")
        faces.append(p)
    meta = []
    for f in faces:
        d = asdict(f)
        # cores resolvidas: o jogo usa para desenhar a cabeça do jogador em campo igual ao rosto
        d["skin_hex"] = SKIN_TONES[f.skin][0]
        d["skin_dark_hex"] = SKIN_TONES[f.skin][1]
        d["hair_hex"] = HAIR_COLORS[f.hair_color]
        meta.append(d)
    (out_dir / "faces.json").write_text(json.dumps(meta, indent=1), encoding="utf-8")
    return faces


def write_sheet(path: Path, faces: list[FaceParams], cols: int = 6, n: int = 18) -> None:
    cells = "".join(f'<div class="c">{build_svg(f, 240, card=True)}<span>#{i:03d} · {f.hair_style}</span></div>'
                    for i, f in enumerate(faces[:n]))
    zoom = build_svg(faces[0], 760, card=True)
    html = f"""<!doctype html><html><head><meta charset="utf-8"><style>
body{{margin:0;background:#0a0f1a;font-family:system-ui,sans-serif;color:#e5e7eb;padding:28px}}
h1{{margin:0 0 4px;font-size:30px;letter-spacing:1px}} p{{margin:0 0 20px;color:#94a3b8}}
.wrap{{display:flex;gap:24px;align-items:flex-start}}
.g{{display:grid;grid-template-columns:repeat({cols},240px);gap:14px}}
.c{{display:flex;flex-direction:column;align-items:center;gap:4px;font-size:13px;color:#94a3b8}}
.z{{display:flex;flex-direction:column;gap:6px;font-size:13px;color:#94a3b8}}
</style></head><body><h1>CRAQUEZ · faces 2D vetoriais</h1>
<p>Geradas proceduralmente (tools/face_gen.py) · SVG nítido em qualquer resolução · à direita: mesma face em 760px</p>
<div class="wrap"><div class="g">{cells}</div><div class="z">{zoom}<span>#000 ampliada (UHD)</span></div></div></body></html>"""
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(html, encoding="utf-8")


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--count", type=int, default=48)
    ap.add_argument("--seed", type=int, default=2026)
    ap.add_argument("--out", type=Path, default=ROOT / "assets" / "faces")
    ap.add_argument("--sheet", type=Path, default=None)
    a = ap.parse_args()
    faces = write_library(a.count, a.seed, a.out)
    print(f"{len(faces)} faces -> {a.out}")
    if a.sheet:
        write_sheet(a.sheet, faces)
        print(f"sheet -> {a.sheet}")


if __name__ == "__main__":
    main()
