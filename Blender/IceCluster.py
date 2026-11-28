import bpy, bmesh, math, random, sys
from mathutils import Vector, Matrix

OUT_BLEND, OUT_FBX = sys.argv[sys.argv.index('--') + 1:][:2]
bpy.ops.wm.read_factory_settings(use_empty=True)
random.seed(67)

mat = bpy.data.materials.new('Ice')
mat.diffuse_color = (0.6, 0.85, 1.0, 1)

bm = bmesh.new()
spikes = [(0.0, 0.0, 0.085, 0.62, 0.0)]
for k in range(6):
    angle = math.radians(60 * k + random.uniform(-18, 18))
    spikes.append((angle, random.uniform(0.07, 0.15), random.uniform(0.04, 0.065), random.uniform(0.22, 0.45), random.uniform(14, 34)))

for angle, offset, radius, height, tilt in spikes:
    out = Vector((math.cos(angle), math.sin(angle), 0))
    axis = Vector((-out.y, out.x, 0))
    lean = Matrix.Rotation(math.radians(tilt), 4, axis) if tilt else Matrix.Identity(4)
    base = Matrix.Translation(out * offset) @ lean @ Matrix.Rotation(random.uniform(0, math.pi), 4, 'Z')
    # short faceted shaft, then a sharp tip; sunk a little so the base never floats
    shaft = height * 0.45
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=6, radius1=radius, radius2=radius * 0.8,
                          depth=shaft + 0.06, matrix=base @ Matrix.Translation((0, 0, shaft / 2 - 0.03)))
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=6, radius1=radius * 0.8, radius2=0.002,
                          depth=height - shaft, matrix=base @ Matrix.Translation((0, 0, shaft + (height - shaft) / 2)))

mesh = bpy.data.meshes.new('IceCluster')
bm.to_mesh(mesh)
bm.free()
mesh.materials.append(mat)
obj = bpy.data.objects.new('IceCluster', mesh)
scene = bpy.data.scenes[0]
scene.collection.objects.link(obj)
bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
bpy.ops.export_scene.fbx(filepath=OUT_FBX, object_types={'MESH'}, use_selection=False,
                         apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False)
print('TRIS', sum(len(p.vertices) - 2 for p in mesh.polygons))
