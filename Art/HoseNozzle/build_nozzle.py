# Procedural fire hose nozzle for Flame & Blame.
# blender -b --python build_nozzle.py -- <out.blend> <out.fbx> [render_prefix]
# Authoring space matches the Hose_Nozzle prefab: a = along the jet (Unity +X), u = up (Unity +Y),
# s = side (Unity +Z). Stored in Blender as (-a, -s, u); bake_space_transform turns that back.
import bpy, bmesh, sys, math
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT_BLEND = argv[0] if len(argv) > 0 else None
OUT_FBX = argv[1] if len(argv) > 1 else None
RENDER = argv[2] if len(argv) > 2 else None

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
MATS = {}


def srgb(h):
    def lin(c):
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return tuple(lin(int(h[i:i + 2], 16) / 255) for i in (0, 2, 4)) + (1.0,)


def mat(name, hexc, rough=0.8):
    m = bpy.data.materials.new('M_Nozzle_' + name)
    try:
        m.use_nodes = True
    except Exception:
        pass
    col = srgb(hexc)
    m.diffuse_color = col
    b = m.node_tree.nodes.get('Principled BSDF')
    b.inputs['Base Color'].default_value = col
    b.inputs['Roughness'].default_value = rough
    MATS[name] = m


mat('Red', 'E2202E')
mat('Steel', 'A4A9B2', rough=0.6)
mat('SteelDark', '7B808A', rough=0.6)
mat('Body', '3C3C46')
mat('Grip', '2B2B32', rough=0.95)


def B(a, u, s):
    return Vector((-a, -s, u))


class Part:
    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.mats = []

    def _tag(self, faces, m):
        if m not in self.mats:
            self.mats.append(m)
        for f in faces:
            f.material_index = self.mats.index(m)

    def _vfaces(self, verts):
        return list({f for v in verts for f in v.link_faces})

    def box(self, a0, a1, u0, u1, s0, s1, m):
        c = B((a0 + a1) / 2, (u0 + u1) / 2, (s0 + s1) / 2)
        mx = Matrix.Translation(c) @ Matrix.Diagonal((abs(a1 - a0), abs(s1 - s0), abs(u1 - u0), 1))
        self._tag(self._vfaces(bmesh.ops.create_cube(self.bm, size=1.0, matrix=mx)['verts']), m)

    def tube(self, a0, a1, r0, r1, m, segs=12, u=0.0, s=0.0, spin=0.0):
        mx = Matrix.Translation(B((a0 + a1) / 2, u, s)) @ Matrix.Rotation(math.pi / 2, 4, 'Y') @ Matrix.Rotation(spin, 4, 'Z')
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=segs,
                                    radius1=r1, radius2=r0, depth=abs(a1 - a0), matrix=mx)
        self._tag(self._vfaces(res['verts']), m)

    def disc(self, a, u, s0, s1, r, m, segs=10):
        mx = Matrix.Translation(B(a, u, (s0 + s1) / 2)) @ Matrix.Rotation(math.pi / 2, 4, 'X')
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=segs,
                                    radius1=r, radius2=r, depth=abs(s1 - s0), matrix=mx)
        self._tag(self._vfaces(res['verts']), m)

    def prism(self, prof, s0, s1, m):
        rings = [[self.bm.verts.new(B(a, u, s)) for a, u in prof] for s in (s0, s1)]
        n = len(prof)
        faces = [self.bm.faces.new(rings[0]), self.bm.faces.new(rings[1][::-1])]
        for i in range(n):
            faces.append(self.bm.faces.new((rings[0][i], rings[1][i], rings[1][(i + 1) % n], rings[0][(i + 1) % n])))
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        self._tag(faces, m)

    def finish(self, origin=(0, 0, 0), parent=None):
        o = B(*origin)
        bmesh.ops.translate(self.bm, verts=self.bm.verts, vec=-o)
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me)
        self.bm.free()
        for m in self.mats:
            me.materials.append(MATS[m])
        ob = bpy.data.objects.new(self.name, me)
        scene.collection.objects.link(ob)
        if parent:
            ob.parent = parent
            ob.location = o - parent.location
        else:
            ob.location = o
        return ob


# ---------------------------------------------------------------- dimensions
AXIS = 0.45                   # height of the jet axis in the prefab
BACK, MOUTH = -0.19, 0.225    # hose socket end and jet mouth
PIVOT = (-0.035, AXIS)        # lever hinge

# ---------------------------------------------------------------- body
P = Part('Nozzle')
P.tube(BACK, -0.15, 0.048, 0.048, 'Steel', 12, AXIS)
P.tube(-0.15, -0.105, 0.064, 0.064, 'Steel', 8, AXIS, spin=math.pi / 8)
P.tube(-0.105, -0.09, 0.05, 0.05, 'SteelDark', 12, AXIS)
for sign in (1, -1):
    P.box(BACK - 0.012, -0.135, AXIS + sign * 0.05, AXIS + sign * 0.078, -0.02, 0.02, 'SteelDark')
P.tube(-0.09, 0.07, 0.043, 0.043, 'Body', 8, AXIS, spin=math.pi / 8)
P.box(-0.075, 0.01, AXIS - 0.04, AXIS + 0.045, -0.046, 0.046, 'Body')
P.tube(0.115, 0.132, 0.046, 0.046, 'Body', 12, AXIS)
P.tube(0.132, MOUTH, 0.05, 0.082, 'Steel', 12, AXIS)
P.tube(MOUTH - 0.014, MOUTH, 0.087, 0.087, 'SteelDark', 12, AXIS)
P.tube(MOUTH, MOUTH + 0.002, 0.07, 0.07, 'Body', 12, AXIS)
P.tube(MOUTH + 0.002, MOUTH + 0.008, 0.03, 0.03, 'Steel', 10, AXIS)
for i in range(8):
    ang = math.pi * i / 4
    mx = Matrix.Translation(B(MOUTH + 0.004, AXIS, 0)) @ Matrix.Rotation(ang, 4, 'X') @ \
        Matrix.Translation((0, 0, 0.048)) @ Matrix.Diagonal((0.004, 0.012, 0.036, 1))
    P._tag(P._vfaces(bmesh.ops.create_cube(P.bm, size=1.0, matrix=mx)['verts']), 'SteelDark')

# pistol grip
GT, GB = AXIS - 0.035, 0.275
P.prism([(-0.05, GT), (0.03, GT), (0.022, 0.375), (0.006, 0.36), (-0.002, GB), (-0.078, GB), (-0.066, 0.36)], -0.023, 0.023, 'Grip')
for k in range(3):
    u = 0.34 - k * 0.022
    a = -0.003 - (0.36 - u) * 0.1
    P.box(a - 0.004, a + 0.006, u - 0.007, u + 0.007, -0.025, 0.025, 'Body')
P.prism([(0.002, GB), (-0.082, GB), (-0.088, GB - 0.022), (0.004, GB - 0.022)], -0.027, 0.027, 'Red')
nozzle = P.finish()

# ---------------------------------------------------------------- adjusting ring
P = Part('Ring')
P.tube(0.07, 0.115, 0.054, 0.054, 'Red', 12, AXIS)
P.tube(0.079, 0.106, 0.061, 0.061, 'Red', 6, AXIS)
P.tube(0.079, 0.106, 0.061, 0.061, 'Red', 6, AXIS, spin=math.pi / 6)
P.finish((0.0925, AXIS, 0), nozzle)

# ---------------------------------------------------------------- lever
P = Part('Lever')
pa, pu = PIVOT
for sign in (1, -1):
    s0, s1 = sign * 0.046, sign * 0.058
    P.disc(pa, pu, s0, s1, 0.03, 'Red')
    P.disc(pa, pu, sign * 0.058, sign * 0.062, 0.015, 'Steel', 8)
    P.prism([(pa - 0.022, pu + 0.012), (pa + 0.02, pu - 0.008), (0.045, 0.535), (0.012, 0.556)], min(s0, s1), max(s0, s1), 'Red')
P.prism([(0.012, 0.556), (0.045, 0.535), (0.14, 0.548), (0.136, 0.57), (0.03, 0.572)], -0.058, 0.058, 'Red')
for k in range(3):
    a = 0.075 + k * 0.02
    P.box(a, a + 0.008, 0.566, 0.574, -0.04, 0.04, 'Body')
P.finish((pa, pu, 0), nozzle)

tris = sum(len(p.vertices) - 2 for o in scene.objects if o.type == 'MESH' for p in o.data.polygons)
print('TRIS', tris)
if OUT_BLEND:
    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
if OUT_FBX:
    bpy.ops.export_scene.fbx(
        filepath=OUT_FBX, use_selection=False, object_types={'MESH', 'EMPTY'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True,
        axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', bake_anim=False, add_leaf_bones=False)

if RENDER:
    scene.render.engine = 'BLENDER_WORKBENCH'
    sh = scene.display.shading
    sh.light = 'STUDIO'; sh.color_type = 'MATERIAL'; sh.show_cavity = True
    scene.render.resolution_x, scene.render.resolution_y = 900, 600
    scene.render.film_transparent = False
    scene.world = bpy.data.worlds.new('w'); scene.world.color = (0.8, 0.8, 0.8)
    cd = bpy.data.cameras.new('cam'); cam = bpy.data.objects.new('cam', cd)
    scene.collection.objects.link(cam); scene.camera = cam; cd.lens = 70
    t = B(0.0, 0.43, 0)
    for name, off in (('persp', B(0.75, 0.25, 0.75)), ('side', B(0, 0, 1.1))):
        cam.location = t + off
        cam.rotation_euler = (t - cam.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = RENDER + '_' + name + '.png'
        bpy.ops.render.render(write_still=True)
