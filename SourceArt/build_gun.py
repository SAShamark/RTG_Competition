import bpy, bmesh, math, sys, os
from mathutils import Matrix, Vector

args = sys.argv[sys.argv.index("--") + 1:]
MESH_DIR, BLEND_PATH, RENDER_DIR = args[0], args[1], args[2]

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene


def make_mat(name, color, emission=0.0, rough=0.6):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (*color, 1)
    b.inputs["Roughness"].default_value = rough
    if emission > 0:
        b.inputs["Emission Color"].default_value = (*color, 1)
        b.inputs["Emission Strength"].default_value = emission
    m.diffuse_color = (*color, 1)
    return m


YELLOW = make_mat("Gun_Yellow", (1.0, 0.56, 0.02))
DARK = make_mat("Gun_Dark", (0.04, 0.04, 0.05))
RED = make_mat("Gun_Red", (0.8, 0.04, 0.03))
GLOW = make_mat("Gun_Glow", (0.1, 0.85, 1.0), 3.0)
SCREEN = make_mat("Gun_Screen", (0.01, 0.03, 0.06), rough=0.2)

ELEMENTS = {
    "Fire": ((1.0, 0.22, 0.02), (1.0, 0.75, 0.1)),
    "Water": ((0.02, 0.2, 1.0), (0.3, 0.75, 1.0)),
    "Ice": ((0.1, 0.8, 1.0), (0.8, 1.0, 1.0)),
    "Lightning": ((0.35, 0.08, 0.9), (1.0, 0.8, 0.1)),
}
CORE = {k: make_mat("Core_" + k, v[0], 0.5, 0.25) for k, v in ELEMENTS.items()}
ICON = {k: make_mat("Icon_" + k, v[1], 1.5) for k, v in ELEMENTS.items()}

# local space is (x, u, h): x = width, u = along the gun (muzzle -> stock), h = up
M_SIDE = Matrix(((0, 0, 1, 0), (1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
M_FRONT = Matrix(((-1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
M_AXIS = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
IDENT = Matrix.Identity(4)


class Builder:
    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.mats = []

    def _commit(self, t, mat, bevel):
        bmesh.ops.recalc_face_normals(t, faces=t.faces[:])
        if bevel > 0:
            bmesh.ops.bevel(t, geom=t.edges[:], offset=bevel, offset_type='OFFSET',
                            segments=1, profile=0.5, affect='EDGES', clamp_overlap=True)
            bmesh.ops.recalc_face_normals(t, faces=t.faces[:])
        if mat not in self.mats:
            self.mats.append(mat)
        idx = self.mats.index(mat)
        for f in t.faces:
            f.material_index = idx
        me = bpy.data.meshes.new("tmp")
        t.to_mesh(me)
        t.free()
        self.bm.from_mesh(me)
        bpy.data.meshes.remove(me)

    def _loft(self, lo_pts, hi_pts, mat, bevel):
        t = bmesh.new()
        lo = [t.verts.new(p) for p in lo_pts]
        hi = [t.verts.new(p) for p in hi_pts]
        n = len(lo)
        t.faces.new(lo)
        t.faces.new(hi[::-1])
        for i in range(n):
            j = (i + 1) % n
            t.faces.new((lo[i], hi[i], hi[j], lo[j]))
        self._commit(t, mat, bevel)

    def poly(self, pts, c0, c1, mat, M, bevel=0.0):
        self._loft([M @ Vector((a, b, c0)) for a, b in pts],
                   [M @ Vector((a, b, c1)) for a, b in pts], mat, bevel)

    def side(self, pts_uv, width, mat, bevel=0.0, xf=IDENT):
        self.poly([(u, -v) for u, v in pts_uv], -width / 2, width / 2, mat, xf @ M_SIDE, bevel)

    def box(self, x0, x1, u0, u1, h0, h1, mat, bevel=0.0, xf=IDENT):
        self.poly([(u0, h0), (u1, h0), (u1, h1), (u0, h1)], x0, x1, mat, xf @ M_SIDE, bevel)

    def frustum(self, n, r0, r1, c0, c1, mat, M, bevel=0.0):
        ring = [(math.cos(math.tau * (i + 0.5) / n), math.sin(math.tau * (i + 0.5) / n)) for i in range(n)]
        self._loft([M @ Vector((a * r0, b * r0, c0)) for a, b in ring],
                   [M @ Vector((a * r1, b * r1, c1)) for a, b in ring], mat, bevel)

    def finish(self, W, pivot=None):
        for v in self.bm.verts:
            v.co = W @ v.co
        loc = Vector((0, 0, 0))
        if pivot is not None:
            loc = W @ Vector(pivot)
            for v in self.bm.verts:
                v.co -= loc
        bmesh.ops.triangulate(self.bm, faces=self.bm.faces[:])
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me)
        self.bm.free()
        for m in self.mats:
            me.materials.append(m)
        obj = bpy.data.objects.new(self.name, me)
        obj.location = loc
        scene.collection.objects.link(obj)
        return obj


def rect(cx, cy, length, width, ang):
    ca, sa = math.cos(ang), math.sin(ang)
    out = []
    for lx, ly in ((-length / 2, -width / 2), (length / 2, -width / 2), (length / 2, width / 2), (-length / 2, width / 2)):
        out.append((cx + lx * ca - ly * sa, cy + lx * sa + ly * ca))
    return out


def icon_shapes(kind):
    if kind == "Water":
        return [([(0, 1), (0.35, 0.35), (0.6, -0.1), (0.62, -0.45), (0.45, -0.8), (0, -1),
                  (-0.45, -0.8), (-0.62, -0.45), (-0.6, -0.1), (-0.35, 0.35)], 0)]
    if kind == "Lightning":
        return [([(0.1, 1), (-0.6, -0.1), (-0.05, -0.1), (-0.25, -1), (0.6, 0.25), (0.05, 0.25), (0.5, 1)], 0)]
    if kind == "Fire":
        return [([(0, -1), (-0.55, -0.8), (-0.8, -0.3), (-0.65, 0.2), (-0.45, 0.0), (-0.3, 0.6), (0.05, 1.0),
                  (0.1, 0.5), (0.4, 0.75), (0.5, 0.3), (0.8, -0.2), (0.6, -0.75)], 0),
                ([(0, -0.9), (-0.3, -0.7), (-0.35, -0.3), (-0.1, 0.05), (0.0, -0.2), (0.25, 0.1),
                  (0.35, -0.4), (0.25, -0.75)], 1)]
    shapes = []
    for k in range(3):
        shapes.append((rect(0, 0, 2.0, 0.2, math.radians(90 + 60 * k)), 0))
    for k in range(6):
        th = math.radians(90 + 60 * k)
        px, py = 0.55 * math.cos(th), 0.55 * math.sin(th)
        for d in (-50, 50):
            a = th + math.radians(d)
            shapes.append((rect(px + 0.2 * math.cos(a), py + 0.2 * math.sin(a), 0.42, 0.16, a), 0))
    return shapes


def add_icon(b, kind, M, s, c0, c1, mat, mat_inner):
    step = (c1 - c0) * 0.4
    for pts, layer in icon_shapes(kind):
        b.poly([(x * s, y * s) for x, y in pts], c0, c1 + layer * step, mat_inner if layer else mat, M)


# ---------------- gun (profile coords traced from the side view, px) ----------------
S = 0.62 / 475.0
W_GUN = Matrix(((S, 0, 0, 0), (0, S, 0, -300 * S), (0, 0, S, 130 * S), (0, 0, 0, 1)))

g = Builder("Gun_Body")
g.side([(26, 64), (38, 50), (190, 48), (204, 62), (190, 108), (26, 108)], 50, YELLOW, 3)
for slot in ([(48, 74), (62, 74), (58, 81), (44, 81)], [(68, 74), (82, 74), (78, 81), (64, 81)],
             [(90, 74), (152, 74), (148, 81), (86, 81)]):
    g.side(slot, 51, DARK)
g.side([(30, 108), (186, 108), (186, 140), (176, 146), (40, 146), (28, 134)], 38, DARK, 2)
g.side([(48, 118), (108, 118), (114, 126), (106, 135), (48, 135)], 40, RED)
g.frustum(8, 31, 31, 8, 30, DARK, Matrix.Translation((0, 0, -90)) @ M_FRONT, 2)
g.frustum(8, 23, 23, 3, 9, GLOW, Matrix.Translation((0, 0, -90)) @ M_FRONT, 1)

g.side([(186, 58), (392, 60), (400, 72), (392, 106), (180, 106)], 40, DARK, 2)
for i in range(5):
    g.box(-17, 17, 290 + i * 8, 295 + i * 8, -60, -54, DARK)
g.side([(326, 57), (386, 58), (392, 70), (384, 83), (330, 81)], 44, RED, 1)
g.box(-21, 21, 210, 246, -73, -64, GLOW)
g.side([(258, 62), (274, 62), (262, 86), (246, 86)], 42, YELLOW)

g.side([(182, 104), (348, 104), (352, 126), (340, 148), (276, 148), (270, 134), (236, 134), (226, 146), (192, 146)],
       36, YELLOW, 2)
g.side([(188, 140), (204, 140), (236, 194), (292, 200), (292, 214), (228, 208)], 16, DARK, 2)
g.side([(280, 140), (338, 140), (346, 170), (340, 224), (298, 224), (290, 180)], 30, DARK, 4)
g.side([(290, 218), (344, 218), (340, 232), (318, 244), (294, 236)], 32, YELLOW, 2)

g.side([(388, 62), (450, 64), (458, 72), (452, 84), (430, 84), (400, 104), (388, 104)], 26, DARK, 2)
g.side([(428, 70), (476, 66), (486, 96), (474, 200), (446, 206), (436, 112)], 34, YELLOW, 3)
g.side([(444, 96), (474, 90), (478, 104), (468, 186), (450, 190), (444, 110)], 38, RED, 1)

g.side([(204, 50), (212, 30), (236, 16), (250, 16), (252, 50)], 14, YELLOW, 2)
g.box(-18, 18, 258, 318, -52, -40, DARK, 2)
XF_SCREEN = Matrix.Translation((0, 276, -28)) @ Matrix.Rotation(math.radians(25), 4, 'X')
g.box(-28, 28, 0, 22, -20, 20, YELLOW, 3, XF_SCREEN)
g.box(-24, 24, 20, 24, -16, 16, DARK, 1, XF_SCREEN)
g.box(-20, 20, 23, 25.5, -12, 12, SCREEN, 0, XF_SCREEN)
gun_body = g.finish(W_GUN)

t = Builder("Gun_Trigger")
t.side([(244, 134), (256, 134), (254, 150), (260, 162), (252, 164), (244, 150)], 8, YELLOW, 1)
gun_trigger = t.finish(W_GUN, pivot=(0, 250, -134))

gun_icons = {}
for kind in ELEMENTS:
    b = Builder("Gun_ScreenIcon_" + kind)
    add_icon(b, kind, XF_SCREEN @ M_FRONT, 10, 25.5, 27, ICON[kind], CORE[kind])
    gun_icons[kind] = b.finish(W_GUN)

# ---------------- cartridges (mm) ----------------
W_MM = Matrix.Scale(0.001, 4)
cartridges = {}
for kind in ELEMENTS:
    c = Builder("Cartridge_" + kind)
    c.box(-13, 13, -15, 15, -13, 13, CORE[kind], 3)
    for sgn in (1, -1):
        M = Matrix.Scale(sgn, 4, (0, 1, 0)) @ M_AXIS
        c.frustum(12, 17, 17, 15, 21, RED, M, 1)
        c.frustum(8, 15, 15, 21, 33, DARK, M, 4)
    for k in range(4):
        add_icon(c, kind, Matrix.Rotation(math.radians(90 * k), 4, 'Y') @ M_SIDE, 8.5, 13, 14,
                 ICON[kind], CORE[kind])
    cartridges[kind] = c.finish(W_MM)


def export(path, objs):
    for o in scene.objects:
        o.select_set(False)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options='FBX_SCALE_ALL',
                             bake_space_transform=True, object_types={'MESH'}, mesh_smooth_type='FACE',
                             add_leaf_bones=False, bake_anim=False)


export(os.path.join(MESH_DIR, "ElementGun.fbx"), [gun_body, gun_trigger] + list(gun_icons.values()))
for kind, obj in cartridges.items():
    export(os.path.join(MESH_DIR, "ElementCartridge_" + kind + ".fbx"), [obj])

for i, obj in enumerate(cartridges.values()):
    obj.location = (0, -0.12 + 0.08 * i, -0.25)
for kind, obj in gun_icons.items():
    obj.hide_render = kind != "Ice"
    obj.hide_viewport = kind != "Ice"

# ---------------- preview render ----------------
world = bpy.data.worlds.new("World")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.6, 0.6, 0.62, 1)
scene.world = world
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN'))
sun.data.energy = 3
sun.rotation_euler = (math.radians(50), 0, math.radians(60))
scene.collection.objects.link(sun)
cam = bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera"))
scene.collection.objects.link(cam)
scene.camera = cam
try:
    scene.render.engine = 'BLENDER_EEVEE'
except TypeError:
    scene.render.engine = 'BLENDER_EEVEE_NEXT'
scene.view_settings.view_transform = 'Standard'
scene.render.resolution_x, scene.render.resolution_y = 1100, 620

bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)


def shot(name, pos, target):
    cam.location = pos
    cam.rotation_euler = (Vector(target) - Vector(pos)).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = os.path.join(RENDER_DIR, name)
    bpy.ops.render.render(write_still=True)


shot("gun.png", (1.05, -0.5, 0.3), (0, -0.08, -0.03))
shot("cartridges.png", (0.36, -0.08, -0.13), (0, 0, -0.25))
print("BUILD_OK")
