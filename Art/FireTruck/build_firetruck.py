# Procedural fire truck for Flame & Blame.
# blender -b --python build_firetruck.py -- <out.blend> <out.fbx> [render_prefix]
# Authoring space: X = lateral (+X is the truck's LEFT side, where the pump panel and
# equipment bay are), f = forward, Z = up. Stored in Blender as (X, -f, Z); exported with
# bake_space_transform, Unity reads it as (-x, z, f): the truck faces +Z, its left is -X.
import bpy, bmesh, sys, math
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT_BLEND = argv[0] if len(argv) > 0 else None
OUT_FBX = argv[1] if len(argv) > 1 else None
RENDER = argv[2] if len(argv) > 2 else None

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.name = 'Shutter_Open'
scene.render.fps = 30
FRAMES = 31
scene.frame_start, scene.frame_end = 1, FRAMES

# ---------------------------------------------------------------- materials
MATS = {}


def srgb(h):
    def lin(c):
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return tuple(lin(int(h[i:i + 2], 16) / 255) for i in (0, 2, 4)) + (1.0,)


def mat(name, hexc, emit=0.0, rough=0.85, metal=0.0):
    m = bpy.data.materials.new('M_Truck_' + name)
    try:
        m.use_nodes = True
    except Exception:
        pass
    col = srgb(hexc)
    m.diffuse_color = col
    m.roughness = rough
    m.metallic = metal
    b = m.node_tree.nodes.get('Principled BSDF')
    b.inputs['Base Color'].default_value = col
    b.inputs['Roughness'].default_value = rough
    b.inputs['Metallic'].default_value = metal
    if emit > 0:
        b.inputs['Emission Color'].default_value = col
        b.inputs['Emission Strength'].default_value = emit
    MATS[name] = m


mat('Red', 'E2202E')
mat('RedDark', 'BE1626')
mat('Cream', 'F7F1E6')
mat('Yellow', 'FFC31F')
mat('Glass', '22335A', rough=0.3)
mat('Trim', '3C3C48')
mat('Black', '19191B')
mat('Tyre', '202022', rough=0.95)
mat('Steel', 'CDD2DA', rough=0.6)
mat('Interior', '343744')
mat('Shelf', '5E626E')
mat('White', 'F3F2EC')
mat('BoxGrey', '737884')
mat('Amber', 'FFAE1F', emit=1.2)
mat('LampWhite', 'FFF0CF', emit=1.5)
mat('TailRed', 'F0301F', emit=1.5)
mat('BeaconRed', 'E3201F', emit=1.0)
mat('BeaconBlue', '2346E0', emit=1.0)

# ---------------------------------------------------------------- builders
WORLD = {}


def _place(ob, origin, parent):
    o = Vector((origin[0], -origin[1], origin[2]))
    scene.collection.objects.link(ob)
    if parent:
        ob.parent = parent
        ob.location = o - WORLD[parent.name]
    else:
        ob.location = o
    WORLD[ob.name] = o
    return ob


def empty(name, origin, parent=None):
    ob = bpy.data.objects.new(name, None)
    ob.empty_display_size = 0.1
    ob.empty_display_type = 'ARROWS'
    return _place(ob, origin, parent)


class Part:
    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.mats = []

    def _tag(self, faces, m):
        if m not in self.mats:
            self.mats.append(m)
        i = self.mats.index(m)
        for f in faces:
            f.material_index = i

    def _vfaces(self, verts):
        return list({f for v in verts for f in v.link_faces})

    def box(self, x0, x1, f0, f1, z0, z1, m):
        x0, x1 = sorted((x0, x1)); f0, f1 = sorted((f0, f1)); z0, z1 = sorted((z0, z1))
        mx = Matrix.Translation(((x0 + x1) / 2, -(f0 + f1) / 2, (z0 + z1) / 2)) @ \
            Matrix.Diagonal((x1 - x0, f1 - f0, z1 - z0, 1))
        r = bmesh.ops.create_cube(self.bm, size=1.0, matrix=mx)
        self._tag(self._vfaces(r['verts']), m)

    def _loft(self, rings, m):
        n = len(rings[0])
        faces = [self.bm.faces.new(rings[0]), self.bm.faces.new(rings[-1][::-1])]
        for a, b in zip(rings, rings[1:]):
            for i in range(n):
                faces.append(self.bm.faces.new((a[i], b[i], b[(i + 1) % n], a[(i + 1) % n])))
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        self._tag(faces, m)

    def prism(self, prof, a0, a1, m, axis='x'):
        # axis 'x': prof = (f, z) extruded along X; axis 'f': prof = (x, z) extruded along f
        def P(a, p):
            return (a, -p[0], p[1]) if axis == 'x' else (p[0], -a, p[1])
        self._loft([[self.bm.verts.new(P(a, p)) for p in prof] for a in (a0, a1)], m)

    def hexa(self, b, t, z0, z1, m):
        def ring(r, z):
            x0, x1, f0, f1 = r
            return [self.bm.verts.new((x, -f, z)) for x, f in ((x0, f0), (x1, f0), (x1, f1), (x0, f1))]
        self._loft([ring(b, z0), ring(t, z1)], m)

    def slab(self, A, B, x0, x1, t0, t1, th, m, sink=0.005):
        A = Vector(A); d = Vector(B) - A
        n = Vector((d.y, -d.x)).normalized()
        p0 = A + d * t0; p1 = A + d * t1
        self.prism([tuple(p0 - n * sink), tuple(p1 - n * sink), tuple(p1 + n * th), tuple(p0 + n * th)], x0, x1, m)

    def cyl(self, c, axis, r, depth, m, segs=12, r2=None):
        rot = {'x': Matrix.Rotation(math.pi / 2, 4, 'Y'), 'f': Matrix.Rotation(math.pi / 2, 4, 'X'),
               'z': Matrix.Identity(4)}[axis]
        mx = Matrix.Translation((c[0], -c[1], c[2])) @ rot
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=segs,
                                    radius1=r, radius2=r if r2 is None else r2, depth=depth, matrix=mx)
        self._tag(self._vfaces(res['verts']), m)

    def ring(self, c, r_out, r_in, depth, m, segs=12):
        vs = []
        for i in range(segs):
            a = 2 * math.pi * i / segs
            ca, sa = math.cos(a), math.sin(a)
            vs.append([self.bm.verts.new((c[0] + dx, -c[1] + r * ca, c[2] + r * sa))
                       for dx, r in ((-depth / 2, r_out), (depth / 2, r_out), (depth / 2, r_in), (-depth / 2, r_in))])
        faces = []
        for i in range(segs):
            a, b = vs[i], vs[(i + 1) % segs]
            for k in range(4):
                faces.append(self.bm.faces.new((a[k], a[(k + 1) % 4], b[(k + 1) % 4], b[k])))
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        self._tag(faces, m)

    def finish(self, origin=(0, 0, 0), parent=None):
        o = Vector((origin[0], -origin[1], origin[2]))
        bmesh.ops.translate(self.bm, verts=self.bm.verts, vec=-o)
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me)
        self.bm.free()
        for m in self.mats:
            me.materials.append(MATS[m])
        return _place(bpy.data.objects.new(self.name, me), origin, parent)


# ---------------------------------------------------------------- dimensions
W = 1.22                      # body half width
F_FRONT, F_CAB, F_REAR = 4.2, 1.45, -3.65
Z_SK, Z_ARCH, Z_BELT, Z_ROOF, Z_BODY = 0.72, 1.22, 1.68, 2.55, 2.85
WF, WR, WX, WRAD = 2.315, -1.973, 0.95, 0.55   # wheel hardpoints of the old truck
XR = W - 0.48                 # recess depth plane of the side bays
CF0, CF1, CZ0, CZ1 = -3.2, -0.35, 1.32, 2.62   # equipment bay opening
PF0, PF1, PZ0, PZ1 = 0.25, 1.3, 1.32, 2.5      # pump panel opening
SIDES = (1, -1)

# ---------------------------------------------------------------- body
B = Part('Body')
B.box(-0.7, 0.7, -3.6, 4.0, 0.38, 0.75, 'Trim')
for c in (WF, WR):
    B.box(-0.72, 0.72, c - 0.9, c + 0.9, 0.6, 1.24, 'Black')
    B.box(-W + 0.01, W - 0.01, c - 0.5, c + 0.5, 1.19, 1.22, 'Black')

B.prism([(WR + 0.86, Z_SK), (WF - 0.86, Z_SK), (WF - 0.5, Z_ARCH), (WR + 0.5, Z_ARCH)], -W, W, 'Red')
B.prism([(WF + 0.86, Z_SK), (F_FRONT, Z_SK), (F_FRONT, Z_ARCH), (WF + 0.5, Z_ARCH)], -W, W, 'Red')
B.prism([(F_REAR, Z_SK), (WR - 0.86, Z_SK), (WR - 0.5, Z_ARCH), (F_REAR, Z_ARCH)], -W, W, 'Red')

# cab
B.box(-W, W, F_CAB, F_FRONT, Z_ARCH, Z_BELT, 'Red')
WS_A, WS_B = (F_FRONT, 1.74), (3.5, Z_ROOF)
B.prism([(F_CAB, Z_BELT), (F_FRONT, Z_BELT), WS_A, WS_B, (F_CAB, Z_ROOF)], -W, W, 'Cream')
B.slab(WS_A, WS_B, -1.08, -0.035, 0.1, 0.9, 0.02, 'Glass')
B.slab(WS_A, WS_B, 0.035, 1.08, 0.1, 0.9, 0.02, 'Glass')
B.box(-0.5, 0.5, F_FRONT, F_FRONT + 0.03, 0.85, 1.55, 'Black')
B.box(-1.3, 1.3, 4.08, 4.42, 0.42, 0.74, 'Trim')
B.cyl((0, 4.46, 0.58), 'x', 0.07, 0.36, 'Red', 8)
B.box(-0.24, -0.18, 4.42, 4.52, 0.48, 0.68, 'Trim')
B.box(0.18, 0.24, 4.42, 4.52, 0.48, 0.68, 'Trim')
# raised roof with beacons
B.hexa((-0.85, 0.85, F_CAB, 2.75), (-0.68, 0.68, F_CAB, 2.5), Z_ROOF, 3.0, 'Cream')
RB_A, RB_B = (2.75, Z_ROOF), (2.5, 3.0)
B.slab(RB_A, RB_B, -0.5, 0.5, 0.25, 0.78, 0.015, 'Black')
B.slab(RB_A, RB_B, -0.42, -0.06, 0.35, 0.68, 0.03, 'Amber')
B.slab(RB_A, RB_B, 0.06, 0.42, 0.35, 0.68, 0.03, 'Amber')

# rear body: core plus the shell around the two side bays
B.box(-W, XR, F_REAR, F_CAB, Z_ARCH, Z_BODY, 'Red')
B.box(XR, W, F_REAR, CF0, Z_ARCH, Z_BODY, 'Red')
B.box(XR, W, CF0, CF1, CZ1, Z_BODY, 'Red')
B.box(XR, W, CF0, CF1, Z_ARCH, CZ0, 'Red')
B.box(XR, W, CF1, PF0, Z_ARCH, Z_BODY, 'Red')
B.box(XR, W, PF0, PF1, PZ1, Z_BODY, 'Red')
B.box(XR, W, PF0, PF1, Z_ARCH, PZ0, 'Red')
B.box(XR, W, PF1, F_CAB, Z_ARCH, Z_BODY, 'Red')


def frame(sx, f0, f1, z0, z1, m='Cream', t=0.05, p=0.015):
    xa, xb = sx * W, sx * (W + p)
    B.box(xa, xb, f0 - t, f0, z0 - t, z1 + t, m)
    B.box(xa, xb, f1, f1 + t, z0 - t, z1 + t, m)
    B.box(xa, xb, f0, f1, z1, z1 + t, m)
    B.box(xa, xb, f0, f1, z0 - t, z0, m)


for sx in SIDES:
    frame(sx, CF0, CF1, CZ0, CZ1)
    frame(sx, PF0, PF1, PZ0, PZ1)
    xa, xb = sx * (W - 0.01), sx * (W + 0.015)
    # side glass
    B.prism([(2.62, 1.8), (3.92, 1.8), (3.46, 2.4), (2.62, 2.4)], xa, xb, 'Glass')
    B.box(xa, xb, 1.65, 2.42, 1.8, 2.4, 'Glass')
    # grab handle
    B.box(sx * (W + 0.05), sx * (W + 0.09), 2.49, 2.55, 1.2, 2.25, 'Trim')
    B.box(sx * W, sx * (W + 0.05), 2.49, 2.55, 1.2, 1.27, 'Trim')
    B.box(sx * W, sx * (W + 0.05), 2.49, 2.55, 2.18, 2.25, 'Trim')
    # mirror
    B.box(sx * W, sx * (W + 0.24), 4.0, 4.05, 1.9, 1.95, 'Trim')
    B.box(sx * W, sx * (W + 0.24), 4.0, 4.05, 2.15, 2.2, 'Trim')
    B.box(sx * (W + 0.2), sx * (W + 0.32), 3.96, 4.1, 1.78, 2.32, 'Trim')
    # head lamps, bumper lamps, tow hooks
    B.box(sx * 0.66, sx * 1.08, F_FRONT, F_FRONT + 0.04, 1.26, 1.44, 'LampWhite')
    B.box(sx * 0.66, sx * 1.08, F_FRONT, F_FRONT + 0.04, 1.02, 1.2, 'LampWhite')
    B.box(sx * 0.72, sx * 1.12, 4.42, 4.44, 0.52, 0.66, 'Amber')
    B.box(sx * 0.36, sx * 0.44, 4.42, 4.52, 0.4, 0.6, 'Yellow')
    # yellow stripe
    sa, sb = sx * W, sx * (W + 0.012)
    B.box(sa, sb, 3.3, F_FRONT, 1.02, 1.14, 'Yellow')
    B.prism([(3.3, 1.02), (3.3, 1.14), (3.0, 1.54), (3.0, 1.42)], sa, sb, 'Yellow')
    B.box(sa, sb, PF1 + 0.05, 3.0, 1.42, 1.54, 'Yellow')
    # wheel arches and skirts
    fa, fb = sx * 1.0, sx * 1.29
    for c in (WF, WR):
        o = [(c - 0.98, Z_SK), (c - 0.58, 1.28), (c + 0.58, 1.28), (c + 0.98, Z_SK)]
        i = [(c - 0.86, Z_SK), (c - 0.5, 1.2), (c + 0.5, 1.2), (c + 0.86, Z_SK)]
        B.prism([o[0], i[0], i[1], o[1]], fa, fb, 'Trim')
        B.prism([o[1], i[1], i[2], o[2]], fa, fb, 'Trim')
        B.prism([o[2], i[2], i[3], o[3]], fa, fb, 'Trim')
    B.box(fa, fb, WR + 0.98, WF - 0.98, 0.45, Z_SK, 'Trim')
    B.box(fa, fb, F_REAR, WR - 0.98, 0.45, Z_SK, 'Trim')
    B.box(fa, fb, WF + 0.98, 4.1, 0.45, Z_SK, 'Trim')
    for f in (3.5, 0.9, -0.6, -3.5):
        B.box(fb, sx * 1.3, f, f + 0.25, 0.53, 0.63, 'Amber')
    for f in (-3.5, 0.65):
        B.box(sx * W, sx * (W + 0.015), f, f + 0.25, 2.69, 2.78, 'Amber')
    # rear lamps
    r0, r1 = F_REAR - 0.03, F_REAR
    B.box(sx * 0.86, sx * 1.1, r0, r1, 1.75, 1.91, 'Amber')
    B.box(sx * 0.86, sx * 1.1, r0, r1, 1.5, 1.66, 'LampWhite')
    B.box(sx * 0.86, sx * 1.1, r0, r1, 1.25, 1.41, 'TailRed')
    B.box(sx * 0.7, sx * 1.05, r0, r1, 2.6, 2.7, 'Amber')
    B.box(sx * 0.72, sx * 1.12, F_REAR - 0.27, F_REAR - 0.25, 0.52, 0.66, 'Amber')
    B.box(sx * 0.36, sx * 0.44, F_REAR - 0.35, F_REAR - 0.25, 0.4, 0.6, 'Yellow')

B.box(-0.72, 0.72, F_REAR - 0.015, F_REAR, 0.95, 2.45, 'RedDark')
B.box(-1.3, 1.3, F_REAR - 0.25, F_REAR + 0.05, 0.42, 0.74, 'Trim')

# right side (no interaction): closed shutter and closed pump door
SLATS = 12
BAR_H = 0.06
SLAT_H = (CZ1 - CZ0 - BAR_H) / SLATS
B.box(-W - 0.004, -W, CF0, CF1, CZ0, CZ1, 'Trim')
for k in range(SLATS):
    zt = CZ1 - k * SLAT_H
    B.box(-W - 0.016, -W, CF0, CF1, zt - SLAT_H + 0.006, zt - 0.006, 'Steel')
B.box(-W - 0.02, -W, CF0, CF1, CZ0, CZ0 + BAR_H, 'Trim')
B.box(-W - 0.012, -W, PF0, PF1, PZ0, PZ1, 'RedDark')
B.box(-W - 0.04, -W - 0.012, PF0 + 0.08, PF0 + 0.14, 1.75, 2.05, 'Trim')
body = B.finish()

# ---------------------------------------------------------------- wheels
for name, sx, c in (('Wheel_FL', 1, WF), ('Wheel_FR', -1, WF), ('Wheel_BL', 1, WR), ('Wheel_BR', -1, WR)):
    P = Part(name)
    x = sx * WX
    P.cyl((x, c, WRAD), 'x', WRAD, 0.30, 'Tyre', 14)
    P.cyl((x + 0.19, c, WRAD), 'x', WRAD, 0.08, 'Tyre', 14, r2=WRAD - 0.08)
    P.cyl((x - 0.19, c, WRAD), 'x', WRAD - 0.08, 0.08, 'Tyre', 14, r2=WRAD)
    P.cyl((x + sx * 0.2, c, WRAD), 'x', 0.31, 0.08, 'Steel', 10)
    P.cyl((x + sx * 0.235, c, WRAD), 'x', 0.12, 0.05, 'Trim', 8)
    P.finish((x, c, WRAD))

# ---------------------------------------------------------------- light bar
# Hierarchy is kept one level deep: bake_space_transform mis-places grandchildren.
P = Part('LightBar')
P.box(-0.82, 0.82, 2.84, 3.16, Z_ROOF, Z_ROOF + 0.05, 'Trim')
P.box(-0.07, 0.07, 2.86, 3.14, Z_ROOF + 0.05, Z_ROOF + 0.2, 'Trim')
lb = P.finish((0, 3.0, Z_ROOF))
for name, x0, x1, m in (('LightBar_LampL', 0.07, 0.8, 'BeaconRed'), ('LightBar_LampR', -0.8, -0.07, 'BeaconBlue')):
    P = Part(name)
    P.box(x0, x1, 2.86, 3.14, Z_ROOF + 0.05, Z_ROOF + 0.2, m)
    P.finish(((x0 + x1) / 2, 3.0, Z_ROOF + 0.05), lb)

# ---------------------------------------------------------------- pump panel
XP = 0.92
PC = ((PF0 + PF1) / 2, (PZ0 + PZ1) / 2)
P = Part('PumpPanel')
P.box(XR, XP, PF0, PF1, PZ0, PZ1, 'Interior')
P.box(XP, W, PF0, PF0 + 0.015, PZ0, PZ1, 'Interior')
P.box(XP, W, PF1 - 0.015, PF1, PZ0, PZ1, 'Interior')
P.box(XP, W, PF0, PF1, PZ1 - 0.015, PZ1, 'Interior')
P.box(XP, W, PF0, PF1, PZ0, PZ0 + 0.015, 'Interior')
P.box(XP, XP + 0.05, 0.55, 1.0, 2.36, 2.43, 'Amber')
for f in (1.08, 0.84):
    P.cyl((XP + 0.02, f, 2.16), 'x', 0.1, 0.04, 'Trim', 12)
    P.cyl((XP + 0.045, f, 2.16), 'x', 0.08, 0.012, 'White', 12)
    P.prism([(f - 0.01, 2.16), (f + 0.01, 2.16), (f + 0.04, 2.21), (f + 0.03, 2.22)], XP + 0.051, XP + 0.057, 'Red')
for z in (1.93, 1.8):
    P.box(XP, XP + 0.02, 1.14, 1.22, z - 0.03, z + 0.03, 'Yellow')
    P.cyl((XP + 0.025, 1.04, z), 'x', 0.03, 0.05, 'Black', 8)
VALVE_F, VALVE_Z = 0.55, 1.95
OUT_F, OUT_Z = 1.0, 1.58
P.cyl((XP + 0.05, VALVE_F, (PZ0 + 2.28) / 2), 'z', 0.05, 2.28 - PZ0, 'Trim', 8)
P.cyl((XP + 0.05, 0.36, (PZ0 + 1.75) / 2), 'z', 0.035, 1.75 - PZ0, 'Trim', 8)
P.cyl((XP + 0.05, (VALVE_F + OUT_F) / 2, OUT_Z), 'f', 0.05, OUT_F - VALVE_F, 'Trim', 8)
P.box(XP, XP + 0.1, VALVE_F - 0.08, VALVE_F + 0.08, VALVE_Z - 0.08, VALVE_Z + 0.08, 'Trim')
P.cyl((XP + 0.11, VALVE_F, VALVE_Z), 'x', 0.03, 0.06, 'Steel', 8)
P.cyl((XP + 0.05, OUT_F, OUT_Z), 'x', 0.14, 0.1, 'Red', 10)
P.cyl((XP + 0.04, 0.36, 1.8), 'x', 0.06, 0.08, 'Trim', 8)
P.cyl((XP + 0.09, 0.36, 1.8), 'x', 0.045, 0.03, 'Yellow', 8)
panel = P.finish((W, PC[0], PC[1]))

VX = XP + 0.14
P = Part('Valve_Wheel')
P.ring((VX, VALVE_F, VALVE_Z), 0.17, 0.125, 0.035, 'Red', 12)
P.box(VX - 0.012, VX + 0.012, VALVE_F - 0.13, VALVE_F + 0.13, VALVE_Z - 0.02, VALVE_Z + 0.02, 'Red')
P.box(VX - 0.012, VX + 0.012, VALVE_F - 0.02, VALVE_F + 0.02, VALVE_Z - 0.13, VALVE_Z + 0.13, 'Red')
P.cyl((VX, VALVE_F, VALVE_Z), 'x', 0.045, 0.05, 'Red', 8)
P.finish((VX, VALVE_F, VALVE_Z), panel)

OX = XP + 0.1
P = Part('HoseOutlet')
P.cyl((OX + 0.06, OUT_F, OUT_Z), 'x', 0.09, 0.12, 'Steel', 10)
P.cyl((OX + 0.1, OUT_F, OUT_Z), 'x', 0.115, 0.03, 'Steel', 10)
P.box(OX + 0.085, OX + 0.115, OUT_F - 0.015, OUT_F + 0.015, OUT_Z - 0.15, OUT_Z + 0.15, 'Steel')
P.cyl((OX + 0.121, OUT_F, OUT_Z), 'x', 0.065, 0.004, 'Black', 10)
P.finish((OX, OUT_F, OUT_Z), panel)
empty('Anchor_Hose', (OX + 0.123, OUT_F, OUT_Z), panel)

# ---------------------------------------------------------------- equipment bay
XB = XR + 0.02
XSH = 1.02
CFC = (CF0 + CF1) / 2
SHELF_Z = 1.83
P = Part('Compartment')
P.box(XR, XB, CF0, CF1, CZ0, CZ1, 'Interior')
P.box(XB, 1.1, CF0, CF0 + 0.015, CZ0, CZ1, 'Interior')
P.box(XB, 1.1, CF1 - 0.015, CF1, CZ0, CZ1, 'Interior')
P.box(XB, W, CF0, CF1, CZ1 - 0.015, CZ1, 'Interior')
P.box(XB, W, CF0, CF1, CZ0, CZ0 + 0.015, 'Interior')
# shelves are left empty for the game's own pickups
P.box(XB, XSH, CF0, CF1, SHELF_Z, SHELF_Z + 0.03, 'Shelf')
P.box(XB, XB + 0.04, -2.2, -1.35, 2.25, 2.31, 'Amber')
comp = P.finish((W, CFC, (CZ0 + CZ1) / 2))

# ---------------------------------------------------------------- shutter
XS = 1.16
SF0, SF1 = CF0 + 0.055, CF1 - 0.055
P = Part('Shutter')
P.box(1.1, W, CF0, CF0 + 0.05, CZ0, CZ1, 'Steel')
P.box(1.1, W, CF1 - 0.05, CF1, CZ0, CZ1, 'Steel')
shutter = P.finish((XS, CFC, CZ1))
slats = []
for k in range(SLATS):
    zt = CZ1 - k * SLAT_H
    zb = zt - SLAT_H
    P = Part('Shutter_Slat_%02d' % (k + 1))
    # lip at the back overlaps the next slat so no gap shows between lamellae
    P.prism([(XS - 0.008, zt), (XS, zt), (XS + 0.018, zt - 0.03), (XS + 0.018, zb + 0.03),
             (XS, zb), (XS - 0.011, zb - 0.006)], SF0, SF1, 'Steel', axis='f')
    slats.append(P.finish((XS, CFC, zt), shutter))
P = Part('Shutter_Bar')
P.box(XS - 0.012, XS + 0.02, SF0, SF1, CZ0, CZ0 + BAR_H, 'Trim')
P.box(XS + 0.02, XS + 0.05, CFC - 0.2, CFC + 0.2, CZ0 + 0.015, CZ0 + 0.045, 'Black')
bar = P.finish((XS, CFC, CZ0 + BAR_H), shutter)

# Roll-up: every slat rides the side guides straight up, turns inward inside the header
# and runs under the roof, hidden inside the body. The bar and last slats stay in view.
VISIBLE_SLATS = 2
TRAVEL = CZ1 - CZ0 - BAR_H - VISIBLE_SLATS * SLAT_H
TURN_Z, TURN_R = CZ1 + 0.03, 0.1
U_TURN = TURN_Z - CZ0
U_ARC = TURN_R * math.pi / 2


def track(u):
    if u <= U_TURN:
        return Vector((XS, CZ0 + u))
    if u <= U_TURN + U_ARC:
        a = (u - U_TURN) / TURN_R
        return Vector((XS - TURN_R + TURN_R * math.cos(a), TURN_Z + TURN_R * math.sin(a)))
    return Vector((XS - TURN_R - (u - U_TURN - U_ARC), TURN_Z + TURN_R))


for fr in range(1, FRAMES + 1):
    s = (fr - 1) / (FRAMES - 1)
    t = TRAVEL * s * s * (3 - 2 * s)
    for k, ob in enumerate(slats):
        u = CZ1 - k * SLAT_H - CZ0 + t
        top = track(u)
        d = (track(u - SLAT_H) - top).normalized()
        ob.location = (top.x - XS, 0, top.y - CZ1)
        ob.rotation_euler = (0, math.atan2(-d.x, -d.y), 0)
        ob.keyframe_insert('location', frame=fr)
        ob.keyframe_insert('rotation_euler', frame=fr)
    bar.location = (0, 0, CZ0 + BAR_H - CZ1 + t)
    bar.keyframe_insert('location', frame=fr)
scene.frame_set(1)

# ---------------------------------------------------------------- output
tris = sum(len(p.vertices) - 2 for o in scene.objects if o.type == 'MESH' for p in o.data.polygons)
print('TRIS', tris, 'OBJECTS', len(scene.objects))
lo = [min(WORLD[o.name][i] + v.co[i] for o in scene.objects if o.type == 'MESH' for v in o.data.vertices) for i in range(3)]
hi = [max(WORLD[o.name][i] + v.co[i] for o in scene.objects if o.type == 'MESH' for v in o.data.vertices) for i in range(3)]
print('BOUNDS', [round(x, 3) for x in lo], [round(x, 3) for x in hi])

if OUT_BLEND:
    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
if OUT_FBX:
    bpy.ops.export_scene.fbx(
        filepath=OUT_FBX, use_selection=False, object_types={'MESH', 'EMPTY'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True,
        axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', use_mesh_modifiers=True,
        bake_anim=True, bake_anim_use_all_bones=False, bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=False, bake_anim_force_startend_keying=False,
        bake_anim_step=1, bake_anim_simplify_factor=1.0, add_leaf_bones=False)

if RENDER:
    scene.render.engine = 'BLENDER_WORKBENCH'
    sh = scene.display.shading
    sh.light = 'STUDIO'
    sh.color_type = 'MATERIAL'
    sh.show_cavity = True
    sh.show_shadows = True
    scene.render.resolution_x, scene.render.resolution_y = 1000, 640
    cd = bpy.data.cameras.new('cam'); cam = bpy.data.objects.new('cam', cd)
    scene.collection.objects.link(cam); scene.camera = cam

    def shot(name, target, offset, frame, lens=60):
        scene.frame_set(frame)
        cd.lens = lens
        t = Vector(target)
        cam.location = t + Vector(offset)
        cam.rotation_euler = (t - cam.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = RENDER + '_' + name + '.png'
        bpy.ops.render.render(write_still=True)

    mid = (0, 0, 1.5)
    views = {
        'persp_open': (mid, (9.5, -11, 4.5), FRAMES, 60),
        'side_closed': (mid, (17, 0, 0.3), 1, 60),
        'bays_open': ((1.2, 0.9, 1.9), (5.2, -1.2, 0.9), FRAMES, 45),
        'bays_half': ((1.2, 1.6, 1.9), (4.2, 0.6, 0.6), 16, 45),
        'front': (mid, (0, -17, 0.3), 1, 60),
        'back': (mid, (-5, 15, 3.5), 1, 60),
    }
    for n in (argv[3].split(',') if len(argv) > 3 else views):
        shot(n, *views[n])
