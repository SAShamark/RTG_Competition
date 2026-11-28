import bpy, bmesh, math, random, sys
from mathutils import Vector, Matrix

OUT = sys.argv[sys.argv.index('--') + 1]
random.seed(67)


def poly(bm, pts, hint):
    f = bm.faces.new([bm.verts.new(p) for p in pts])
    f.normal_update()
    if f.normal.dot(hint) < 0:
        f.normal_flip()


def spike(bm, origin, direction, length, width, sides=4, belly=0.25, flat=1.0):
    direction = direction.normalized()
    frame = direction.to_track_quat('Z', 'Y').to_matrix() @ Matrix.Rotation(random.uniform(0, math.pi), 3, 'Z')
    mid = origin + direction * (length * belly)
    tip = origin + direction * length
    ring = []
    for k in range(sides):
        a = 2 * math.pi * k / sides
        ring.append(mid + frame @ Vector((math.cos(a) * width, math.sin(a) * width * flat, 0)))
    for k in range(sides):
        a, b = ring[k], ring[(k + 1) % sides]
        out = (a + b) / 2 - mid
        poly(bm, [origin, a, b], out - direction * width)
        poly(bm, [a, b, tip], out)


def fibonacci(count):
    for i in range(count):
        y = 1 - 2 * (i + 0.5) / count
        r = math.sqrt(1 - y * y)
        a = i * 2.39996323
        yield Vector((math.cos(a) * r, y, math.sin(a) * r))


def build_burst(bm):
    for d in fibonacci(14):
        d = (d + Vector((random.uniform(-0.25, 0.25), random.uniform(-0.25, 0.25), random.uniform(-0.25, 0.25)))).normalized()
        length = random.uniform(0.8, 1.55)
        spike(bm, Vector((0, 0, 0)), d, length, length * random.uniform(0.09, 0.14), flat=random.uniform(0.45, 0.9))
    for _ in range(16):
        d = Vector((random.uniform(-1, 1), random.uniform(-1, 1), random.uniform(-1, 1))).normalized()
        length = random.uniform(0.35, 0.75)
        spike(bm, Vector((0, 0, 0)), d, length, length * random.uniform(0.12, 0.2), flat=random.uniform(0.5, 1.0))


def build_shard(bm):
    spike(bm, Vector((0, 0, -0.1)), Vector((0, 0, 1)), 0.42, 0.07, sides=4, belly=0.3, flat=0.3)


def build_icicles(bm):
    for _ in range(9):
        a, r = random.uniform(0, 2 * math.pi), random.uniform(0, 0.22)
        length = random.uniform(0.22, 0.8)
        lean = Matrix.Rotation(math.radians(random.uniform(-7, 7)), 4, 'X') @ Matrix.Rotation(math.radians(random.uniform(-7, 7)), 4, 'Y')
        base = Matrix.Translation((math.cos(a) * r, math.sin(a) * r, 0)) @ lean
        radius = random.uniform(0.03, 0.065)
        bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=5, radius1=radius, radius2=0.003,
                              depth=length + 0.04, matrix=base @ Matrix.Translation((0, 0, length / 2 - 0.02)))


def build_crystals(bm):
    spikes = [(0.0, 0.0, 0.1, 0.85, 0.0)]
    for k in range(7):
        angle = math.radians(51 * k + random.uniform(-18, 18))
        spikes.append((angle, random.uniform(0.08, 0.2), random.uniform(0.045, 0.08), random.uniform(0.3, 0.65), random.uniform(14, 38)))
    for angle, offset, radius, height, tilt in spikes:
        out = Vector((math.cos(angle), math.sin(angle), 0))
        lean = Matrix.Rotation(math.radians(tilt), 4, Vector((-out.y, out.x, 0))) if tilt else Matrix.Identity(4)
        base = Matrix.Translation(out * offset) @ lean @ Matrix.Rotation(random.uniform(0, math.pi), 4, 'Z')
        shaft = height * 0.5
        bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=6, radius1=radius, radius2=radius * 0.85,
                              depth=shaft + 0.06, matrix=base @ Matrix.Translation((0, 0, shaft / 2 - 0.03)))
        bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=6, radius1=radius * 0.85, radius2=0.002,
                              depth=height - shaft, matrix=base @ Matrix.Translation((0, 0, shaft + (height - shaft) / 2)))


def clip(polygon, point, normal):
    result = []
    for i in range(len(polygon)):
        a, b = polygon[i], polygon[(i + 1) % len(polygon)]
        da, db = (a - point).dot(normal), (b - point).dot(normal)
        if da <= 0:
            result.append(a)
        if (da < 0) != (db < 0) and da != db:
            result.append(a + (b - a) * (da / (da - db)))
    return result


def build_plates(radius, count, low, high, gap=0.03):
    def build(bm):
        seeds = []
        while len(seeds) < count:
            a, r = random.uniform(0, 2 * math.pi), radius * 1.1 * math.sqrt(random.uniform(0, 1))
            p = Vector((math.cos(a) * r, math.sin(a) * r))
            if all((p - s).length > radius * 1.4 / math.sqrt(count) for s in seeds):
                seeds.append(p)
        big = radius * 3
        for i, s in enumerate(seeds):
            cell = [Vector((-big, -big)), Vector((big, -big)), Vector((big, big)), Vector((-big, big))]
            for j, other in enumerate(seeds):
                if i != j and cell:
                    cell = clip(cell, (s + other) / 2, other - s)
            if len(cell) < 3:
                continue
            centre = sum(cell, Vector((0, 0))) / len(cell)
            if centre.length > radius * random.uniform(0.78, 1.0) or max((p - centre).length for p in cell) > radius * 0.6:
                continue
            height = (high + (low - high) * min(1.0, centre.length / radius)) * random.uniform(0.7, 1.25)
            tilt = Matrix.Rotation(math.radians(random.uniform(-5, 5)), 3, 'X') @ Matrix.Rotation(math.radians(random.uniform(-5, 5)), 3, 'Y')
            lift = random.uniform(0.0, 0.03)

            def place(p, shrink, z):
                offset = p - centre
                offset = offset * max(0.0, 1 - shrink / max(offset.length, 1e-4))
                local = tilt @ Vector((offset.x, offset.y, z))
                return Vector((centre.x + local.x, centre.y + local.y, local.z + lift))

            bottom = [place(p, gap, -0.05) for p in cell]
            mid = [place(p, gap, height - 0.03) for p in cell]
            top = [place(p, gap + 0.035, height) for p in cell]
            mid3 = Vector((centre.x, centre.y, height / 2))
            poly(bm, top, Vector((0, 0, 1)))
            for k in range(len(cell)):
                k2 = (k + 1) % len(cell)
                out = (mid[k] + mid[k2]) / 2 - mid3
                out.z = 0
                poly(bm, [bottom[k], bottom[k2], mid[k2], mid[k]], out)
                poly(bm, [mid[k], mid[k2], top[k2], top[k]], out + Vector((0, 0, out.length)))
    return build


def export(name, build):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bm = bmesh.new()
    build(bm)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    tris = sum(len(f.verts) - 2 for f in bm.faces)
    bm.free()
    mesh.materials.append(bpy.data.materials.new('Ice'))
    obj = bpy.data.objects.new(name, mesh)
    bpy.data.scenes[0].collection.objects.link(obj)
    bpy.ops.export_scene.fbx(filepath=OUT + '/' + name + '.fbx', object_types={'MESH'}, use_selection=False,
                             apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                             bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False)
    print('MESH', name, tris)


export('IceBurst', build_burst)
export('IceShard', build_shard)
export('Icicles', build_icicles)
export('IceCluster', build_crystals)
export('IcePlates_Large', build_plates(1.7, 34, 0.05, 0.2))
export('IcePlates_Small', build_plates(0.8, 22, 0.04, 0.13))
