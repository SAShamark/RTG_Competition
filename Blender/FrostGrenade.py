import bpy, bmesh, math, sys
from mathutils import Vector, Matrix

R = 0.045
OUT_BLEND, OUT_FBX, OUT_PNG = sys.argv[sys.argv.index('--') + 1:][:3]

bpy.ops.wm.read_factory_settings(use_empty=True)

COLORS = {
    'FG_Yellow': ((1.0, 0.62, 0.03), 0.0),
    'FG_Dark': ((0.08, 0.08, 0.10), 0.0),
    'FG_Glow': ((0.15, 0.85, 1.0), 4.0),
    'FG_Ice': ((0.45, 0.80, 1.0), 0.6),
}
NAMES = list(COLORS)
YEL, DRK, GLO, ICE = range(4)


def make_mat(name):
    m = bpy.data.materials.new(name)
    try:
        m.use_nodes = True
    except Exception:
        pass
    color, emission = COLORS[name]
    bsdf = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Roughness'].default_value = 0.6
    if emission:
        bsdf.inputs['Emission Color'].default_value = (*color, 1)
        bsdf.inputs['Emission Strength'].default_value = emission
    m.diffuse_color = (*color, 1)
    return m


MATS = [make_mat(n) for n in NAMES]


def T(v):
    return Matrix.Translation(Vector(v))


def RZ(a):
    return Matrix.Rotation(a, 4, 'Z')


def set_mat(verts, mi):
    for f in {f for v in verts for f in v.link_faces}:
        f.material_index = mi


def frame(z, x_hint):
    z = z.normalized()
    x = (x_hint - x_hint.dot(z) * z).normalized()
    y = z.cross(x)
    return Matrix((x, y, z)).transposed().to_4x4()


def box(bm, M, size, mi):
    r = bmesh.ops.create_cube(bm, size=1.0, matrix=M @ Matrix.Diagonal(Vector((*size, 1))))
    set_mat(r['verts'], mi)


def cone(bm, M, r1, r2, depth, segs, mi):
    r = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs,
                              radius1=r1, radius2=r2, depth=depth, matrix=M)
    set_mat(r['verts'], mi)


def poly(bm, pts, mi, hint):
    f = bm.faces.new([bm.verts.new(p) for p in pts])
    f.normal_update()
    if f.normal.dot(hint) < 0:
        f.normal_flip()
    f.material_index = mi


def lathe(bm, profile, mats, segs):
    for s in range(len(profile) - 1):
        (r0, z0), (r1, z1) = profile[s], profile[s + 1]
        nr, nz = (z1 - z0), -(r1 - r0)
        for k in range(segs):
            a0, a1 = 2 * math.pi * k / segs, 2 * math.pi * (k + 1) / segs
            am = (a0 + a1) / 2
            pts = []
            for r, z, a in ((r0, z0, a0), (r0, z0, a1), (r1, z1, a1), (r1, z1, a0)):
                p = Vector((r * math.cos(a), r * math.sin(a), z))
                if not any((p - q).length < 1e-7 for q in pts):
                    pts.append(p)
            poly(bm, pts, mats[s], Vector((nr * math.cos(am), nr * math.sin(am), nz)))


def cell_corners(n, u, v, i, j, count=3):
    def a(k):
        return math.tan(math.radians(-45 + 90 * k / count))
    return [(n + a(p) * u + a(q) * v).normalized() for p, q in ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))]


def plate(bm, corners, mi):
    c = sum(corners, Vector()).normalized()

    def ring(s, rad):
        return [(c + (p - c) * s).normalized() * rad for p in corners]

    base, mid, top = ring(0.93, 0.95 * R), ring(0.93, 1.0 * R), ring(0.78, 1.035 * R)
    poly(bm, top, mi, c)
    for k in range(4):
        k2 = (k + 1) % 4
        out = (mid[k] + mid[k2]) / 2 - c * R
        poly(bm, [base[k], base[k2], mid[k2], mid[k]], mi, out)
        poly(bm, [mid[k], mid[k2], top[k2], top[k]], mi, out + c * R * 0.05)
    return c


def emblem(bm, n):
    x = Vector((0, 0, 1)).cross(n)
    F = frame(n, x)

    def at(d):
        return T(n * d) @ F

    cone(bm, at(0.92 * R) @ RZ(math.pi / 8), 0.40 * R, 0.40 * R, 0.2 * R, 8, YEL)
    cone(bm, at(0.935 * R) @ RZ(math.pi / 8), 0.32 * R, 0.32 * R, 0.2 * R, 8, DRK)
    S = at(1.041 * R)
    for deg in (90, 30, 150):
        box(bm, S @ RZ(math.radians(deg)), (0.48 * R, 0.05 * R, 0.012 * R), GLO)
    for k in range(6):
        a = math.radians(30 + 60 * k)
        px, py = math.cos(a) * 0.15 * R, math.sin(a) * 0.15 * R
        for s in (-1, 1):
            b = a + s * math.radians(55)
            cx, cy = px + math.cos(b) * 0.045 * R, py + math.sin(b) * 0.045 * R
            box(bm, S @ T((cx, cy, 0)) @ RZ(b), (0.09 * R, 0.04 * R, 0.012 * R), GLO)


def bar_xz(bm, p0, p1, width, thick, mi, lift=0.0, shrink=1.0):
    dx, dz = (p1[0] - p0[0]), (p1[1] - p0[1])
    length = math.hypot(dx, dz)
    M = (T(((p0[0] + p1[0]) / 2 * R, 0, (p0[1] + p1[1]) / 2 * R))
         @ Matrix.Rotation(-math.atan2(dz, dx), 4, 'Y') @ T((0, 0, lift * R)))
    box(bm, M, (length * shrink * R, width * R, thick * R), mi)


def finish(bm, name, origin):
    bmesh.ops.translate(bm, verts=bm.verts, vec=-Vector(origin))
    used = sorted({f.material_index for f in bm.faces})
    for f in bm.faces:
        f.material_index = used.index(f.material_index)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for mi in used:
        mesh.materials.append(MATS[mi])
    obj = bpy.data.objects.new(name, mesh)
    obj.location = Vector(origin)
    bpy.context.scene.collection.objects.link(obj)
    return obj


scene = bpy.data.scenes[0] if bpy.data.scenes else bpy.data.scenes.new('Scene')
bpy.context.window.scene = scene

X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))

# ---------- Body ----------
bm = bmesh.new()
r = bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=8, radius=0.955 * R)
set_mat(r['verts'], DRK)

for n, u, v in ((-Y, X, Z), (Y, X, Z), (-X, Y, Z), (X, Y, Z), (-Z, X, Y), (Z, X, Y)):
    is_face = abs(n.y) > 0.5
    for i in range(3):
        for j in range(3):
            corners = cell_corners(n, u, v, i, j)
            if is_face and i == 1 and j == 1:
                emblem(bm, n)
                continue
            is_band = is_face and j == 1
            is_bottom = n.z < -0.5 and i == 1 and j == 1
            c = plate(bm, corners, DRK if (is_band or is_bottom) else YEL)
            if is_band:
                box(bm, T(c * R * 1.04) @ frame(c, u), (0.3 * R, 0.1 * R, 0.02 * R), GLO)
            elif is_face and i == 1:
                cone(bm, T(c * R * 1.045) @ frame(c, u), 0.05 * R, 0.05 * R, 0.03 * R, 8, DRK)

spikes = [X, -X] + [Vector((sx, sy, sz)).normalized() for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)]
for d in spikes:
    F = frame(d, Z)
    cone(bm, T(d * R * 0.99) @ F, 0.26 * R, 0.23 * R, 0.16 * R, 8, DRK)
    cone(bm, T(d * R * 1.27) @ F, 0.19 * R, 0.0015 * R, 0.42 * R, 6, ICE)

profile = [(0, 0.80), (0.66, 0.80), (0.52, 1.08), (0.42, 1.08), (0.42, 1.36),
           (0.54, 1.36), (0.54, 1.60), (0.40, 1.60), (0.40, 1.48), (0, 1.48)]
lathe(bm, [(pr * R, pz * R) for pr, pz in profile], [DRK, DRK, DRK, DRK, YEL, YEL, YEL, YEL, DRK], 16)
for k in range(8):
    a = math.radians((2 * k + 0.5) * 22.5)
    box(bm, T((math.cos(a) * 0.415 * R, math.sin(a) * 0.415 * R, 1.22 * R)) @ RZ(a),
        (0.03 * R, 0.1 * R, 0.18 * R), GLO)
box(bm, T((0.6 * R, 0, 1.47 * R)), (0.24 * R, 0.3 * R, 0.22 * R), DRK)
body = finish(bm, 'FrostGrenade', (0, 0, 0))

# ---------- Lever ----------
bm = bmesh.new()
cone(bm, T((0.74 * R, 0, 1.52 * R)) @ Matrix.Rotation(math.pi / 2, 4, 'X'), 0.11 * R, 0.11 * R, 0.38 * R, 10, YEL)
bar_xz(bm, (0.5, 1.68), (1.08, 1.68), 0.34, 0.15, DRK)
bar_xz(bm, (0.5, 1.68), (1.08, 1.68), 0.28, 0.05, YEL, lift=0.085, shrink=0.85)
bar_xz(bm, (1.03, 1.68), (1.5, 1.2), 0.34, 0.15, DRK)
bar_xz(bm, (1.03, 1.68), (1.5, 1.2), 0.18, 0.03, GLO, lift=0.08, shrink=0.6)
lever = finish(bm, 'Lever', (0.74 * R, 0, 1.52 * R))

# ---------- Pin ----------
bm = bmesh.new()
ring_c, ring_r, count = (1.64, 0.78), 0.22, 10
bar_xz(bm, (1.47, 1.2), (1.62, 1.0), 0.14, 0.11, DRK)
pts = [(ring_c[0] + ring_r * math.cos(math.radians(18 + 36 * k)), ring_c[1] + ring_r * math.sin(math.radians(18 + 36 * k)))
       for k in range(count)]
for k in range(count):
    p0, p1 = pts[k], pts[(k + 1) % count]
    is_low = (p0[1] + p1[1]) / 2 < ring_c[1] - 0.1
    bar_xz(bm, p0, p1, 0.12, 0.11, YEL if is_low else DRK, shrink=1.25)
pin = finish(bm, 'Pin', (ring_c[0] * R, 0, ring_c[1] * R))

lever.parent = body
pin.parent = body

# ---------- Check render ----------
cam_data = bpy.data.cameras.new('Cam')
cam_data.lens = 70
cam_data.clip_start = 0.01
cam = bpy.data.objects.new('Cam', cam_data)
scene.collection.objects.link(cam)
cam.location = Vector((0.11, -0.40, 0.10))
target = Vector((0.012, 0, 0.012))
cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
scene.camera = cam
world = bpy.data.worlds.new('W')
world.color = (0.35, 0.33, 0.34)
scene.world = world
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'MATERIAL'
scene.display.shading.show_cavity = True
scene.render.resolution_x = 800
scene.render.resolution_y = 800
scene.render.filepath = OUT_PNG
bpy.ops.render.render(write_still=True)

bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
bpy.ops.export_scene.fbx(filepath=OUT_FBX, object_types={'MESH'}, use_selection=False,
                         apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False)
tris = sum(len(p.vertices) - 2 for o in (body, lever, pin) for p in o.data.polygons)
print('TRIS', tris)
