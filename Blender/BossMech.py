import bpy, bmesh, math, sys, os
import numpy as np
from mathutils import Vector, Matrix

R = 1.4
ZC = 1.87
RB = 0.965
STEP = 7.5
HEART_FLARE, SHUTTER_SLIDE, HATCH_OPEN = 30.0, 0.44, 100.0
FRONT_LEG_LON, REAR_LEG_LON = 58.0, 122.0
HOLE_LON, HOLE_LAT = (120.0, 157.5), (22.5, 60.0)
OUT_BLEND, OUT_PNG, TEX_DIR, OUT_FBX = sys.argv[sys.argv.index('--') + 1:][:4]
TEX = 2048

bpy.ops.wm.read_factory_settings(use_empty=True)

COLORS = {
    'Boss_Red': ((0.72, 0.05, 0.04), 0.0),
    'Boss_Dark': ((0.085, 0.075, 0.08), 0.0),
    'Boss_Yellow': ((1.0, 0.58, 0.03), 0.0),
    'Boss_Metal': ((0.42, 0.44, 0.47), 0.0),
    'Boss_GlowOrange': ((1.0, 0.40, 0.02), 5.0),
    'Boss_GlowYellow': ((1.0, 0.78, 0.10), 4.0),
    'Boss_GlowRed': ((1.0, 0.04, 0.02), 5.0),
    'Boss_Cabin': ((0.21, 0.18, 0.16), 0.0),
    'Boss_Seat': ((0.50, 0.13, 0.05), 0.0),
}
NAMES = list(COLORS)
RED, DRK, YEL, MET, GLO, GLY, GLR, CAB, SEAT = range(9)
X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))


def make_mat(name):
    m = bpy.data.materials.new(name)
    try:
        m.use_nodes = True
    except Exception:
        pass
    color, emission = COLORS[name]
    bsdf = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Roughness'].default_value = 0.55
    if emission:
        bsdf.inputs['Emission Color'].default_value = (*color, 1)
        bsdf.inputs['Emission Strength'].default_value = emission
    m.diffuse_color = (*color, 1)
    return m


MATS = [make_mat(n) for n in NAMES]


def T(v):
    return Matrix.Translation(Vector(v))


def RXd(a):
    return Matrix.Rotation(math.radians(a), 4, 'X')


def RYd(a):
    return Matrix.Rotation(math.radians(a), 4, 'Y')


def RZd(a):
    return Matrix.Rotation(math.radians(a), 4, 'Z')


def AY(p):
    return T(p) @ RXd(90)


def AX(p):
    return T(p) @ RYd(90)


def axis(M, i):
    return M.to_3x3().col[i].copy()


def frame(z, x_hint=None):
    z = Vector(z).normalized()
    x = None
    for h in (x_hint, X, Y, Z):
        if h is None:
            continue
        x = Vector(h) - Vector(h).dot(z) * z
        if x.length > 1e-4:
            break
    x.normalize()
    y = z.cross(x)
    return Matrix((x, y, z)).transposed().to_4x4()


def sdir(lon, lat):
    lo, la = math.radians(lon), math.radians(lat)
    return Vector((math.sin(lo) * math.cos(la), -math.cos(lo) * math.cos(la), math.sin(la)))


def set_mat(verts, mi):
    for f in {f for v in verts for f in v.link_faces}:
        f.material_index = mi


def poly(bm, pts, mi, hint):
    uniq = []
    for p in pts:
        if not any((p - q).length < 1e-6 for q in uniq):
            uniq.append(p)
    if len(uniq) < 3:
        return
    f = bm.faces.new([bm.verts.new(p) for p in uniq])
    f.normal_update()
    if f.normal.dot(hint) < 0:
        f.normal_flip()
    f.material_index = mi


def box(bm, M, size, mi):
    r = bmesh.ops.create_cube(bm, size=1.0, matrix=M @ Matrix.Diagonal(Vector((*size, 1))))
    set_mat(r['verts'], mi)


def cbox(bm, M, size, mi, ch=0.03, taper=(1.0, 1.0)):
    h = [s / 2 for s in size]
    ch = min(ch, min(h) * 0.6)
    centre = M @ Vector((0, 0, 0))

    def vert(s, a):
        p = [s[i] * (h[i] - (ch if i != a else 0)) for i in range(3)]
        t = (p[2] / h[2] + 1) / 2
        p[0] *= 1 + (taper[0] - 1) * t
        p[1] *= 1 + (taper[1] - 1) * t
        return M @ Vector(p)

    def face(pts):
        poly(bm, pts, mi, sum(pts, Vector()) / len(pts) - centre)

    for a in range(3):
        b, c = (a + 1) % 3, (a + 2) % 3
        for sa in (-1, 1):
            pts = []
            for sb, sc in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                s = [0, 0, 0]
                s[a], s[b], s[c] = sa, sb, sc
                pts.append(vert(s, a))
            face(pts)
    for a, b, c in ((0, 1, 2), (1, 2, 0), (2, 0, 1)):
        for sa in (-1, 1):
            for sb in (-1, 1):
                lo, hi = [0, 0, 0], [0, 0, 0]
                lo[a], lo[b], lo[c] = sa, sb, -1
                hi[a], hi[b], hi[c] = sa, sb, 1
                face([vert(lo, a), vert(hi, a), vert(hi, b), vert(lo, b)])
    for sx in (-1, 1):
        for sy in (-1, 1):
            for sz in (-1, 1):
                s = [sx, sy, sz]
                face([vert(s, 0), vert(s, 1), vert(s, 2)])


def bar(bm, p0, p1, w, t, mi, hint=X):
    p0, p1 = Vector(p0), Vector(p1)
    box(bm, T((p0 + p1) / 2) @ frame(p1 - p0, hint), (w, t, (p1 - p0).length), mi)


def cyl(bm, M, r, depth, segs, mi, r2=None):
    res = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r,
                                radius2=r if r2 is None else r2, depth=depth, matrix=M)
    set_mat(res['verts'], mi)


def tube(bm, p0, p1, r, segs, mi):
    p0, p1 = Vector(p0), Vector(p1)
    cyl(bm, T((p0 + p1) / 2) @ frame(p1 - p0), r, (p1 - p0).length, segs, mi)


def lathe(bm, M, profile, mats, segs, a0=0.0, a1=360.0):
    rot = M.to_3x3()
    for s in range(len(profile) - 1):
        (r0, z0), (r1, z1) = profile[s], profile[s + 1]
        nr, nz = (z1 - z0), -(r1 - r0)
        mi = mats if isinstance(mats, int) else mats[s]
        for k in range(segs):
            b0 = math.radians(a0 + (a1 - a0) * k / segs)
            b1 = math.radians(a0 + (a1 - a0) * (k + 1) / segs)
            bmid = (b0 + b1) / 2
            pts = [M @ Vector((r * math.cos(a), r * math.sin(a), z))
                   for r, z, a in ((r0, z0, b0), (r0, z0, b1), (r1, z1, b1), (r1, z1, b0))]
            poly(bm, pts, mi, rot @ Vector((nr * math.cos(bmid), nr * math.sin(bmid), nz)))


def sector(bm, M, r0, r1, a0, a1, z0, z1, mi, segs):
    lathe(bm, M, [(r0, z0), (r1, z0), (r1, z1), (r0, z1), (r0, z0)], mi, segs, a0, a1)
    rot = M.to_3x3()
    for a, s in ((a0, -1), (a1, 1)):
        c, sn = math.cos(math.radians(a)), math.sin(math.radians(a))
        pts = [M @ Vector((r * c, r * sn, z)) for r, z in ((r0, z0), (r1, z0), (r1, z1), (r0, z1))]
        poly(bm, pts, mi, rot @ Vector((-sn * s, c * s, 0)))


def border(nu, nv):
    return ([(i, 0) for i in range(nu)] + [(nu, j) for j in range(nv)]
            + [(i, nv) for i in range(nu, 0, -1)] + [(0, j) for j in range(nv, 0, -1)])


def shell(bm, lon0, lon1, lat0, lat1, r0, r1, mi, b=1.3):
    nu = max(1, round((lon1 - lon0) / STEP))
    nv = max(1, round((lat1 - lat0) / STEP))
    bs = b / math.cos(math.radians((lat0 + lat1) / 2))

    def grid(l0, l1, a0, a1, r):
        return {(i, j): sdir(l0 + (l1 - l0) * i / nu, a0 + (a1 - a0) * j / nv) * r
                for i in range(nu + 1) for j in range(nv + 1)}

    top = grid(lon0 + bs, lon1 - bs, lat0 + b, lat1 - b, r1)
    base = grid(lon0, lon1, lat0, lat1, r0)
    centre = sdir((lon0 + lon1) / 2, (lat0 + lat1) / 2) * r1
    for i in range(nu):
        for j in range(nv):
            pts = [top[i, j], top[i + 1, j], top[i + 1, j + 1], top[i, j + 1]]
            poly(bm, pts, mi, sum(pts, Vector()))
    ring = border(nu, nv)
    for k in range(len(ring)):
        a, c = ring[k], ring[(k + 1) % len(ring)]
        poly(bm, [base[a], base[c], top[c], top[a]], mi, (top[a] + top[c]) / 2 - centre)


def both(fn, lon0, lon1, *args):
    fn(lon0, lon1, *args)
    fn(-lon1, -lon0, *args)


def in_hole(lon, lat):
    return HOLE_LON[0] < abs(lon) < HOLE_LON[1] and HOLE_LAT[0] < lat < HOLE_LAT[1]


def base_sphere(bm):
    for i in range(48):
        l0 = -180 + STEP * i
        for j in range(24):
            a0 = -90 + STEP * j
            if in_hole(l0 + STEP / 2, a0 + STEP / 2):
                continue
            corners = ((l0, a0), (l0 + STEP, a0), (l0 + STEP, a0 + STEP), (l0, a0 + STEP))
            if all(math.degrees(math.acos(max(-1, min(1, -sdir(l, a).y)))) < 54 for l, a in corners):
                continue
            poly(bm, [sdir(l, a) * RB for l, a in corners], DRK, sdir(l0 + STEP / 2, a0 + STEP / 2))


def well(bm, l0, l1, a0, a1, depth):
    rf = RB - depth
    c = sdir((l0 + l1) / 2, (a0 + a1) / 2)
    nu, nv = round((l1 - l0) / STEP), round((a1 - a0) / STEP)

    def g(ij):
        return sdir(l0 + STEP * ij[0], a0 + STEP * ij[1])

    ring = border(nu, nv)
    for k in range(len(ring)):
        da, db = g(ring[k]), g(ring[(k + 1) % len(ring)])
        mid = (da + db) / 2 * RB
        poly(bm, [da * RB, db * RB, db * rf, da * rf], MET, c * mid.dot(c) - mid)
    for i in range(nu):
        for j in range(nv):
            pts = [g(ij) * rf for ij in ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))]
            poly(bm, pts, DRK, c)


scene = bpy.data.scenes[0] if bpy.data.scenes else bpy.data.scenes.new('Scene')
bpy.context.window.scene = scene
OBJ_W = {}
TRIS = [0]


def make_mesh(bm, name, W=None, mirror=False):
    W = W or Matrix.Identity(4)
    if mirror:
        bmesh.ops.scale(bm, vec=Vector((-1, 1, 1)), verts=bm.verts)
        bmesh.ops.reverse_faces(bm, faces=bm.faces)
    inv = W.inverted()
    for v in bm.verts:
        v.co = (inv @ v.co) * R
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    thr = math.radians(38)
    for f in bm.faces:
        f.smooth = True
    for e in bm.edges:
        fs = e.link_faces
        e.smooth = len(fs) == 2 and fs[0].material_index == fs[1].material_index and e.calc_face_angle(3.14) < thr
    used = sorted({f.material_index for f in bm.faces})
    for f in bm.faces:
        f.material_index = used.index(f.material_index)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for mi in used:
        mesh.materials.append(MATS[mi])
    return mesh


def add_obj(name, mesh, W, parent):
    Wm = W.copy()
    Wm.translation = W.translation * R + Vector((0, 0, ZC * R))
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    OBJ_W[name] = Wm
    obj.parent = parent
    obj.matrix_basis = OBJ_W[parent.name].inverted() @ Wm
    if mesh:
        TRIS[0] += sum(len(p.vertices) - 2 for p in mesh.polygons)
    return obj


root = bpy.data.objects.new('BossMech', None)
scene.collection.objects.link(root)
OBJ_W['BossMech'] = Matrix.Identity(4)

FF = frame(-Y, X)
LEGS = (('FR', FRONT_LEG_LON), ('FL', -FRONT_LEG_LON), ('RR', REAR_LEG_LON), ('RL', -REAR_LEG_LON))


def ML(lon):
    return RZd(lon - 90)


# ================= Body =================
bm = bmesh.new()
base_sphere(bm)

# cockpit housing + cabin shell
lathe(bm, FF, [(0.82, 0.42), (0.82, 0.74), (0.78, 0.78), (0.71, 0.78), (0.71, 0.62), (0.46, 0.62), (0.46, 0.79),
               (0.40, 0.79), (0.40, 0.50), (0.58, 0.50), (0.58, -0.20), (0, -0.20)],
      [DRK, DRK, DRK, DRK, DRK, YEL, YEL, CAB, CAB, CAB, CAB], 32)
lathe(bm, FF, [(0.82, 0.69), (0.85, 0.69), (0.85, 0.75), (0.82, 0.75)], YEL, 32)
for k in range(12):
    cyl(bm, FF @ RZd(30 * k + 15) @ T((0.745, 0, 0.787)), 0.024, 0.024, 6, MET)
for sy in (-1, 1):
    cbox(bm, FF @ T((0, sy * 0.585, 0.68)), (0.44, 0.17, 0.14), DRK, 0.02)
    for k in range(4):
        box(bm, FF @ T((-0.15 + 0.1 * k, sy * 0.585, 0.75)), (0.06, 0.11, 0.02), GLY)
for sx in (-1, 1):
    cbox(bm, FF @ T((sx * 0.585, 0, 0.66)), (0.15, 0.30, 0.10), MET, 0.02)
    for dy in (-0.09, 0.09):
        cyl(bm, FF @ T((sx * 0.585, dy, 0.715)), 0.028, 0.02, 6, DRK)

# cockpit interior
box(bm, FF @ T((0, -0.45, 0.16)), (0.80, 0.06, 0.70), MET)
for k in range(5):
    box(bm, FF @ T((0, -0.418, -0.08 + 0.12 * k)), (0.50, 0.012, 0.04), DRK)
cbox(bm, FF @ T((0, -0.33, 0.10)), (0.26, 0.20, 0.30), DRK, 0.03)
cbox(bm, FF @ T((0, -0.20, 0.13)), (0.42, 0.07, 0.44), DRK, 0.02)
cbox(bm, FF @ T((0, -0.15, 0.15)), (0.34, 0.07, 0.36), SEAT, 0.025)
SB = FF @ T((0, 0.12, -0.07)) @ RXd(-8)
cbox(bm, SB, (0.44, 0.62, 0.08), DRK, 0.03)
cbox(bm, SB @ T((0, -0.04, 0.05)), (0.32, 0.44, 0.07), SEAT, 0.025)
cbox(bm, SB @ T((0, 0.30, 0.05)), (0.22, 0.13, 0.08), SEAT, 0.025)
for sx in (-1, 1):
    cbox(bm, SB @ T((sx * 0.19, -0.04, 0.07)), (0.07, 0.46, 0.10), DRK, 0.02)
    cbox(bm, FF @ T((sx * 0.25, -0.08, 0.16)), (0.07, 0.05, 0.36), DRK, 0.015)
    tube(bm, FF @ Vector((sx * 0.25, -0.06, 0.30)), FF @ Vector((sx * 0.25, 0.04, 0.32)), 0.014, 6, MET)
    cyl(bm, FF @ T((sx * 0.25, 0.05, 0.32)) @ RXd(90), 0.024, 0.04, 6, GLR)
    cbox(bm, FF @ T((sx * 0.45, -0.14, 0.20)), (0.14, 0.30, 0.40), DRK, 0.02)
    for k in range(3):
        box(bm, FF @ T((sx * 0.376, -0.06, 0.07 + 0.13 * k)), (0.012, 0.10, 0.10), GLY)
    box(bm, FF @ T((sx * 0.23, 0.525, 0.18)), (0.12, 0.03, 0.34), GLY)
    box(bm, FF @ T((sx * 0.392, 0, 0.64)), (0.012, 0.22, 0.05), GLY)
cbox(bm, FF @ T((0, 0.10, -0.17)), (0.70, 0.60, 0.05), DRK, 0.02)
box(bm, FF @ T((0, 0.46, -0.14)), (0.30, 0.05, 0.02), GLO)

# armor
P0, P1 = 0.972, 1.03


def plate(l0, l1, a0, a1, mi=RED, r1=P1):
    shell(bm, l0, l1, a0, a1, P0 - 0.02, r1, mi)


both(plate, 57, 80, 4, 50)
both(plate, 100, 115, 4, 50)
both(plate, 70, 83, -58, -3)
both(plate, 97, 109, -58, -13)
both(plate, 135, 163, -58, -13)
both(plate, 110, 134, -64, -40)
both(plate, 117, 161, 3, 16)
both(plate, 87, 93, -58, -15)
both(plate, 36, 82, 53, 64)
both(plate, 98, 115, 53, 64)
def tier(l0, l1, a0, a1):
    shell(bm, l0, l1, a0, a1, P1 - 0.002, 1.052, RED, 1.0)


both(tier, 61, 77, 9, 45)
both(tier, 102, 113, 9, 45)
both(tier, 139, 160, -53, -18)
both(tier, 72, 81, -52, -9)
both(tier, 99, 107, -53, -18)
for lon in (59.5, 81.5, 101.5, 113.5):
    for lat in (9, 27, 45):
        for sg in (-1, 1):
            n = sdir(sg * lon, lat)
            cyl(bm, T(n * 1.033) @ frame(n), 0.016, 0.012, 6, MET)
both(plate, 97, 163, -9, -3, YEL, 1.045)
plate(167, 193, -9, -3, YEL, 1.045)
for lat in (2, 9, 16):
    n = sdir(180, lat)
    box(bm, T(n * 0.975) @ frame(n, X), (0.26, 0.05, 0.03), GLY)

# missile wells
MISSILE_W = {}
for side, sgn in (('R', 1), ('L', -1)):
    l0, l1 = (HOLE_LON[0], HOLE_LON[1]) if sgn > 0 else (-HOLE_LON[1], -HOLE_LON[0])
    well(bm, l0, l1, HOLE_LAT[0], HOLE_LAT[1], 0.33)
    lc, k = (l0 + l1) / 2, 0
    for dlat in (-12.5, 0.0, 12.5):
        for dlon in (-9.4, 9.4):
            n = sdir(lc + dlon, (HOLE_LAT[0] + HOLE_LAT[1]) / 2 + dlat)
            F = frame(n, Z)
            cyl(bm, T(n * 0.665) @ F, 0.058, 0.07, 8, DRK)
            cyl(bm, T(n * 0.705) @ F, 0.064, 0.012, 8, YEL)
            MISSILE_W[f'Missile_{side}_{k}'] = T(n * 0.64) @ F
            k += 1
    for lon in (lc - 12, lc + 12):
        n = sdir(lon, 19)
        cyl(bm, T(n * 1.0) @ frame(Vector((math.cos(math.radians(lon)), math.sin(math.radians(lon)), 0))),
            0.028, 0.11, 8, YEL)

# top cap, head, railing
lathe(bm, Matrix.Identity(4), [(0.43, 0.88), (0.43, 0.93), (0.36, 0.99), (0.2, 1.02), (0.2, 1.10), (0.16, 1.13),
                               (0, 1.13)], DRK, 16)
cbox(bm, T((0, -0.50, 0.98)), (0.70, 0.42, 0.30), DRK, 0.04, (0.88, 0.92))
cbox(bm, T((0, -0.54, 1.15)), (0.76, 0.46, 0.06), DRK, 0.02)
for s in (-1, 1):
    box(bm, T((s * 0.17, -0.70, 0.99)) @ RYd(-15 * s), (0.25, 0.05, 0.085), GLY)
    lons = [s * (40 + 26.25 * k) for k in range(5)]
    tops = [sdir(l, 0) * 0.60 + Z * 1.12 for l in lons]
    feet = [sdir(l, 0) * 0.43 + Z * 0.92 for l in lons]
    mids = [f.lerp(t, 0.55) for f, t in zip(feet, tops)]
    for k in range(5):
        tube(bm, feet[k], tops[k], 0.022, 6, YEL)
        if k < 4:
            tube(bm, tops[k], tops[k + 1], 0.022, 6, YEL)
            tube(bm, mids[k], mids[k + 1], 0.018, 6, YEL)

# belly
cbox(bm, T((0, 0, -1.0)), (0.60, 0.60, 0.26), DRK, 0.04)
for sy in (-1, 1):
    for k in (-1, 0, 1):
        box(bm, T((0.14 * k, sy * 0.30, -1.0)), (0.09, 0.03, 0.13), GLY)
for s in (-1, 1):
    pts = [Vector((s * x, 0, z)) for x, z in ((0.28, -1.0), (0.48, -1.07), (0.64, -0.98), (0.63, -0.78))]
    for k in range(3):
        tube(bm, pts[k], pts[k + 1], 0.04, 6, RED)
    # gun shoulders
    tube(bm, (s * 0.72, 0, 0.56), (s * 1.0, 0, 0.62), 0.15, 8, DRK)
    cyl(bm, AX((s * 0.86, 0, 0.59)), 0.18, 0.05, 8, YEL)

# hip sockets
for _, lon in LEGS:
    cbox(bm, ML(lon) @ T((1.0, 0, -0.44)), (0.40, 0.34, 0.34), DRK, 0.05)
    cbox(bm, ML(lon) @ T((0.90, 0, -0.44)), (0.22, 0.48, 0.48), DRK, 0.06)
    cbox(bm, ML(lon) @ T((0.93, 0, -0.20)), (0.20, 0.40, 0.10), RED, 0.03)

body = add_obj('Body', make_mesh(bm, 'Body'), Matrix.Identity(4), root)

# ================= Beacon =================
bm = bmesh.new()
lathe(bm, Matrix.Identity(4), [(0.12, 1.13), (0.12, 1.24), (0.09, 1.31), (0, 1.34)], GLR, 12)
W = T((0, 0, 1.13))
add_obj('Beacon', make_mesh(bm, 'Beacon', W), W, body)

# ================= Cockpit doors =================
bm = bmesh.new()
sector(bm, FF, 0.44, 0.88, 16, 74, 0.835, 0.895, RED, 6)
sector(bm, FF, 0.53, 0.80, 22, 68, 0.895, 0.915, RED, 6)
sector(bm, FF, 0.86, 0.93, 15, 75, 0.825, 0.91, YEL, 6)
sector(bm, FF, 0.44, 0.49, 15, 75, 0.83, 0.905, DRK, 6)
G = FF @ RZd(45)
cbox(bm, G @ T((0.56, 0, 0.92)), (0.24, 0.18, 0.04), MET, 0.012)
for dx, dy in ((-0.07, -0.05), (-0.07, 0.05), (0.07, -0.05), (0.07, 0.05)):
    cyl(bm, G @ T((0.56 + dx, dy, 0.945)), 0.02, 0.014, 6, DRK)
cyl(bm, G @ T((0.45, 0, 0.84)) @ RXd(90), 0.035, 0.30, 8, MET)
PETAL_LOCAL = T((0.45, 0, 0.835)) @ RZd(-90)
petal_mesh = make_mesh(bm, 'CockpitPetal', G @ PETAL_LOCAL)

bm = bmesh.new()
sector(bm, FF, 0.0, 0.42, 0.6, 89.4, 0.795, 0.82, MET, 6)
sector(bm, FF, 0.34, 0.42, 0.6, 89.4, 0.79, 0.826, YEL, 6)
sector(bm, FF, 0.10, 0.28, 12, 78, 0.815, 0.83, DRK, 4)
SHUTTER_LOCAL = T((0, 0, 0.80)) @ RZd(-90)
shutter_mesh = make_mesh(bm, 'CockpitShutter', G @ SHUTTER_LOCAL)

for k in range(4):
    Gk = FF @ RZd(45 + 90 * k)
    add_obj(f'CockpitPetal_{k}', petal_mesh, Gk @ PETAL_LOCAL @ RXd(HEART_FLARE), body)
    add_obj(f'CockpitShutter_{k}', shutter_mesh, Gk @ SHUTTER_LOCAL @ T((0, SHUTTER_SLIDE, 0)), body)

# ================= Ladder =================
LS = FF @ Vector((0, -0.40, 0.90))
LG = Vector((0, LS.y - 0.65, -1.87))
ln = (LG - LS).normalized()
llen = (LG - LS).length
bm = bmesh.new()
for sx in (-1, 1):
    off = X * (sx * 0.17)
    bar(bm, LS + off - ln * 0.10, LG + off - ln * 0.03, 0.04, 0.08, YEL)
    cbox(bm, T(LG + off + Vector((0, 0, 0.025))), (0.09, 0.16, 0.05), DRK, 0.015)
    bar(bm, LS + off + Vector((0, 0, 0.02)), LS + off + Vector((0, 0.11, 0.02)), 0.04, 0.05, YEL)
RUNGS = 7
for k in range(RUNGS):
    box(bm, T(LS + ln * llen * (k + 0.6) / (RUNGS + 0.4)), (0.34, 0.09, 0.025), DRK)
box(bm, T(LS + Vector((0, 0.03, 0.05))), (0.40, 0.14, 0.025), MET)
W = T(LS) @ frame(ln, X)
add_obj('Ladder', make_mesh(bm, 'Ladder', W), W, body)

# ================= Missile hatches + missiles =================
hatches = []
for side, sgn in (('R', 1), ('L', -1)):
    l0, l1 = (117, 161) if sgn > 0 else (-161, -117)
    bm = bmesh.new()
    shell(bm, l0, l1, 19, 63.5, P0, 1.022, RED)
    shell(bm, l0 + 5, l1 - 5, 25, 58, 1.02, 1.04, RED, 1.0)
    lc = (l0 + l1) / 2
    n = sdir(lc, 19)
    east = Vector((math.cos(math.radians(lc)), math.sin(math.radians(lc)), 0))
    cyl(bm, T(n * 1.0) @ frame(east), 0.022, 0.5, 8, MET)
    Fh = Matrix((east, n.cross(east), n)).transposed().to_4x4()
    W = T(n * 1.0) @ Fh
    hatches.append(add_obj(f'MissileHatch_{side}', make_mesh(bm, f'MissileHatch_{side}', W), W, body))

bm = bmesh.new()
lathe(bm, Matrix.Identity(4), [(0, 0), (0.030, 0), (0.042, 0.025), (0.042, 0.17), (0.042, 0.20), (0.042, 0.23),
                               (0.028, 0.29), (0, 0.32)], [DRK, DRK, DRK, YEL, RED, RED, RED], 8)
for k in range(4):
    box(bm, RZd(90 * k + 22.5) @ T((0.055, 0, 0.045)), (0.04, 0.008, 0.07), YEL)
missile_mesh = make_mesh(bm, 'Missile')
for name, W in MISSILE_W.items():
    add_obj(name, missile_mesh, W, body)

# ================= Guns =================
GUN_C = Vector((1.30, 0, 0.64))
GUN_P = Vector((1.03, 0, 0.62))
for side, mirror in (('R', False), ('L', True)):
    Fg = T(GUN_C) @ FF
    Fo = Fg @ RZd(22.5)
    bm = bmesh.new()
    cyl(bm, AX((1.05, 0, 0.62)), 0.16, 0.09, 10, YEL)
    lathe(bm, Fo, [(0, -0.42), (0.16, -0.42), (0.2, -0.37), (0.2, -0.18), (0.245, -0.18), (0.245, 0.28),
                   (0.215, 0.31), (0, 0.31)], [DRK, DRK, DRK, RED, RED, RED, RED], 8)
    lathe(bm, Fo, [(0.2, -0.30), (0.215, -0.30), (0.215, -0.25), (0.2, -0.25)], YEL, 8)
    for ang in (0, 90):
        Fa = Fg @ RZd(ang)
        box(bm, Fa @ T((0.226, 0, 0.05)), (0.02, 0.20, 0.30), DRK)
        for zz in (-0.04, 0.05, 0.14):
            box(bm, Fa @ T((0.238, 0, zz)), (0.012, 0.14, 0.05), GLY)
    cbox(bm, T((1.12, -0.02, 0.33)), (0.26, 0.34, 0.14), YEL, 0.025)
    box(bm, T((1.12, -0.19, 0.33)), (0.20, 0.02, 0.09), DRK)
    for dx in (-0.05, 0.05):
        box(bm, T((1.12 + dx, -0.20, 0.33)), (0.06, 0.02, 0.05), GLY)
    sx = -1 if mirror else 1
    W = T((sx * GUN_P.x, GUN_P.y, GUN_P.z)) @ FF
    gun = add_obj(f'Gun_{side}', make_mesh(bm, f'Gun_{side}', W, mirror), W, body)

    bm = bmesh.new()
    lathe(bm, Fo, [(0.15, 0.12), (0.15, 0.36), (0.215, 0.36), (0.215, 0.57), (0.25, 0.57), (0.25, 0.66),
                   (0.225, 0.69), (0.185, 0.69), (0.185, 0.56), (0.11, 0.50), (0, 0.50)],
          [DRK, RED, RED, YEL, YEL, YEL, YEL, DRK, DRK, GLO], 8)
    W = T((sx * GUN_C.x, GUN_C.y - 0.31, GUN_C.z)) @ FF
    add_obj(f'GunBarrel_{side}', make_mesh(bm, f'GunBarrel_{side}', W, mirror), W, gun)

# ================= Legs =================
H, K, A = Vector((1.18, 0, -0.44)), Vector((1.74, 0, -0.20)), Vector((1.90, 0, -1.60))

bm = bmesh.new()
cyl(bm, AY(H), 0.20, 0.46, 12, DRK)
cyl(bm, AY(H), 0.225, 0.12, 12, YEL)
cyl(bm, AY(H), 0.08, 0.50, 8, MET)
d = K - H
Ft = frame(d, Y)
up = axis(Ft, 1)
cbox(bm, T(H + d * 0.5 + up * 0.03) @ Ft, (0.40, 0.34, d.length + 0.06), RED, 0.06)
cbox(bm, T(H + d * 0.5 + up * 0.21) @ Ft, (0.46, 0.08, d.length * 0.75), RED, 0.025)
box(bm, T(H + d * 0.5 + up * 0.25) @ Ft, (0.30, 0.02, d.length * 0.45), DRK)
thigh_mesh = make_mesh(bm, 'Thigh', T(H))

bm = bmesh.new()
d2 = A - K
dn = d2.normalized()
Fs = frame(d2, Y)
out = axis(Fs, 1)
cyl(bm, AY(K), 0.23, 0.68, 12, DRK)
cyl(bm, AY(K), 0.255, 0.14, 12, YEL)
cyl(bm, AY(K), 0.09, 0.72, 8, MET)
cbox(bm, T(K + dn * 0.24 + out * 0.12) @ Fs, (0.62, 0.50, 0.68), RED, 0.08, (0.85, 0.8))
cbox(bm, T(K + dn * 0.26 + out * 0.36) @ Fs, (0.40, 0.05, 0.42), RED, 0.02)
box(bm, T(K + dn * 0.12 + out * 0.385) @ Fs, (0.26, 0.03, 0.07), DRK)
for sx in (-1, 1):
    for dz in (0.10, 0.42):
        cyl(bm, T(K + dn * dz + out * 0.385 + Y * (sx * 0.16)) @ frame(out), 0.018, 0.014, 6, MET)
cbox(bm, T(K + dn * 0.90 + out * 0.06) @ Fs, (0.50, 0.40, 0.80), RED, 0.06, (0.8, 0.8))
box(bm, T(K + dn * 0.90 + out * 0.245) @ Fs, (0.12, 0.04, 0.46), YEL)
box(bm, T(K + dn * 0.61 + out * 0.30) @ Fs, (0.50, 0.04, 0.05), YEL)
cbox(bm, T(K + dn * 0.72 - out * 0.10) @ Fs, (0.30, 0.30, 1.25), DRK, 0.03)
cbox(bm, T(K + dn * 1.27) @ Fs, (0.32, 0.32, 0.24), DRK, 0.04)
shin_mesh = make_mesh(bm, 'Shin', T(K))

bm = bmesh.new()
cyl(bm, AY(A), 0.15, 0.46, 10, DRK)
cyl(bm, AY(A), 0.07, 0.50, 8, YEL)
cbox(bm, T((1.90, 0, -1.66)), (0.40, 0.38, 0.18), DRK, 0.04)
cbox(bm, T((1.93, 0, -1.725)), (0.84, 0.70, 0.15), YEL, 0.03, (0.6, 0.65))
cbox(bm, T((1.93, 0, -1.835)), (0.90, 0.76, 0.07), DRK, 0.02)
for sy in (-1, 1):
    cbox(bm, T((2.37, sy * 0.20, -1.80)), (0.18, 0.22, 0.14), DRK, 0.03)
    box(bm, T((1.93, sy * 0.30, -1.725)) @ RXd(-sy * 28), (0.30, 0.02, 0.09), DRK)
cbox(bm, T((1.49, 0, -1.80)), (0.14, 0.36, 0.14), DRK, 0.03)
foot_mesh = make_mesh(bm, 'Foot', T(A))

PA = H + d * 0.5 - up * 0.17
PB = K + dn * 0.95 - out * 0.27
pn = (PB - PA).normalized()
plen = (PB - PA).length
bm = bmesh.new()
cyl(bm, AY(PA), 0.06, 0.20, 8, DRK)
tube(bm, PA + pn * 0.03, PA + pn * plen * 0.62, 0.07, 8, YEL)
tube(bm, PA + pn * plen * 0.58, PA + pn * plen * 0.64, 0.082, 8, DRK)
PISTON_W = T(PA) @ frame(pn, Y)
piston_mesh = make_mesh(bm, 'PistonCyl', PISTON_W)
bm = bmesh.new()
cyl(bm, AY(PB), 0.06, 0.20, 8, DRK)
tube(bm, PB, PB - pn * plen * 0.6, 0.035, 6, MET)
ROD_W = T(PB) @ frame(-pn, Y)
rod_mesh = make_mesh(bm, 'PistonRod', ROD_W)

for tag, lon in LEGS:
    M = ML(lon)
    thigh = add_obj(f'Thigh_{tag}', thigh_mesh, M @ T(H), body)
    shin = add_obj(f'Shin_{tag}', shin_mesh, M @ T(K), thigh)
    add_obj(f'Foot_{tag}', foot_mesh, M @ T(A), shin)
    add_obj(f'PistonCyl_{tag}', piston_mesh, M @ PISTON_W, thigh)
    add_obj(f'PistonRod_{tag}', rod_mesh, M @ ROD_W, shin)

# ================= UV atlas =================
uniq = {}
for o in scene.objects:
    if o.type == 'MESH':
        uniq.setdefault(o.data.name, o)
bake_objs = list(uniq.values())
for o in scene.objects:
    o.select_set(False)
for o in bake_objs:
    o.select_set(True)
bpy.context.view_layer.objects.active = bake_objs[0]
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.0)
bpy.ops.uv.select_all(action='SELECT')
bpy.ops.uv.average_islands_scale()
try:
    bpy.ops.uv.pack_islands(rotate=True, margin_method='FRACTION', margin=0.0025, shape_method='AABB')
except Exception as e:
    print('PACK_FALLBACK', e)
    bpy.ops.uv.pack_islands(margin=0.0025)
bpy.ops.object.mode_set(mode='OBJECT')

# ================= Procedural source materials =================
PARAMS = {
    'Boss_Red': dict(color=(0.37, 0.022, 0.018), kind='paint', wear=1.0, wearcol=(0.50, 0.48, 0.46), smooth=0.42, dust=1.0),
    'Boss_Dark': dict(color=(0.04, 0.04, 0.045), kind='metal', wear=1.0, wearcol=(0.30, 0.29, 0.29), smooth=0.38, dust=1.0,
                      metal=0.7),
    'Boss_Yellow': dict(color=(0.80, 0.40, 0.02), kind='paint', wear=1.0, wearcol=(0.50, 0.48, 0.46), smooth=0.42, dust=1.0),
    'Boss_Metal': dict(color=(0.36, 0.37, 0.39), kind='metal', wear=1.0, wearcol=(0.62, 0.62, 0.62), smooth=0.5, dust=1.0,
                       metal=1.0),
    'Boss_GlowOrange': dict(color=(1.0, 0.38, 0.02), kind='glow', smooth=0.8),
    'Boss_GlowYellow': dict(color=(1.0, 0.72, 0.08), kind='glow', smooth=0.8),
    'Boss_GlowRed': dict(color=(1.0, 0.03, 0.02), kind='glow', smooth=0.8),
    'Boss_Cabin': dict(color=(0.11, 0.10, 0.09), kind='paint', wear=0.6, wearcol=(0.45, 0.44, 0.42), smooth=0.35, dust=0.3),
    'Boss_Seat': dict(color=(0.30, 0.07, 0.03), kind='paint', wear=0.15, wearcol=(0.45, 0.20, 0.10), smooth=0.25, dust=0.3),
}
DIRT_COLOR, DUST_COLOR = (0.02, 0.017, 0.015), (0.27, 0.22, 0.16)
BEVEL_RADIUS, AO_DISTANCE = 0.035, 0.5


def build_source(mat, P, mode, image):
    nt = mat.node_tree
    nt.nodes.clear()
    N, L = nt.nodes.new, nt.links.new

    def put(sock, v):
        if isinstance(v, bpy.types.NodeSocket):
            L(v, sock)
        elif isinstance(v, (int, float)):
            sock.default_value = v
        else:
            sock.default_value = (*v, 1)

    def M(op, a, b=0.0, clamp=False):
        n = N('ShaderNodeMath')
        n.operation, n.use_clamp = op, clamp
        put(n.inputs[0], a)
        put(n.inputs[1], b)
        return n.outputs[0]

    def mix(f, a, b):
        n = N('ShaderNodeMix')
        n.data_type = 'RGBA'
        put(n.inputs[0], f)
        put(n.inputs[6], a)
        put(n.inputs[7], b)
        return n.outputs[2]

    def smooth(x, lo, hi):
        n = N('ShaderNodeMapRange')
        n.interpolation_type = 'SMOOTHSTEP'
        put(n.inputs[0], x)
        n.inputs[1].default_value, n.inputs[2].default_value = lo, hi
        return n.outputs[0]

    def vscale(v, k):
        n = N('ShaderNodeVectorMath')
        n.operation = 'SCALE'
        L(v, n.inputs[0])
        put(n.inputs[3], k)
        return n.outputs[0]

    def rgb(c):
        n = N('ShaderNodeRGB')
        n.outputs[0].default_value = (*c, 1)
        return n.outputs[0]

    tex = N('ShaderNodeTexImage')
    tex.image = image
    nt.nodes.active = tex
    out = N('ShaderNodeOutputMaterial')
    bev = N('ShaderNodeBevel')
    bev.samples = 6
    bev.inputs['Radius'].default_value = BEVEL_RADIUS
    if mode == 'normal':
        bsdf = N('ShaderNodeBsdfPrincipled')
        if P['kind'] != 'glow':
            L(bev.outputs[0], bsdf.inputs['Normal'])
        L(bsdf.outputs[0], out.inputs['Surface'])
        return
    em = N('ShaderNodeEmission')
    L(em.outputs[0], out.inputs['Surface'])
    glow = P['kind'] == 'glow'
    if mode == 'emission':
        L(rgb(P['color'] if glow else (0, 0, 0)), em.inputs['Color'])
        return
    if glow:
        L(rgb([c * 0.55 for c in P['color']] if mode == 'albedo' else (0.0, P['smooth'], 0.0)), em.inputs['Color'])
        return

    tc = N('ShaderNodeTexCoord').outputs['Object']
    geo = N('ShaderNodeNewGeometry')

    def noise(scale, detail):
        n = N('ShaderNodeTexNoise')
        L(tc, n.inputs['Vector'])
        n.inputs['Scale'].default_value, n.inputs['Detail'].default_value = scale, detail
        return n.outputs[0]

    aon = N('ShaderNodeAmbientOcclusion')
    aon.only_local, aon.samples = True, 16
    aon.inputs['Distance'].default_value = AO_DISTANCE
    ao = aon.outputs['AO']
    nb, nm, nf = noise(0.9, 2.0), noise(5.0, 4.0), noise(34.0, 2.0)
    dotn = N('ShaderNodeVectorMath')
    dotn.operation = 'DOT_PRODUCT'
    L(bev.outputs[0], dotn.inputs[0])
    L(geo.outputs['Normal'], dotn.inputs[1])
    edge = smooth(M('SUBTRACT', 1.0, dotn.outputs['Value']), 0.004, 0.07)
    convex = smooth(ao, 0.6, 0.9)
    wear = smooth(M('MULTIPLY', M('MULTIPLY', edge, convex), M('ADD', 0.35, M('MULTIPLY', nm, 1.3))), 0.42, 0.72)
    chips = smooth(M('MULTIPLY', nf, nm), 0.40, 0.46)
    wear = M('MULTIPLY', M('MAXIMUM', wear, M('MULTIPLY', chips, 0.8)), P['wear'], clamp=True)
    dirt = M('MULTIPLY', M('SUBTRACT', 1.0, ao), M('ADD', 0.5, M('MULTIPLY', nm, 1.6)), clamp=True)
    sep = N('ShaderNodeSeparateXYZ')
    L(geo.outputs['Position'], sep.inputs[0])
    dust = M('MULTIPLY', M('SUBTRACT', 1.0, smooth(sep.outputs['Z'], 0.0, 1.0)),
             M('ADD', 0.25, M('MULTIPLY', nb, 0.9)), clamp=True)
    dust = M('MULTIPLY', dust, P['dust'] * 0.6)

    if mode == 'albedo':
        c = vscale(rgb(P['color']), M('ADD', 0.86, M('MULTIPLY', nb, 0.28)))
        c = mix(M('MULTIPLY', dirt, 0.75), c, DIRT_COLOR)
        c = mix(wear, c, P['wearcol'])
        c = mix(dust, c, DUST_COLOR)
        c = vscale(c, M('ADD', 0.5, M('MULTIPLY', ao, 0.5)))
        L(c, em.inputs['Color'])
        return
    if P['kind'] == 'paint':
        metal = M('MULTIPLY', wear, 0.9)
    else:
        metal = M('MULTIPLY', M('SUBTRACT', 1.0, dust), P['metal'])
    sm = M('ADD', P['smooth'], M('MULTIPLY', wear, 0.6 - P['smooth']))
    sm = M('MULTIPLY', sm, M('SUBTRACT', 1.0, M('MULTIPLY', dust, 0.5)))
    sm = M('MULTIPLY', sm, M('SUBTRACT', 1.0, M('MULTIPLY', dirt, 0.4)))
    comb = N('ShaderNodeCombineXYZ')
    L(metal, comb.inputs[0])
    L(sm, comb.inputs[1])
    L(comb.outputs[0], em.inputs['Color'])


# ================= Bake =================
scene.render.engine = 'CYCLES'
try:
    cprefs = bpy.context.preferences.addons['cycles'].preferences
    for dev in ('OPTIX', 'CUDA', 'HIP', 'ONEAPI'):
        try:
            cprefs.compute_device_type = dev
        except Exception:
            continue
        cprefs.get_devices()
        gpus = [d for d in cprefs.devices if d.type != 'CPU']
        if gpus:
            for d in cprefs.devices:
                d.use = d.type != 'CPU'
            scene.cycles.device = 'GPU'
            print('BAKE_DEVICE', dev, [d.name for d in gpus])
            break
except Exception as e:
    print('GPU_SETUP_FAILED', e)

os.makedirs(TEX_DIR, exist_ok=True)
PASSES = (('albedo', 'EMIT', 48, 'sRGB', (0.05, 0.04, 0.04, 1)), ('emission', 'EMIT', 1, 'sRGB', (0, 0, 0, 1)),
          ('ms', 'EMIT', 16, 'Non-Color', (0, 0.4, 0, 1)), ('normal', 'NORMAL', 16, 'Non-Color', (0.5, 0.5, 1, 1)))
IMG = {}
for mode, btype, samples, cspace, fill in PASSES:
    img = bpy.data.images.new(f'BossMech_{mode}', TEX, TEX, alpha=False)
    img.colorspace_settings.name = cspace
    img.generated_color = fill
    IMG[mode] = img
    for m in MATS:
        build_source(m, PARAMS[m.name], mode, img)
    scene.cycles.samples = samples
    bpy.ops.object.bake(type=btype, margin=6, use_clear=False)
    print('BAKED', mode)


def save_image(img, name):
    img.filepath_raw = os.path.join(TEX_DIR, name)
    img.file_format = 'PNG'
    img.save()


save_image(IMG['albedo'], 'BossMech_Albedo.png')
save_image(IMG['emission'], 'BossMech_Emission.png')
save_image(IMG['normal'], 'BossMech_Normal.png')
px = np.array(IMG['ms'].pixels[:], dtype=np.float32).reshape(-1, 4)
msa = np.stack([px[:, 0], px[:, 0], px[:, 0], px[:, 1]], axis=1)
ms_img = bpy.data.images.new('BossMech_MetallicSmoothness', TEX, TEX, alpha=True)
ms_img.colorspace_settings.name = 'Non-Color'
ms_img.alpha_mode = 'CHANNEL_PACKED'
ms_img.pixels.foreach_set(msa.ravel())
save_image(ms_img, 'BossMech_MetallicSmoothness.png')
bpy.data.images.remove(IMG.pop('ms'))

# ================= Final material =================
fin = bpy.data.materials.new('BossMech')
try:
    fin.use_nodes = True
except Exception:
    pass
nt = fin.node_tree
bsdf = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')


def tex_node(img):
    n = nt.nodes.new('ShaderNodeTexImage')
    n.image = img
    return n


t_alb, t_emi, t_nrm, t_ms = tex_node(IMG['albedo']), tex_node(IMG['emission']), tex_node(IMG['normal']), tex_node(ms_img)
nt.links.new(t_alb.outputs['Color'], bsdf.inputs['Base Color'])
nt.links.new(t_emi.outputs['Color'], bsdf.inputs['Emission Color'])
bsdf.inputs['Emission Strength'].default_value = 4.0
nt.links.new(t_ms.outputs['Color'], bsdf.inputs['Metallic'])
inv = nt.nodes.new('ShaderNodeMath')
inv.operation = 'SUBTRACT'
inv.inputs[0].default_value = 1.0
nt.links.new(t_ms.outputs['Alpha'], inv.inputs[1])
nt.links.new(inv.outputs[0], bsdf.inputs['Roughness'])
nmap = nt.nodes.new('ShaderNodeNormalMap')
nt.links.new(t_nrm.outputs['Color'], nmap.inputs['Color'])
nt.links.new(nmap.outputs[0], bsdf.inputs['Normal'])
for o in bake_objs:
    o.data.materials.clear()
    o.data.materials.append(fin)
    o.data.polygons.foreach_set('material_index', [0] * len(o.data.polygons))
    o.data.update()
for m in MATS:
    bpy.data.materials.remove(m)

# ================= Check renders =================
cam_data = bpy.data.cameras.new('Cam')
cam = bpy.data.objects.new('Cam', cam_data)
scene.collection.objects.link(cam)
scene.camera = cam
world = bpy.data.worlds.new('W')
try:
    world.use_nodes = True
except Exception:
    pass
bg = next((n for n in world.node_tree.nodes if n.type == 'BACKGROUND'), None) if world.node_tree else None
if bg:
    bg.inputs[0].default_value = (0.62, 0.64, 0.68, 1)
    bg.inputs[1].default_value = 0.7
scene.world = world
sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN'))
sun.data.energy, sun.data.angle = 3.5, 0.15
sun.rotation_euler = (math.radians(52), 0, math.radians(28))
scene.collection.objects.link(sun)
gmat = bpy.data.materials.new('GroundPreview')
gmat.diffuse_color = (0.12, 0.12, 0.13, 1)
gm = bpy.data.meshes.new('Ground')
gm.from_pydata([(-40, -40, 0), (40, -40, 0), (40, 40, 0), (-40, 40, 0)], [], [(0, 1, 2, 3)])
gm.materials.append(gmat)
ground = bpy.data.objects.new('Ground', gm)
scene.collection.objects.link(ground)
scene.cycles.samples = 64
scene.cycles.use_denoising = True
scene.view_settings.view_transform = 'Standard'
VW, VH = 800, 700
scene.render.resolution_x, scene.render.resolution_y = VW, VH
VIEWS = [
    ('persp', Vector((4.2, -10.5, 3.4)), Vector((0, 0, 2.1))),
    ('rear', Vector((-6.0, 9.0, 6.0)), Vector((0, 0, 2.2))),
]
tiles = []
for name, loc, target in VIEWS:
    cam.location = loc
    cam.rotation_euler = (target - loc).to_track_quat('-Z', 'Y').to_euler()
    if name == 'rear':
        for h in hatches:
            h.matrix_basis = h.matrix_basis @ RXd(HATCH_OPEN)
    path = os.path.join(TEX_DIR, f'_boss_{name}.png')
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(path)
    tiles.append(np.array(img.pixels[:], dtype=np.float32).reshape(VH, VW, 4))
    bpy.data.images.remove(img)
    os.remove(path)
    if name == 'rear':
        for h in hatches:
            h.matrix_basis = h.matrix_basis @ RXd(-HATCH_OPEN)
sheet = np.concatenate(tiles, axis=1)
out = bpy.data.images.new('sheet', VW * 2, VH, alpha=True)
out.pixels.foreach_set(sheet.ravel())
out.filepath_raw = OUT_PNG
out.file_format = 'PNG'
out.save()
bpy.data.images.remove(out)
for o in (cam, sun, ground):
    bpy.data.objects.remove(o)
bpy.data.materials.remove(gmat)

bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
if OUT_FBX != '-':
    # exporter rotates only root objects: pre-rotate root so Unity gets an identity root
    from bpy_extras.io_utils import axis_conversion
    G = axis_conversion(to_forward='-Z', to_up='Y').to_4x4()
    root.matrix_basis = G.inverted()
    body.matrix_basis = G @ body.matrix_basis
    bpy.ops.export_scene.fbx(filepath=OUT_FBX, object_types={'MESH', 'EMPTY'}, use_selection=False,
                             apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                             bake_space_transform=False, mesh_smooth_type='EDGE', add_leaf_bones=False,
                             bake_anim=False, path_mode='STRIP')
print('TRIS', TRIS[0], 'MESHES', len(bake_objs))
