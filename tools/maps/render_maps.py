# Gera os mapas de Resources/UI/Mapas a partir do Natural Earth (domínio público):
#   python3 render_maps.py ne_50m_admin_0_countries.geojson saida/
import json, math, sys, os
from PIL import Image, ImageDraw, ImageFilter
src, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)
d = json.load(open(src))
CONT = {  # slug, title, bbox lon0 lon1 lat0 lat1
 'america-do-sul': ('América do Sul', -84, -32, -57, 14),
 'europa': ('Europa', -26, 46, 33, 71.5),
 'africa': ('África', -20, 54, -36, 38),
 'america-do-norte': ('América do Norte e Central', -170, -50, 5, 75),
 'asia-oceania': ('Ásia e Oceania', 25, 180, -48, 62),
 'mundo': ('Todos', -170, 180, -56, 80),
}
SA = {'AR','BR','CL','CO','EC','PE','PY','UY','VE','BO','GY','SR','GF','FK'}
NA_EXTRA = set()
def cont_of(p):
    iso = p.get('ISO_A2_EH') or p.get('ISO_A2'); c = p['CONTINENT']
    if c == 'South America': return 'america-do-sul'
    if c == 'North America': return 'america-do-norte'
    if c == 'Africa': return 'africa'
    if c in ('Asia', 'Oceania'):
        if iso in ('TR', 'CY', 'GE', 'AM', 'AZ', 'IL'): return 'europa'
        return 'asia-oceania'
    if c == 'Europe': return 'europa'
    return None
W, H, S = 640, 400, 3
LAND, LAND_C, HL, BORDER = (40, 54, 92, 255), (52, 116, 214, 255), (240, 196, 40, 255), (12, 20, 42, 255)
def polys(g):
    if g['type'] == 'Polygon': return [g['coordinates']]
    return g['coordinates']
def render(slug, hl_iso=None):
    _, lon0, lon1, lat0, lat1 = CONT[slug]
    latc = math.radians((lat0 + lat1) / 2)
    kx = math.cos(latc) if slug != 'mundo' else 0.8
    sx, sy = (lon1 - lon0) * kx, (lat1 - lat0)
    sc = min((W * 0.94) / sx, (H * 0.94) / sy)
    ox = W / 2 - ((lon0 + lon1) / 2) * kx * sc
    oy = H / 2 + ((lat0 + lat1) / 2) * sc
    im = Image.new('RGBA', (W * S, H * S), (0, 0, 0, 0))
    dr = ImageDraw.Draw(im)
    for f in d['features']:
        p = f['properties']; iso = p.get('ISO_A2_EH') or p.get('ISO_A2')
        if p['CONTINENT'] == 'Antarctica': continue
        mine = cont_of(p) == slug or slug == 'mundo'
        col = HL if hl_iso and iso == hl_iso else LAND_C if mine else LAND
        for poly in polys(f['geometry']):
            ring = poly[0]
            pts = []
            shift = 360 if slug == 'asia-oceania' and sum(q[0] for q in ring) / len(ring) < -100 else 0
            for lon, lat in ring:
                lon += shift
                pts.append(((ox + lon * kx * sc) * S, (oy - lat * sc) * S))
            if len(pts) > 2: dr.polygon(pts, fill=col, outline=BORDER, width=max(1, S))
    im = im.resize((W, H), Image.LANCZOS)
    return im
for slug in CONT:
    render(slug).quantize(32, method=Image.FASTOCTREE).save(f'{out}/{slug}.png', optimize=True)
CL = {'AR':'america-do-sul','BR':'america-do-sul','CL':'america-do-sul','EC':'america-do-sul','PE':'america-do-sul','UY':'america-do-sul','CO':'america-do-sul','PY':'america-do-sul','BO':'america-do-sul','VE':'america-do-sul',
      'GB':'europa','ES':'europa','FR':'europa','IT':'europa','NL':'europa','PT':'europa','RU':'europa','UA':'europa','DE':'europa','BE':'europa','TR':'europa',
      'SA':'asia-oceania','JP':'asia-oceania','KR':'asia-oceania','CN':'asia-oceania','US':'america-do-norte','MX':'america-do-norte'}
for iso, slug in CL.items():
    render(slug, iso).quantize(32, method=Image.FASTOCTREE).save(f'{out}/pais_{iso}.png', optimize=True)
print('ok')
