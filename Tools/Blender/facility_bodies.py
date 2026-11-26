# Bodies, the breaker room door sized to its frame, and room dressing for the breaker room and the
# lab office (2026-11-11). Conventions as facility_assets.py. Run build_bodies() through the bridge.
# The sheet over the body is a real cloth simulation dropped onto a metaball mannequin.
exec(open(r"C:/Users/Admin/3D projects/My project/Tools/Blender/facility_horror.py").read())
from mathutils import Quaternion

PALETTE["Skin"] = ((0.36, 0.32, 0.3, 1), 0.0, 0.55, None)
PALETTE["Bag"] = ((0.035, 0.045, 0.045, 1), 0.1, 0.35, None)


# ------------------------------------------------------------------ mannequin

def _meta_obj(name):
    mb = bpy.data.metaballs.new(name)
    mb.resolution = 0.018
    mb.render_resolution = 0.018
    mb.threshold = 0.6
    o = bpy.data.objects.new(name, mb)
    bpy.context.collection.objects.link(o)
    return o, mb


def _el(mb, kind, co, r, size=(1, 1, 1), yaw=0.0, pitch=0.0):
    e = mb.elements.new(type=kind)
    e.co = co
    e.radius = r
    e.size_x, e.size_y, e.size_z = size
    e.rotation = Quaternion((0, 0, 1), math.radians(yaw)) @ Quaternion((0, 1, 0), math.radians(pitch))
    e.stiffness = 2.0
    return e


def mannequin(name="Mannequin", arm_out=False, scale=1.0, depth=1.0):
    """A body lying on its back along +x (head at +x), back on z 0."""
    o, mb = _meta_obj(name)
    E = lambda *a, **k: _el(mb, *a, **k)
    E("ELLIPSOID", (0.80, 0, 0.11), 0.115, (1.15, 0.85, 0.95))           # head
    E("CAPSULE", (0.655, 0, 0.085), 0.055, (0.05, 1, 1))                 # neck
    E("ELLIPSOID", (0.42, 0, 0.12), 0.21, (1.0, 0.95, 0.55))             # chest
    E("ELLIPSOID", (0.15, 0, 0.105), 0.17, (1.0, 0.9, 0.55))             # belly
    E("ELLIPSOID", (-0.06, 0, 0.1), 0.18, (0.8, 1.0, 0.55))              # pelvis
    for y in (-0.1, 0.1):
        E("CAPSULE", (-0.34, y, 0.08), 0.08, (0.19, 1, 1))                # thigh
        E("CAPSULE", (-0.73, y * 1.1, 0.06), 0.056, (0.18, 1, 1))         # shin
        E("CAPSULE", (-0.95, y * 1.15, 0.09), 0.045, (0.055, 1, 1), pitch=80)   # foot
    for side in (-1, 1):
        if arm_out and side < 0:
            E("CAPSULE", (0.47, -0.36, 0.06), 0.05, (0.13, 1, 1), yaw=-50)
            E("CAPSULE", (0.36, -0.6, 0.045), 0.042, (0.13, 1, 1), yaw=-75)
            E("ELLIPSOID", (0.32, -0.8, 0.03), 0.05, (1.3, 0.8, 0.45), yaw=-80)
        else:
            E("CAPSULE", (0.42, side * 0.25, 0.06), 0.05, (0.12, 1, 1))
            E("CAPSULE", (0.13, side * 0.27, 0.05), 0.042, (0.12, 1, 1))
            E("ELLIPSOID", (-0.09, side * 0.27, 0.04), 0.05, (1.4, 0.8, 0.45))
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.convert(target="MESH")
    m = bpy.context.active_object
    m.name = name
    m.scale = (scale, scale, scale * depth)
    bpy.ops.object.transform_apply(scale=True)
    _finish(m, "Skin", smooth=True)
    return m


# ------------------------------------------------------------------ cloth

def drop_cloth(size, location, colliders, frames=70, subdiv=(70, 42), name="Cloth"):
    """A cloth grid dropped from location onto the collider objects and the floor; returns it applied."""
    floor = bpy.ops.mesh.primitive_plane_add(size=8, location=(0, 0, 0))
    floor = bpy.context.active_object
    floor.modifiers.new("col", "COLLISION")
    for c in colliders:
        mod = c.modifiers.new("col", "COLLISION")
        c.collision.thickness_outer = 0.012
        c.collision.cloth_friction = 8
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=subdiv[0], y_subdivisions=subdiv[1], size=1, location=location)
    cloth = bpy.context.active_object
    cloth.name = name
    cloth.scale = (size[0], size[1], 1)
    bpy.ops.object.transform_apply(scale=True)
    mod = cloth.modifiers.new("Cloth", "CLOTH")
    s = mod.settings
    s.quality = 8
    s.mass = 0.25
    s.tension_stiffness = 12
    s.compression_stiffness = 12
    s.bending_stiffness = 0.05
    s.air_damping = 2.0
    mod.collision_settings.distance_min = 0.006
    mod.collision_settings.collision_quality = 4
    mod.point_cache.frame_start = 1
    mod.point_cache.frame_end = frames
    scene = bpy.context.scene
    scene.frame_start, scene.frame_end = 1, frames
    for f in range(1, frames + 1):
        scene.frame_set(f)
    bpy.ops.object.select_all(action="DESELECT")
    cloth.select_set(True)
    bpy.context.view_layer.objects.active = cloth
    bpy.ops.object.modifier_apply(modifier="Cloth")
    scene.frame_set(1)
    bpy.data.objects.remove(floor, do_unlink=True)
    for c in colliders:
        c.modifiers.remove(c.modifiers["col"])
    return cloth


def stain_on(target, x, y, r, m, seed):
    """An irregular stain laid onto the target's top surface (projected down, lifted 3 mm)."""
    random.seed(seed)
    bvh = BVHTree.FromObject(target, bpy.context.evaluated_depsgraph_get())
    bm = bmesh.new()
    rings, segs = 5, 18
    centre = None
    for i in range(rings + 1):
        for j in range(segs):
            a = j * 2 * math.pi / segs
            rr = r * (i / rings) * (1 + 0.35 * math.sin(a * 3 + seed) + random.uniform(-0.15, 0.15))
            px, py = x + math.cos(a) * rr, y + math.sin(a) * rr * 0.8
            hit = bvh.ray_cast(Vector((px, py, 3.0)), Vector((0, 0, -1)))
            z = (hit[0].z if hit[0] is not None else 0.0) + 0.003
            v = bm.verts.new((px, py, z))
            if i == 0:
                break
    bm.verts.ensure_lookup_table()
    centre = bm.verts[0]
    ring = lambda i: [bm.verts[1 + (i - 1) * segs + j] for j in range(segs)]
    for j in range(segs):
        bm.faces.new((centre, ring(1)[j], ring(1)[(j + 1) % segs]))
    for i in range(1, rings):
        a, b = ring(i), ring(i + 1)
        for j in range(segs):
            bm.faces.new((a[j], b[j], b[(j + 1) % segs], a[(j + 1) % segs]))
    mesh = bpy.data.meshes.new("stain")
    bm.to_mesh(mesh)
    bm.free()
    o = bpy.data.objects.new("stain", mesh)
    bpy.context.collection.objects.link(o)
    return _finish(o, m, smooth=True)


def covered_body2():
    """Body on the floor under a draped sheet, one arm slipping out, blood soaking through."""
    body = mannequin("Body", arm_out=True, depth=1.35)
    sheet = drop_cloth((2.3, 1.35), (-0.02, 0.08, 0.6), [body], frames=90, name="Sheet")
    _finish(sheet, "Sheet", smooth=True)
    sol = sheet.modifiers.new("thick", "SOLIDIFY")
    sol.thickness = 0.004
    P = [sheet, body]
    for k, (x, y, r, m) in enumerate(((0.42, 0.06, 0.13, "Blood_Dry"), (0.3, -0.12, 0.07, "Blood"),
                                      (0.8, 0.02, 0.06, "Blood_Dry"), (-0.1, 0.1, 0.05, "Blood_Dry"))):
        P.append(stain_on(sheet, x, y, r, m, 50 + k))
    P.append(stain_on(sheet, 0.45, -0.95, 0.22, "Blood", 61))     # pool on the floor by the hand
    return join(P, "Horror_CoveredBody2")


def body_bag2():
    """Black vinyl body bag lying along +x: body shape inside, sagging ends, zip in a U, carry handles."""
    o, mb = _meta_obj("Bag")
    mb.resolution = 0.022
    for k in range(17):
        x = -0.95 + k * 0.115
        _el(mb, "ELLIPSOID", (x, 0, 0.05), 0.3, (0.8, 1.55, 0.36 if x < 0.3 else 0.42))
    _el(mb, "ELLIPSOID", (0.84, 0, 0.1), 0.13, (1.1, 0.9, 0.8))          # head
    _el(mb, "ELLIPSOID", (0.42, 0, 0.12), 0.2, (1.0, 1.0, 0.6))          # chest
    for y in (-0.1, 0.1):
        _el(mb, "ELLIPSOID", (-0.98, y, 0.13), 0.075, (0.8, 0.8, 1.0))   # feet tent the bag
        _el(mb, "CAPSULE", (-0.4, y, 0.1), 0.08, (0.25, 1, 1))           # legs
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.convert(target="MESH")
    bag = bpy.context.active_object
    for v in bag.data.vertices:
        v.co.z = max(0.0, v.co.z - 0.02)
    tex = bpy.data.textures.new("folds", "CLOUDS")
    tex.noise_scale = 0.12
    disp = bag.modifiers.new("folds", "DISPLACE")
    disp.texture = tex
    disp.strength = 0.012
    _finish(bag, "Bag", smooth=True)
    P = [bag]
    bvh = BVHTree.FromObject(bag, bpy.context.evaluated_depsgraph_get())
    # Zip: along the top from the feet to the head, round the head and back a little.
    path = [(x / 100.0, 0.0) for x in range(-90, 86, 3)]
    path += [(0.86 + 0.07 * math.sin(a / 10), 0.07 * (1 - math.cos(a / 10))) for a in range(0, 32, 3)]
    for k, (x, y) in enumerate(path):
        hit = bvh.ray_cast(Vector((x, y, 3.0)), Vector((0, 0, -1)))
        if hit[0] is None:
            continue
        P.append(box((0.022, 0.012, 0.008), (x, y, hit[0].z + 0.005), "Metal_Mid"))
    end = bvh.ray_cast(Vector((-0.9, 0.0, 3.0)), Vector((0, 0, -1)))
    if end[0] is not None:
        P.append(box((0.05, 0.02, 0.012), (-0.95, 0.0, end[0].z + 0.01), "Metal_Mid"))
    for x in (-0.6, 0.0, 0.55):
        for side in (-1, 1):
            hit = bvh.ray_cast(Vector((x, side * 0.6, 0.06)), Vector((0, -side, 0)))
            y = hit[0].y if hit[0] is not None else side * 0.42
            P.append(box((0.18, 0.02, 0.03), (x, y + side * 0.012, 0.07), "Rubber"))
    return join(P, "Horror_BodyBag2")


# ------------------------------------------------------------------ door

def breaker_door3():
    """Leaf cut to the kit frame's opening: 2.11 x 2.48, top corners 0.28. Hinge at the origin, the leaf
    runs to Blender -x (Unity +x)."""
    W, H, T, C = 2.11, 2.48, 0.08, 0.28
    mesh = bpy.data.meshes.new("leaf")
    o = bpy.data.objects.new("leaf", mesh)
    bpy.context.collection.objects.link(o)
    bm = bmesh.new()
    outline = [(0, 0), (-W, 0), (-W, H - C), (-W + C, H), (-C, H), (0, H - C)]
    for side in (-1, 1):
        verts = [bm.verts.new((x, side * T / 2, z)) for x, z in outline]
        bm.faces.new(verts if side > 0 else list(reversed(verts)))
    bm.verts.ensure_lookup_table()
    n = len(outline)
    for i in range(n):
        a, b = i, (i + 1) % n
        bm.faces.new((bm.verts[a], bm.verts[b], bm.verts[n + b], bm.verts[n + a]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    _finish(o, "Paint_Blue", bevel=0.01)
    cx = -W / 2
    P = [o]
    for side in (-1, 1):
        y = side * (T / 2 + 0.006)
        P.append(box((W - 0.3, 0.012, 0.95), (cx, y, 0.6), "Metal_Mid", bevel=0.006))
        P.append(box((W - 0.3, 0.012, 0.6), (cx, y, 1.45), "Metal_Mid", bevel=0.006))
        P.append(box((W - 0.3, 0.014, 0.05), (cx, y, 1.1), "Paint_Yellow"))
        for k in range(6):
            P.append(box((W - 0.3, 0.014, 0.03), (cx, y, 0.2 + k * 0.08), "Paint_Yellow" if k % 2 else "Metal_Dark"))
        P.append(box((0.62, 0.014, 0.42), (cx, y, 2.02), "Metal_Dark", bevel=0.008))
        P.append(box((0.54, 0.016, 0.34), (cx, y * 1.05, 2.02), "Glass"))
        for k in range(1, 4):
            P.append(box((0.008, 0.018, 0.34), (cx - 0.27 + k * 0.135, y * 1.06, 2.02), "Grate"))
        P.append(cyl(0.035, 0.05, (-W + 0.18, side * (T / 2 + 0.02), 1.05), "Metal_Dark", verts=16, rot=(90, 0, 0)))
        P.append(box((0.17, 0.03, 0.03), (-W + 0.26, side * (T / 2 + 0.035), 1.05), "Metal_Dark", bevel=0.008))
        P.append(box((0.08, 0.01, 0.2), (-W + 0.18, side * (T / 2 + 0.008), 0.87), "Metal_Dark"))
        P.append(box((0.4, 0.008, 0.1), (cx, y * 1.02, 1.75), "Paper"))          # BREAKERS label
    for z in (0.3, 1.25, 2.15):
        P.append(cyl(0.025, 0.18, (0.0, 0.0, z), "Metal_Dark", verts=12))
    return join(P, "Door_Breaker")


# ------------------------------------------------------------------ room dressing

def cable_tray():
    """6 m ladder tray along Blender +x (pivot at x 0), cables in it, hanger rods 0.6 m up."""
    P = []
    for y in (-0.2, 0.2):
        P.append(box((6.0, 0.02, 0.08), (3, y, 0), "Metal_Mid"))
    for k in range(20):
        P.append(box((0.025, 0.4, 0.015), (0.15 + k * 0.3, 0, -0.03), "Metal_Mid"))
    random.seed(4)
    for k in range(7):
        y = -0.15 + k * 0.05
        P.append(cyl(0.018 + random.uniform(0, 0.01), 6.0, (3, y, -0.005 + random.uniform(0, 0.02)),
                     random.choice(("Rubber", "Rubber", "Paint_Red", "Paint_Blue")), verts=8, rot=(0, 90, 0)))
    for x in (0.5, 3.0, 5.5):
        for y in (-0.22, 0.22):
            P.append(cyl(0.008, 0.6, (x, y, 0.3), "Metal_Dark", verts=6))
        P.append(box((0.04, 0.5, 0.03), (x, 0, -0.05), "Metal_Dark"))
    return join(P, "Elec_CableTray")


def rubber_mat():
    """Insulating mat 1 x 1 x 0.01 (scaled in Unity), yellow border."""
    P = [box((1.0, 1.0, 0.01), (0, 0, 0.005), "Rubber")]
    for k in range(9):
        P.append(box((0.9, 0.01, 0.003), (0, -0.4 + k * 0.1, 0.011), "Rubber"))
    return join(P, "Prop_RubberMat")


def water_cooler():
    P = [box((0.32, 0.32, 0.95), (0, 0, 0.475), "Paint_White", bevel=0.02),
         box((0.2, 0.02, 0.16), (0, -0.165, 0.85), "Metal_Dark"),
         cyl(0.015, 0.05, (-0.05, -0.18, 0.88), "Paint_Blue", verts=8, rot=(90, 0, 0)),
         cyl(0.015, 0.05, (0.05, -0.18, 0.88), "Paint_Red", verts=8, rot=(90, 0, 0)),
         cyl(0.135, 0.36, (0, 0, 1.13), "Glass", verts=20),
         cyl(0.05, 0.05, (0, 0, 0.96), "Paint_Blue", verts=12),
         box((0.24, 0.12, 0.02), (0, -0.12, 0.62), "Grate")]
    return join(P, "Prop_WaterCooler")


def dead_plant():
    random.seed(12)
    P = [cyl(0.16, 0.32, (0, 0, 0.16), "Paint_White", verts=20), cyl(0.15, 0.02, (0, 0, 0.31), "Cardboard", verts=20)]
    for k in range(9):
        a, l = random.uniform(0, 6.28), random.uniform(0.35, 0.8)
        tilt = random.uniform(10, 40)
        P.append(cyl(0.006, l, (math.cos(a) * 0.05 + math.cos(a) * math.sin(math.radians(tilt)) * l / 2,
                                 math.sin(a) * 0.05 + math.sin(a) * math.sin(math.radians(tilt)) * l / 2,
                                 0.32 + math.cos(math.radians(tilt)) * l / 2),
                     "Wood", verts=5, rot=(0, tilt, math.degrees(a))))
    for k in range(5):
        P.append(box((0.08, 0.04, 0.003), (random.uniform(-0.25, 0.25), random.uniform(-0.25, 0.25), 0.002),
                     "Cardboard", rot=(0, 0, random.uniform(0, 180))))
    return join(P, "Prop_DeadPlant")


def cork_board():
    random.seed(31)
    P = [box((1.2, 0.03, 0.8), (0, 0.015, 0), "Wood", bevel=0.01), box((1.12, 0.02, 0.72), (0, -0.005, 0), "Cardboard")]
    for k in range(9):
        P.append(box((random.uniform(0.12, 0.22), 0.003, random.uniform(0.14, 0.28)),
                     (random.uniform(-0.45, 0.45), -0.018, random.uniform(-0.25, 0.25)), "Paper",
                     rot=(0, random.uniform(-8, 8), 0)))
    P.append(box((0.2, 0.004, 0.15), (0.3, -0.021, 0.18), "Blood_Dry", rot=(0, 12, 0)))
    return join(P, "Prop_CorkBoard")


def wall_clock():
    P = [cyl(0.17, 0.04, (0, 0.02, 0), "Metal_Dark", verts=32, rot=(90, 0, 0)),
         cyl(0.15, 0.01, (0, -0.002, 0), "Paint_White", verts=32, rot=(90, 0, 0)),
         box((0.012, 0.006, 0.11), (0.03, -0.01, 0.04), "Metal_Dark", rot=(0, -35, 0)),
         box((0.01, 0.006, 0.08), (-0.03, -0.012, 0.02), "Metal_Dark", rot=(0, 60, 0))]
    for k in range(12):
        a = k * math.pi / 6
        P.append(box((0.008, 0.004, 0.02), (math.sin(a) * 0.13, -0.008, math.cos(a) * 0.13), "Metal_Dark",
                     rot=(0, -math.degrees(a), 0)))
    return join(P, "Prop_WallClock")


def build_bodies():
    made = []
    for fn in (covered_body2, body_bag2, breaker_door3, cable_tray, rubber_mat, water_cooler, dead_plant,
               cork_board, wall_clock):
        clear()
        o = fn()
        export([o], o.name)
        made.append((o.name, len(o.data.polygons), tuple(round(d, 2) for d in o.dimensions)))
    return made
