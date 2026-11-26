# Helpers for modelling facility props in Blender (exec'd by each bridge call).
import bpy, bmesh, math, os
from mathutils import Vector, Matrix

OUT = r"C:/Users/Admin/3D projects/My project/Assets/Art/Generated/Facility/Models"
os.makedirs(OUT, exist_ok=True)

# Material slots by name; Unity remaps them to the facility URP materials of the same role.
PALETTE = {
    "Metal_Dark":   ((0.10, 0.11, 0.12, 1), 0.8, 0.45, None),
    "Metal_Mid":    ((0.32, 0.33, 0.35, 1), 0.85, 0.35, None),
    "Paint_White":  ((0.62, 0.64, 0.66, 1), 0.1, 0.5, None),
    "Paint_Yellow": ((0.70, 0.48, 0.05, 1), 0.2, 0.5, None),
    "Rubber":       ((0.02, 0.02, 0.025, 1), 0.0, 0.8, None),
    "Glass":        ((0.6, 0.8, 0.85, 0.15), 0.0, 0.05, None),
    "Liquid":       ((0.05, 0.7, 0.45, 0.6), 0.0, 0.1, (0.05, 1.0, 0.55)),
    "Emit_Cyan":    ((0.05, 0.3, 0.4, 1), 0.0, 0.3, (0.2, 0.8, 1.0)),
    "Emit_Red":     ((0.3, 0.02, 0.02, 1), 0.0, 0.3, (1.0, 0.08, 0.04)),
    "Screen":       ((0.01, 0.015, 0.02, 1), 0.0, 0.1, None),
    "Grate":        ((0.18, 0.19, 0.2, 1), 0.8, 0.5, None),
    "Paint_Blue":   ((0.08, 0.18, 0.34, 1), 0.3, 0.5, None),
    "Paint_Red":    ((0.45, 0.05, 0.04, 1), 0.2, 0.5, None),
    "Cardboard":    ((0.45, 0.33, 0.2, 1), 0.0, 0.85, None),
    "Wood":         ((0.42, 0.32, 0.2, 1), 0.0, 0.8, None),
    "Wrap":         ((0.55, 0.58, 0.6, 1), 0.0, 0.25, None),
    # Horror pass (facility_horror.py)
    "Blood":        ((0.16, 0.01, 0.01, 1), 0.0, 0.15, None),
    "Blood_Dry":    ((0.07, 0.015, 0.01, 1), 0.0, 0.6, None),
    "Sheet":        ((0.6, 0.6, 0.56, 1), 0.0, 0.9, None),
    "Cloth":        ((0.22, 0.24, 0.2, 1), 0.0, 0.9, None),
    "Paper":        ((0.82, 0.8, 0.72, 1), 0.0, 0.9, None),
    "Bottle_Glass": ((0.06, 0.2, 0.09, 1), 0.0, 0.08, None),
    "Tin":          ((0.6, 0.61, 0.63, 1), 0.9, 0.35, None),
}


def clear():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes):
        if m.users == 0:
            bpy.data.meshes.remove(m)


def mat(name):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        col, metal, rough, emit = PALETTE[name]
        b = m.node_tree.nodes.get("Principled BSDF")
        b.inputs["Base Color"].default_value = col
        b.inputs["Metallic"].default_value = metal
        b.inputs["Roughness"].default_value = rough
        if col[3] < 1:
            b.inputs["Alpha"].default_value = col[3]
        if emit:
            b.inputs["Emission Color"].default_value = (*emit, 1)
            b.inputs["Emission Strength"].default_value = 3.0
    return m


def _finish(o, m, bevel=0.0, segments=2, smooth=False):
    o.data.materials.clear()
    o.data.materials.append(mat(m))
    if bevel > 0:
        mod = o.modifiers.new("bevel", "BEVEL")
        mod.width = bevel
        mod.segments = segments
        mod.limit_method = "ANGLE"
    if smooth:
        for p in o.data.polygons:
            p.use_smooth = True
    return o


def box(size, loc, m="Metal_Dark", bevel=0.0, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=[math.radians(a) for a in rot])
    o = bpy.context.active_object
    o.scale = size
    bpy.ops.object.transform_apply(scale=True)
    return _finish(o, m, bevel)


def cyl(r, depth, loc, m="Metal_Dark", verts=32, rot=(0, 0, 0), bevel=0.0, smooth=True):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=loc,
                                        rotation=[math.radians(a) for a in rot])
    o = bpy.context.active_object
    _finish(o, m, bevel, smooth=False)
    if smooth:
        # Smooth the sides only: auto smooth by angle keeps caps flat.
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35))
    return o


def torus(R, r, loc, m="Metal_Mid", rot=(0, 0, 0), major=48, minor=10):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, location=loc,
                                     rotation=[math.radians(a) for a in rot],
                                     major_segments=major, minor_segments=minor)
    o = bpy.context.active_object
    _finish(o, m)
    bpy.ops.object.shade_smooth()
    return o


def tube(points, r, m="Rubber", res=8):
    """A pipe/cable along a poly-bezier through the points."""
    cu = bpy.data.curves.new("tube", "CURVE")
    cu.dimensions = "3D"
    cu.bevel_depth = r
    cu.bevel_resolution = 2
    cu.resolution_u = res
    sp = cu.splines.new("BEZIER")
    sp.bezier_points.add(len(points) - 1)
    for bp, p in zip(sp.bezier_points, points):
        bp.co = p
        bp.handle_left_type = bp.handle_right_type = "AUTO"
    cu.use_fill_caps = True
    o = bpy.data.objects.new("tube", cu)
    bpy.context.collection.objects.link(o)
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.convert(target="MESH")
    o = bpy.context.active_object
    o.data.materials.clear()
    o.data.materials.append(mat(m))
    bpy.ops.object.shade_smooth()
    return o


def ring_of(n, radius, z, fn):
    out = []
    for i in range(n):
        a = 2 * math.pi * i / n
        out.append(fn(math.cos(a) * radius, math.sin(a) * radius, z, a))
    return out


def join(objs, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
        bpy.context.view_layer.objects.active = o
        for mod in list(o.modifiers):
            bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = bpy.context.active_object
    o.name = name
    o.data.name = name
    # Bake the first part's rotation/scale into the mesh: Unity sets the root's rotation when it
    # places a model, so a rotated root (Elec_CableDrum, whose first part was a turned cylinder)
    # lost its turn and sank half into the floor.
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    # The pivot is the world origin (the join keeps the first part's origin otherwise).
    bpy.context.scene.cursor.location = (0, 0, 0)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    # World-scaled box projection: one UV unit = 2 m, so tiling wear textures stay even.
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.cube_project(cube_size=2.0, correct_aspect=True, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    return o


def export(objs, name):
    """FBX for Unity: metres, Y up, transforms baked so the root has no rotation."""
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    path = os.path.join(OUT, name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_UNITS",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False,
                             object_types={"MESH"})
    return path
