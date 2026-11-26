# Facility horror pass (2026-11-10): furniture in place of the primitive boxes, the throwables, and
# the horror dressing. Same conventions as facility_assets.py: Z up, metres, front of a prop towards
# Blender -y (Unity +z), back towards +y; build_horror() exports every asset.
exec(open(r"C:/Users/Admin/3D projects/My project/Tools/Blender/facility_assets.py").read())
import random
from mathutils.bvhtree import BVHTree


# ------------------------------------------------------------------ helpers

def blob(radius, loc, m, seed, segments=30, rough=0.3, thick=0.004, squash=(1.0, 1.0)):
    """A flat, irregular puddle/stain lying on loc (z is its underside)."""
    random.seed(seed)
    ph = [random.uniform(0, 6.283) for _ in range(4)]
    bm = bmesh.new()
    vs = []
    for i in range(segments):
        a = 2 * math.pi * i / segments
        k = 1 + rough * (0.55 * math.sin(3 * a + ph[0]) + 0.3 * math.sin(5 * a + ph[1]) + 0.15 * math.sin(11 * a + ph[2]))
        k += random.uniform(-0.06, 0.06)
        vs.append(bm.verts.new((loc[0] + math.cos(a) * radius * k * squash[0],
                                loc[1] + math.sin(a) * radius * k * squash[1], loc[2] + thick)))
    face = bm.faces.new(vs)
    ret = bmesh.ops.extrude_face_region(bm, geom=[face])
    down = [g for g in ret["geom"] if isinstance(g, bmesh.types.BMVert)]
    bmesh.ops.translate(bm, verts=down, vec=(0, 0, -thick))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new("blob")
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new("blob", me)
    bpy.context.collection.objects.link(o)
    return _finish(o, m)


_META_COUNT = 0


def meta(elements, m, resolution=0.045, threshold=0.6):
    """A smooth blobby mesh from metaball spheres [(co, radius), ...]."""
    global _META_COUNT
    _META_COUNT += 1
    name = "meta_%d" % _META_COUNT  # a family of its own: same-named metaballs melt together
    mb = bpy.data.metaballs.new(name)
    mb.resolution = resolution
    mb.render_resolution = resolution
    mb.threshold = threshold
    ob = bpy.data.objects.new(name, mb)
    bpy.context.collection.objects.link(ob)
    for co, r in elements:
        e = mb.elements.new(type="BALL")
        e.co = co
        e.radius = r
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.context.view_layer.update()
    bpy.ops.object.convert(target="MESH")
    o = bpy.context.active_object
    o.data.materials.clear()
    o.data.materials.append(mat(m))
    bpy.ops.object.shade_smooth()
    return o


def top_at(o, x, y):
    """Height of an object's surface above (x, y), or None."""
    dg = bpy.context.evaluated_depsgraph_get()
    tree = BVHTree.FromObject(o, dg)
    hit = tree.ray_cast(Vector((x, y, 10)), Vector((0, 0, -1)))
    return hit[0].z if hit[0] is not None else None


def lay_down(o, rx=0.0, ry=0.0, rz=0.0):
    """Turns a joined prop (pivot at the origin) and sets it back on the floor (lowest point z 0)."""
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    o.rotation_euler = (math.radians(rx), math.radians(ry), math.radians(rz))
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    low = min(v.co.z for v in o.data.vertices)
    for v in o.data.vertices:
        v.co.z -= low
    return o


def handle(loc, m="Metal_Dark", w=0.14):
    return box((w, 0.03, 0.02), loc, m, bevel=0.005)


# ------------------------------------------------------------------ furniture

def desk():
    """1.6 x 0.75 m steel office desk, top at 0.78, drawer pedestal on the right, modesty panel at the back."""
    P = [box((1.6, 0.75, 0.04), (0, 0, 0.76), "Metal_Mid", bevel=0.01),
         box((0.42, 0.7, 0.72), (0.56, 0.0, 0.37), "Metal_Dark", bevel=0.01),
         box((1.1, 0.02, 0.42), (-0.2, 0.34, 0.5), "Metal_Dark")]
    for k in range(3):
        z = 0.6 - k * 0.22
        P.append(box((0.38, 0.02, 0.19), (0.56, -0.355, z), "Metal_Mid", bevel=0.005))
        P.append(handle((0.56, -0.37, z + 0.05)))
    for y in (-0.33, 0.33):
        P.append(box((0.045, 0.045, 0.74), (-0.75, y, 0.37), "Metal_Dark"))
    P.append(box((0.045, 0.7, 0.04), (-0.75, 0, 0.06), "Metal_Dark"))
    P.append(box((0.7, 0.03, 0.03), (-0.2, -0.33, 0.7), "Metal_Dark"))  # under-top rail
    return join(P, "Furn_Desk")


def monitor():
    P = [box((0.56, 0.05, 0.36), (0, 0, 0.3), "Metal_Dark", bevel=0.01),
         box((0.5, 0.01, 0.3), (0, -0.027, 0.3), "Screen"),
         box((0.06, 0.06, 0.12), (0, 0.04, 0.08), "Metal_Dark"),
         box((0.24, 0.18, 0.02), (0, 0.03, 0.01), "Metal_Dark", bevel=0.005),
         box((0.44, 0.15, 0.02), (0, -0.32, 0.01), "Metal_Dark", bevel=0.004)]   # keyboard
    for i in range(10):
        P.append(box((0.035, 0.03, 0.006), (-0.19 + i * 0.042, -0.32, 0.023), "Rubber"))
    return join(P, "Furn_Monitor")


def office_chair(fallen=False):
    P = []
    for i in range(5):
        a = 72 * i
        r = math.radians(a)
        P.append(box((0.32, 0.05, 0.04), (math.cos(r) * 0.16, math.sin(r) * 0.16, 0.1), "Metal_Dark", rot=(0, 0, a)))
        P.append(cyl(0.03, 0.03, (math.cos(r) * 0.31, math.sin(r) * 0.31, 0.035), "Rubber", verts=10, rot=(90, 0, a)))
    P.append(cyl(0.04, 0.06, (0, 0, 0.12), "Metal_Dark", verts=12))
    P.append(cyl(0.025, 0.3, (0, 0, 0.28), "Metal_Mid", verts=12))
    P.append(box((0.48, 0.46, 0.08), (0, 0, 0.47), "Cloth", bevel=0.03))
    P.append(box((0.44, 0.07, 0.52), (0, 0.25, 0.82), "Cloth", bevel=0.03, rot=(-8, 0, 0)))
    P.append(box((0.06, 0.05, 0.36), (0, 0.22, 0.56), "Metal_Dark", rot=(-8, 0, 0)))
    for x in (-0.26, 0.26):
        P.append(box((0.05, 0.3, 0.03), (x, 0.02, 0.66), "Rubber", bevel=0.01))
        P.append(box((0.03, 0.03, 0.16), (x, 0.08, 0.57), "Metal_Dark"))
    o = join(P, "Furn_OfficeChair_Fallen" if fallen else "Furn_OfficeChair")
    if fallen:
        lay_down(o, rx=-88, rz=25)
    return o


def workbench():
    """3 m heavy bench (back at +y), lower shelf, vise, pegboard with tools, a toolbox and rags."""
    random.seed(13)
    P = [box((3.0, 0.85, 0.06), (1.5 - 1.5, 0, 0.9), "Wood", bevel=0.01),
         box((3.0, 0.85, 0.03), (0, 0, 0.86), "Metal_Dark"),
         box((2.9, 0.75, 0.03), (0, 0.02, 0.22), "Metal_Mid")]
    for x in (-1.45, 1.45):
        for y in (-0.38, 0.38):
            P.append(box((0.06, 0.06, 0.86), (x, y, 0.43), "Metal_Dark"))
    P.append(box((2.9, 0.04, 0.06), (0, -0.4, 0.82), "Metal_Dark"))
    # Pegboard and tools.
    P.append(box((2.8, 0.03, 1.1), (0, 0.41, 1.55), "Metal_Mid", bevel=0.005))
    for k in range(24):
        P.append(box((0.012, 0.01, 0.012), (-1.3 + (k % 12) * 0.22, 0.395, 1.25 + (k // 12) * 0.6), "Metal_Dark"))
    for k, (x, kind) in enumerate(((-1.1, "wrench"), (-0.85, "wrench"), (-0.6, "hammer"), (-0.25, "saw"),
                                   (0.3, "wrench"), (0.55, "screw"), (0.75, "screw"), (1.05, "hammer"))):
        z = 1.55 + random.uniform(-0.1, 0.1)
        if kind == "wrench":
            P.append(box((0.03, 0.015, 0.32), (x, 0.385, z), "Metal_Mid"))
            P.append(torus(0.035, 0.012, (x, 0.385, z + 0.17), "Metal_Mid", rot=(90, 0, 0), major=12, minor=6))
        elif kind == "hammer":
            P.append(box((0.03, 0.02, 0.3), (x, 0.385, z), "Wood"))
            P.append(box((0.12, 0.03, 0.04), (x, 0.385, z + 0.16), "Metal_Dark"))
        elif kind == "saw":
            P.append(box((0.32, 0.005, 0.1), (x, 0.39, z), "Metal_Mid", rot=(0, 8, 0)))
            P.append(box((0.08, 0.025, 0.1), (x - 0.19, 0.385, z), "Paint_Red"))
        else:
            P.append(cyl(0.012, 0.18, (x, 0.385, z), "Paint_Red" if k % 2 else "Paint_Yellow", verts=8))
    # Vise at the left end.
    P += [box((0.18, 0.12, 0.12), (-1.25, -0.32, 0.99), "Paint_Blue", bevel=0.01),
          box((0.18, 0.04, 0.12), (-1.25, -0.43, 0.99), "Paint_Blue", bevel=0.01),
          cyl(0.012, 0.3, (-1.25, -0.48, 0.99), "Metal_Mid", verts=8, rot=(0, 90, 0))]
    # Toolbox and clutter on top.
    P += [box((0.5, 0.24, 0.22), (0.9, 0.12, 1.04), "Paint_Red", bevel=0.015),
          box((0.3, 0.03, 0.03), (0.9, 0.12, 1.17), "Metal_Dark"),
          box((0.3, 0.22, 0.03), (-0.2, -0.05, 0.945), "Cloth", rot=(0, 0, 18)),
          cyl(0.04, 0.12, (0.25, 0.1, 0.99), "Paint_Yellow", verts=12),
          cyl(0.035, 0.18, (0.35, 0.18, 1.02), "Metal_Mid", verts=12)]
    # Lower shelf: a crate and cans.
    P += [box((0.6, 0.45, 0.3), (-0.8, 0.05, 0.39), "Wood", bevel=0.01),
          cyl(0.15, 0.32, (0.6, 0.05, 0.4), "Paint_Blue", verts=16),
          cyl(0.15, 0.32, (0.95, 0.1, 0.4), "Metal_Mid", verts=16)]
    return join(P, "Furn_Workbench")


def metal_shelf(name="Furn_MetalShelf", seed=2):
    """1.6 x 0.5 x 1.9 m steel shelving with supplies. The middle of the second shelf stays clear."""
    random.seed(seed)
    P = []
    for x in (-0.78, 0.78):
        for y in (-0.23, 0.23):
            P.append(box((0.04, 0.04, 1.9), (x, y, 0.95), "Metal_Dark"))
    levels = (0.12, 0.62, 1.12, 1.62)
    for li, z in enumerate(levels):
        P.append(box((1.6, 0.5, 0.025), (0, 0, z), "Metal_Mid"))
        P.append(box((1.6, 0.02, 0.05), (0, -0.25, z + 0.01), "Metal_Dark"))
        x = -0.7
        while x < 0.65:
            if li == 1 and -0.3 < x < 0.3:
                x += 0.1
                continue
            r = random.random()
            if r < 0.3:
                w, d, h = random.uniform(0.25, 0.38), random.uniform(0.3, 0.42), random.uniform(0.18, 0.34)
                P.append(box((w, d, h), (x + w / 2, random.uniform(-0.04, 0.04), z + 0.0125 + h / 2), "Cardboard",
                             rot=(0, 0, random.uniform(-6, 6))))
                x += w + 0.04
            elif r < 0.55:
                h = random.uniform(0.22, 0.3)
                P.append(cyl(0.06, h, (x + 0.06, random.uniform(-0.1, 0.1), z + 0.0125 + h / 2),
                             random.choice(("Paint_Blue", "Paint_White", "Paint_Red", "Paint_Yellow")), verts=12))
                P.append(cyl(0.02, 0.04, (x + 0.06, 0, z + 0.0125 + h + 0.02), "Rubber", verts=8))
                x += 0.14
            elif r < 0.7:
                P.append(box((0.28, 0.24, 0.07), (x + 0.14, 0, z + 0.05), "Cloth", rot=(0, 0, random.uniform(-20, 20))))
                x += 0.3
            else:
                x += 0.12
    return join(P, name)


def cot():
    """Folding camp bed, 0.8 x 2.0 m, along y; a rumpled blanket and a pillow."""
    random.seed(8)
    P = [box((0.78, 1.9, 0.02), (0, 0, 0.42), "Cloth", bevel=0.005)]
    for x in (-0.4, 0.4):
        P.append(cyl(0.018, 2.0, (x, 0, 0.42), "Metal_Mid", verts=10, rot=(90, 0, 0)))
    for y in (-0.8, 0.0, 0.8):
        P.append(box((0.03, 0.03, 0.6), (0, y, 0.21), "Metal_Dark", rot=(0, 52, 0)))
        P.append(box((0.03, 0.03, 0.6), (0, y, 0.21), "Metal_Dark", rot=(0, -52, 0)))
        P.append(box((0.8, 0.03, 0.025), (0, y, 0.02), "Metal_Dark"))
    P.append(box((0.74, 1.15, 0.05), (0.02, -0.28, 0.455), "Paint_Red", bevel=0.02, rot=(0, 2, 4)))
    P.append(box((0.72, 0.3, 0.1), (0.0, 0.3, 0.48), "Paint_Red", bevel=0.04, rot=(0, -3, -2)))  # thrown-back fold
    P.append(box((0.3, 0.5, 0.04), (0.42, -0.4, 0.3), "Paint_Red", bevel=0.015, rot=(0, 70, 0)))  # hanging over the side
    P.append(box((0.5, 0.32, 0.1), (0, 0.78, 0.48), "Sheet", bevel=0.04))
    return join(P, "Furn_Cot")


def filing_cabinet(open_drawer=False, name="Furn_FilingCabinet"):
    P = [box((0.5, 0.62, 1.35), (0, 0, 0.675), "Metal_Mid", bevel=0.01)]
    for k in range(4):
        z = 1.17 - k * 0.32
        out = 0.35 if (open_drawer and k == 1) else 0.0
        P.append(box((0.46, 0.02, 0.29), (0, -0.315 - out, z), "Metal_Mid", bevel=0.004))
        P.append(handle((0, -0.335 - out, z + 0.06)))
        P.append(box((0.08, 0.01, 0.04), (0, -0.33 - out, z - 0.06), "Paper"))
        if out > 0:
            P.append(box((0.44, 0.34, 0.25), (0, -0.315 - out / 2, z - 0.02), "Metal_Dark"))
            for j in range(6):
                P.append(box((0.4, 0.004, 0.28), (0, -0.2 - j * 0.05, z + 0.05), "Paper", rot=(random.uniform(-12, 12), 0, 0)))
    return join(P, name)


def whiteboard():
    """Wall board (back at +y, 1.1..2.3 m up) with frantic writing in marker and red."""
    random.seed(6)
    P = [box((2.4, 0.03, 1.2), (0, 0, 1.7), "Paint_White"),
         box((2.46, 0.04, 0.04), (0, -0.005, 2.32), "Metal_Mid"), box((2.46, 0.04, 0.04), (0, -0.005, 1.08), "Metal_Mid"),
         box((0.04, 0.04, 1.26), (-1.22, -0.005, 1.7), "Metal_Mid"), box((0.04, 0.04, 1.26), (1.22, -0.005, 1.7), "Metal_Dark"),
         box((1.2, 0.08, 0.02), (0.2, -0.05, 1.07), "Metal_Mid")]
    # Rows of "writing", a diagram, tally marks, a red cross-out and a taped photo.
    for row in range(5):
        x = -1.05
        while x < 0.2:
            w = random.uniform(0.06, 0.2)
            P.append(box((w, 0.005, 0.012), (x + w / 2, -0.017, 2.15 - row * 0.12 + random.uniform(-0.01, 0.01)), "Metal_Dark"))
            x += w + 0.04
    for k in range(9):
        P.append(box((0.008, 0.005, 0.16), (0.45 + k * 0.05, -0.017, 1.9), "Paint_Red", rot=(0, 15 if k % 5 == 4 else 0, 0)))
    for a in (35, -35):
        P.append(box((0.9, 0.006, 0.02), (-0.5, -0.019, 1.45), "Paint_Red", rot=(0, a, 0)))
    P.append(box((0.18, 0.006, 0.24), (0.9, -0.019, 1.55), "Paper", rot=(0, 6, 0)))
    P.append(box((0.07, 0.007, 0.02), (0.9, -0.022, 1.68), "Wrap"))
    return join(P, "Furn_Whiteboard")


def mop_bucket():
    P = [box((0.42, 0.34, 0.32), (0, 0, 0.22), "Paint_Yellow", bevel=0.03),
         box((0.36, 0.28, 0.02), (0, 0, 0.36), "Liquid"),
         box((0.2, 0.3, 0.16), (0.12, 0, 0.44), "Paint_Yellow", bevel=0.02),
         cyl(0.012, 1.3, (0.05, 0.02, 0.95), "Metal_Mid", verts=8, rot=(0, 12, 0))]
    for x in (-0.17, 0.17):
        for y in (-0.13, 0.13):
            P.append(cyl(0.03, 0.03, (x, y, 0.03), "Rubber", verts=10, rot=(90, 0, 0)))
    P.append(meta([((0.0, 0.02, 0.38), 0.09), ((0.03, 0.0, 0.33), 0.08)], "Cloth"))
    return join(P, "Furn_MopBucket")


def trash_bags():
    random.seed(4)
    P = []
    for k, (x, y, s) in enumerate(((0, 0, 1.0), (0.45, 0.1, 0.9), (0.2, 0.42, 0.95), (0.62, 0.48, 0.8))):
        P.append(meta([((x, y, 0.22 * s), 0.3 * s), ((x + 0.05, y, 0.36 * s), 0.22 * s), ((x, y + 0.03, 0.48 * s), 0.1 * s)],
                      "Rubber", resolution=0.05))
        P.append(cyl(0.025, 0.08, (x, y + 0.03, 0.56 * s), "Rubber", verts=8))
    return join(P, "Furn_TrashBags")


# ------------------------------------------------------------------ throwables

def item_bottle():
    """Glass bottle centred on its middle: 0.3 m tall (z -0.15..0.15), as the throwable's collider."""
    P = [cyl(0.04, 0.2, (0, 0, -0.05), "Bottle_Glass", verts=20),
         cyl(0.04, 0.012, (0, 0, -0.144), "Bottle_Glass", verts=20)]
    bpy.ops.mesh.primitive_cone_add(vertices=20, radius1=0.04, radius2=0.016, depth=0.05, location=(0, 0, 0.075))
    c = bpy.context.active_object
    _finish(c, "Bottle_Glass")
    bpy.ops.object.shade_smooth()
    P.append(c)
    P += [cyl(0.016, 0.05, (0, 0, 0.125), "Bottle_Glass", verts=14),
          cyl(0.018, 0.012, (0, 0, 0.147), "Metal_Mid", verts=14),
          cyl(0.0405, 0.08, (0, 0, -0.04), "Paper", verts=20)]
    return join(P, "Item_Bottle")


def item_can():
    """Tin can centred on its middle: 0.13 m tall, 0.075 across."""
    P = [cyl(0.0375, 0.12, (0, 0, 0), "Tin", verts=24),
         torus(0.035, 0.004, (0, 0, 0.061), "Tin", major=24, minor=6),
         torus(0.035, 0.004, (0, 0, -0.061), "Tin", major=24, minor=6),
         cyl(0.038, 0.08, (0, 0, 0), "Paint_Red", verts=24)]
    for k in range(3):
        P.append(torus(0.0378, 0.0015, (0, 0, -0.03 + k * 0.03), "Tin", major=24, minor=4))
    return join(P, "Item_Can")


# ------------------------------------------------------------------ horror

def covered_body():
    """A body under a sheet, lying along x (head at -x), blood soaking through."""
    random.seed(23)
    E = []
    # Spine of overlapping spheres from head to feet, then arms and legs alongside.
    for k in range(12):
        x = -0.95 + k * 0.17
        r = 0.3 if -0.6 < x < 0.25 else 0.22
        z = 0.06 + (0.05 if -0.6 < x < 0.25 else 0.0)
        for y in ((0.0,) if x < 0.25 else (-0.11, 0.11)):
            E.append(((x, y, z), r))
    for y in (-0.3, 0.3):
        for k in range(4):
            E.append(((-0.55 + k * 0.17, y, 0.04), 0.17))
    E += [((1.05, -0.12, 0.12), 0.18), ((1.05, 0.14, 0.12), 0.18)]  # feet tenting the sheet
    sheet = meta(E, "Sheet", resolution=0.035, threshold=0.9)
    # Keep it low: a sheet over a body is ~0.3 m high at the chest.
    for v in sheet.data.vertices:
        v.co.z = max(0.0, v.co.z) * 0.75
    P = [sheet, blob(0.55, (0.05, 0, 0.0), "Sheet", 3, rough=0.25, thick=0.012, squash=(2.3, 0.95))]
    for k, (x, y, r) in enumerate(((-0.35, 0.05, 0.13), (-0.2, -0.1, 0.09), (-0.88, 0.02, 0.07), (0.1, 0.06, 0.06))):
        z = top_at(sheet, x, y)
        if z is not None:
            P.append(blob(r, (x, y, z - 0.01), "Blood_Dry", 40 + k, rough=0.4, thick=0.014))
    P.append(blob(0.45, (-0.55, -0.6, 0.0), "Blood", 9, rough=0.35, thick=0.003, squash=(1.4, 0.8)))
    return join(P, "Horror_CoveredBody")


def body_bag():
    E = [((-0.8 + k * 0.2, 0, 0.13 + 0.03 * math.sin(k)), 0.24 - (0.05 if k in (0, 8) else 0)) for k in range(9)]
    bag = meta(E, "Rubber", resolution=0.045)
    P = [bag]
    pts = []
    for k in range(13):
        x = -0.9 + k * 0.15
        z = top_at(bag, x, 0.0)
        if z is not None:
            pts.append((x, 0.0, z + 0.004))
    if len(pts) > 2:
        P.append(tube(pts, 0.008, "Metal_Mid", res=4))
    P.append(box((0.05, 0.02, 0.03), (pts[-3][0], 0.0, pts[-3][2] + 0.01), "Metal_Mid"))
    return join(P, "Horror_BodyBag")


def gurney(fallen=False):
    random.seed(31 if fallen else 30)
    P = [box((1.95, 0.62, 0.12), (0, 0, 0.78), "Cloth", bevel=0.03),
         box((2.0, 0.66, 0.03), (0, 0, 0.71), "Metal_Mid")]
    for x in (-0.9, 0.9):
        for y in (-0.28, 0.28):
            P.append(box((0.035, 0.035, 0.64), (x, y, 0.38), "Metal_Mid"))
            P.append(cyl(0.05, 0.03, (x, y, 0.05), "Rubber", verts=12, rot=(90, 0, 0)))
        P.append(box((0.035, 0.6, 0.035), (x, 0, 0.25), "Metal_Mid"))
    P.append(box((1.8, 0.03, 0.03), (0, 0, 0.25), "Metal_Mid"))
    for y in (-0.33, 0.33):
        P.append(cyl(0.015, 1.6, (0, y, 0.98), "Metal_Mid", verts=8, rot=(0, 90, 0)))
    P.append(cyl(0.015, 0.6, (-1.03, 0, 0.98), "Metal_Mid", verts=8, rot=(90, 0, 0)))
    o_blood = blob(0.22, (0.1, 0.05, 0.84), "Blood_Dry", 5, rough=0.4, thick=0.01)
    P.append(o_blood)
    for k in range(3):
        P.append(box((0.04, 0.12, 0.004), (-0.3 + k * 0.4, -0.33, 0.85 - k * 0.02), "Rubber", rot=(0, 0, 90)))  # straps
    o = join(P, "Horror_Gurney_Fallen" if fallen else "Horror_Gurney")
    if fallen:
        lay_down(o, rx=90, rz=0)
    return o


def blood_pool():
    P = [blob(0.75, (0, 0, 0), "Blood", 11, rough=0.35, thick=0.004, squash=(1.3, 1.0))]
    random.seed(12)
    for k in range(9):
        a = random.uniform(0, 6.28)
        d = random.uniform(1.0, 1.6)
        P.append(blob(random.uniform(0.03, 0.09), (math.cos(a) * d, math.sin(a) * d * 0.8, 0), "Blood", 60 + k, rough=0.2, thick=0.003))
    return join(P, "Horror_BloodPool")


def blood_trail():
    """Smears dragged 4 m along +x, thinning out, with handprints near the start."""
    random.seed(14)
    P = []
    for k in range(11):
        x = k * 0.38
        w = 0.32 * (1 - k / 13.0)
        P.append(blob(0.2, (x, random.uniform(-0.06, 0.06), 0), "Blood" if k < 6 else "Blood_Dry", 80 + k,
                      rough=0.45, thick=0.003, squash=(1.5, w / 0.2)))
    for k, (x, y, a) in enumerate(((-0.25, 0.32, 20), (0.15, -0.36, -15))):
        P.append(blob(0.06, (x, y, 0.001), "Blood_Dry", 120 + k, rough=0.2, thick=0.003, squash=(1.3, 1.0)))
        for f in range(4):
            r = math.radians(a + (f - 1.5) * 18)
            P.append(blob(0.013, (x + math.cos(r) * 0.1, y + math.sin(r) * 0.1, 0.001), "Blood_Dry", 130 + k * 4 + f,
                          rough=0.1, thick=0.003, squash=(2.0, 1.0)))
    return join(P, "Horror_BloodTrail")


def cage():
    """1.3 x 1.3 x 2.0 m specimen cage, front door torn open, bars bent outwards from inside."""
    random.seed(17)
    P = [box((1.4, 1.4, 0.08), (0, 0, 0.04), "Metal_Dark", bevel=0.01),
         box((1.4, 1.4, 0.06), (0, 0, 2.03), "Metal_Dark", bevel=0.01)]
    for x in (-0.66, 0.66):
        for y in (-0.66, 0.66):
            P.append(box((0.06, 0.06, 2.0), (x, y, 1.0), "Metal_Mid"))
    for k in range(11):
        t = -0.55 + k * 0.11
        P.append(cyl(0.012, 1.92, (t, 0.66, 1.0), "Metal_Mid", verts=8))
        P.append(cyl(0.012, 1.92, (-0.66, t, 1.0), "Metal_Mid", verts=8))
        if k in (4, 5, 6):
            bend = 0.18 + 0.1 * (k == 5)
            P.append(tube([(0.66, t, 0.08), (0.66, t, 0.7), (0.66 + bend, t + random.uniform(-0.05, 0.05), 1.1),
                           (0.66, t, 1.5), (0.66, t, 1.96)], 0.012, "Metal_Mid", res=6))
        else:
            P.append(cyl(0.012, 1.92, (0.66, t, 1.0), "Metal_Mid", verts=8))
    # The door, torn off its top hinge, hanging outwards.
    door = []
    for k in range(6):
        door.append(cyl(0.012, 1.8, (-0.5 + k * 0.11, 0, 0.95), "Metal_Mid", verts=8))
    for z in (0.15, 0.95, 1.75):
        door.append(box((0.62, 0.03, 0.04), (-0.22, 0, z), "Metal_Dark"))
    for o in door:
        o.select_set(False)
    d = join(door, "door_tmp")
    d.rotation_euler = (math.radians(-14), 0, math.radians(-115))
    d.location = (0.62, -0.66, 0.02)
    bpy.ops.object.select_all(action="DESELECT")
    d.select_set(True)
    bpy.context.view_layer.objects.active = d
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=False)
    P.append(d)
    P += [blob(0.4, (0, 0, 0.08), "Blood_Dry", 51, rough=0.4, thick=0.005),
          tube([(-0.6, 0.6, 1.6), (-0.4, 0.3, 0.9), (-0.2, 0.1, 0.12), (0.2, -0.2, 0.09)], 0.015, "Metal_Dark", res=6)]  # chain
    return join(P, "Horror_Cage")


def claw_marks():
    """Four deep gouges on a wall (the wall is at +y, they face -y), about 1.3 m long."""
    random.seed(19)
    P = []
    for k in range(4):
        x0 = -0.24 + k * 0.15
        pts = []
        for st in range(7):
            t = st / 6.0
            pts.append((x0 + t * 0.42 + 0.035 * math.sin(t * 3 + k), 0.005, 2.0 - t * 1.3))
        P.append(tube(pts, 0.03 - 0.004 * abs(k - 1.5), "Metal_Dark", res=6))
        for p in pts[1:-1]:
            P.append(box((0.05, 0.012, 0.09), (p[0] + 0.035, -0.012, p[2]), "Metal_Mid", rot=(0, -22, 0)))
            P.append(box((0.04, 0.01, 0.07), (p[0] - 0.03, -0.01, p[2] + 0.02), "Metal_Mid", rot=(0, -18, 0)))
        P.append(box((0.02, 0.006, 0.25), (pts[-1][0] + 0.02, -0.008, pts[-1][2] - 0.1), "Blood_Dry", rot=(0, -15, 0)))
    return join(P, "Horror_ClawMarks")


def papers():
    random.seed(29)
    P = []
    for k in range(18):
        a = random.uniform(0, 6.28)
        d = random.uniform(0, 1.1) ** 1.3
        P.append(box((0.21, 0.297, 0.0015), (math.cos(a) * d, math.sin(a) * d, 0.001 + k * 0.0016), "Paper",
                     rot=(random.uniform(-1, 1), random.uniform(-1, 1), random.uniform(0, 180))))
    P.append(box((0.24, 0.33, 0.012), (0.5, -0.4, 0.006), "Wood", rot=(0, 0, 30)))
    P.append(box((0.21, 0.297, 0.002), (0.5, -0.4, 0.013), "Paper", rot=(0, 0, 30)))
    P.append(blob(0.07, (-0.3, 0.25, 0.03), "Blood_Dry", 77, rough=0.4, thick=0.002))
    return join(P, "Horror_Papers")


def biohazard_bin():
    P = [cyl(0.3, 0.7, (0, 0, 0.35), "Paint_Red", verts=24, bevel=0.01),
         cyl(0.32, 0.06, (0, 0, 0.72), "Paint_Red", verts=24, bevel=0.01),
         cyl(0.305, 0.12, (0, 0, 0.45), "Paint_Yellow", verts=24),
         box((0.2, 0.04, 0.04), (0, -0.31, 0.65), "Metal_Dark")]
    return join(P, "Horror_BiohazardBin")


def specimen_shelf():
    """Steel shelf of specimen jars, things floating in them; back at +y."""
    random.seed(37)
    P = []
    for x in (-0.78, 0.78):
        for y in (-0.22, 0.22):
            P.append(box((0.04, 0.04, 1.95), (x, y, 0.975), "Metal_Dark"))
    for z in (0.12, 0.65, 1.18, 1.71):
        P.append(box((1.6, 0.48, 0.025), (0, 0, z), "Metal_Mid"))
        x = -0.65
        while x < 0.68:
            r = random.uniform(0.06, 0.1)
            h = random.uniform(0.2, 0.36)
            if random.random() < 0.85:
                P.append(cyl(r, h, (x, 0, z + 0.0125 + h / 2), "Glass", verts=16))
                P.append(cyl(r * 0.92, h * 0.85, (x, 0, z + 0.0125 + h * 0.43), "Liquid", verts=14))
                P.append(cyl(r + 0.005, 0.03, (x, 0, z + 0.0125 + h + 0.015), "Metal_Mid", verts=16))
                P.append(meta([((x + random.uniform(-0.02, 0.02), random.uniform(-0.02, 0.02), z + 0.0125 + h * (0.3 + 0.1 * j)),
                                 r * random.uniform(0.25, 0.4)) for j in range(3)], "Blood_Dry", resolution=0.02))
            x += r * 2 + 0.05
    return join(P, "Lab_SpecimenShelf")


def gas_cylinders():
    P = [box((0.5, 1.4, 0.05), (0, 0, 0.025), "Metal_Dark"),
         box((0.06, 1.4, 0.06), (0.22, 0, 1.2), "Paint_Yellow"), box((0.06, 1.4, 0.06), (0.22, 0, 0.6), "Paint_Yellow")]
    for y in (-0.68, 0.68):
        P.append(box((0.06, 0.06, 1.3), (0.22, y, 0.65), "Paint_Yellow"))
    for k, col in enumerate(("Paint_Blue", "Metal_Mid", "Paint_Red", "Paint_Blue")):
        y = -0.48 + k * 0.32
        P += [cyl(0.12, 1.3, (0, y, 0.7), col, verts=20),
              cyl(0.03, 0.12, (0, y, 1.41), "Metal_Mid", verts=10),
              torus(0.04, 0.01, (0, y, 1.48), "Metal_Dark", major=12, minor=6)]
    P.append(tube([(0.2, -0.68, 1.0), (0.13, -0.2, 0.98), (0.13, 0.2, 0.98), (0.2, 0.68, 1.0)], 0.01, "Metal_Mid", res=4))
    return join(P, "Prop_GasCylinders")


def tool_cart():
    P = [box((0.9, 0.5, 0.86), (0, 0, 0.53), "Paint_Red", bevel=0.02),
         box((0.94, 0.54, 0.03), (0, 0, 0.97), "Rubber", bevel=0.01),
         cyl(0.015, 0.5, (0.5, 0, 0.85), "Metal_Mid", verts=8, rot=(90, 0, 0))]
    for y in (-0.22, 0.22):
        P.append(box((0.04, 0.03, 0.03), (0.47, y, 0.85), "Metal_Mid"))
    for k in range(5):
        z = 0.88 - k * 0.15
        P.append(box((0.82, 0.01, 0.12), (0, -0.255, z - 0.05), "Paint_Red", bevel=0.003))
        P.append(handle((0, -0.265, z - 0.02), "Metal_Mid", 0.4))
    for x in (-0.38, 0.38):
        for y in (-0.2, 0.2):
            P.append(cyl(0.05, 0.035, (x, y, 0.05), "Rubber", verts=12, rot=(90, 0, 0)))
    P += [box((0.3, 0.025, 0.05), (-0.2, 0.05, 1.0), "Metal_Mid", rot=(0, 0, 30)),
          cyl(0.025, 0.25, (0.15, -0.05, 1.0), "Paint_Yellow", verts=8, rot=(0, 90, 15))]
    return join(P, "Prop_ToolCart")


def warning_sign():
    """Folding A-frame 'danger' sign, 0.9 m tall, black and yellow chevrons."""
    P = []
    for s in (-1, 1):
        P.append(box((0.5, 0.025, 0.85), (0, s * 0.14, 0.43), "Paint_Yellow", bevel=0.01, rot=(s * 9, 0, 0)))
        for k in range(3):
            P.append(box((0.42, 0.006, 0.06), (0, s * (0.157 + 0.0), 0.55 + k * 0.1), "Metal_Dark", rot=(s * 9, 30, 0)))
    P.append(box((0.5, 0.3, 0.04), (0, 0, 0.88), "Paint_Yellow", bevel=0.01))
    return join(P, "Prop_WarningSign")


def build_horror():
    made = []
    for fn in (desk, monitor, office_chair, lambda: office_chair(True), workbench, metal_shelf,
               lambda: metal_shelf("Furn_MetalShelf_B", 9), cot, filing_cabinet,
               lambda: filing_cabinet(True, "Furn_FilingCabinet_Open"), whiteboard, mop_bucket, trash_bags,
               item_bottle, item_can, covered_body, body_bag, gurney, lambda: gurney(True), blood_pool,
               blood_trail, cage, claw_marks, papers, biohazard_bin, specimen_shelf, gas_cylinders, tool_cart,
               warning_sign, cable_drum):
        clear()
        o = fn()
        export([o], o.name)
        made.append((o.name, len(o.data.polygons)))
    return made


def locker():
    """Double steel locker, 1.07 wide, 0.7 deep, 2.14 tall; doors towards -y; vents, handles, a dent."""
    P = [box((1.07, 0.66, 2.06), (0, 0.02, 1.03), "Metal_Mid", bevel=0.012),
         box((1.12, 0.72, 0.06), (0, 0.0, 2.11), "Metal_Dark", bevel=0.01),
         box((1.1, 0.66, 0.08), (0, 0.02, 0.04), "Metal_Dark", bevel=0.01)]
    for side, x in ((-1, -0.27), (1, 0.27)):
        P.append(box((0.5, 0.025, 1.92), (x, -0.325, 1.06), "Metal_Mid", bevel=0.006))
        for k in range(6):
            P.append(box((0.3, 0.012, 0.022), (x, -0.34, 1.72 + k * 0.045), "Metal_Dark"))
        for k in range(4):
            P.append(box((0.3, 0.012, 0.022), (x, -0.34, 0.25 + k * 0.045), "Metal_Dark"))
        P.append(box((0.025, 0.04, 0.16), (x - side * 0.2, -0.355, 1.05), "Metal_Dark", bevel=0.005))
        P.append(box((0.07, 0.006, 0.04), (x, -0.341, 1.45), "Paper"))
    P.append(box((0.012, 0.03, 1.9), (0, -0.33, 1.06), "Metal_Dark"))
    P.append(box((0.22, 0.02, 0.3), (0.3, -0.348, 0.9), "Metal_Dark", rot=(4, 0, 12)))   # dent
    return join(P, "Furn_Locker")


def build_fixes():
    made = []
    for fn in (covered_body, cot, claw_marks, locker):
        clear()
        o = fn()
        export([o], o.name)
        made.append((o.name, len(o.data.polygons)))
    return made


def breaker_door():
    """Breaker room door leaf, 1.42 x 2.6 x 0.1. Pivot on the hinge edge; the leaf runs to Blender -x
    (Unity +x, as the Door's Hinge expects), faces +-y. Inset panels, wired window, push plate,
    lever handles both sides, three hinge knuckles."""
    W, H, T = 1.42, 2.6, 0.1
    cx = -W / 2
    P = [box((W - 0.01, T, H - 0.01), (cx, 0, H / 2), "Metal_Mid", bevel=0.012)]
    for side in (-1, 1):
        y = side * (T / 2 + 0.006)
        P.append(box((W - 0.16, 0.012, 0.9), (cx, y, 0.62), "Metal_Dark", bevel=0.006))     # kick panel
        P.append(box((W - 0.16, 0.012, 0.75), (cx, y, 1.52), "Metal_Dark", bevel=0.006))    # middle
        P.append(box((0.5, 0.014, 0.42), (cx, y, 2.15), "Metal_Dark", bevel=0.008))        # window frame
        P.append(box((0.42, 0.016, 0.34), (cx, y * 1.05, 2.15), "Glass"))
        for k in range(1, 4):
            P.append(box((0.008, 0.018, 0.34), (cx - 0.21 + k * 0.105, y * 1.06, 2.15), "Grate"))
        P.append(box((0.28, 0.01, 0.06), (cx, y, 1.86), "Paint_Yellow"))                    # warning strip
        # Lever handle near the free edge (Unity local x 1.25 -> Blender -1.25).
        hy = side * (T / 2 + 0.035)
        P.append(cyl(0.035, 0.05, (-1.25, side * (T / 2 + 0.02), 1.1), "Metal_Dark", verts=16, rot=(90, 0, 0)))
        P.append(box((0.17, 0.03, 0.03), (-1.17, hy, 1.1), "Metal_Dark", bevel=0.008))
        P.append(box((0.08, 0.01, 0.2), (-1.25, side * (T / 2 + 0.008), 0.92), "Metal_Dark"))  # lock plate
    for z in (0.3, 1.3, 2.3):
        P.append(cyl(0.025, 0.18, (0.0, 0.0, z), "Metal_Dark", verts=12))
    return join(P, "Door_Breaker")


def build_door():
    clear()
    o = breaker_door()
    export([o], o.name)
    return (o.name, len(o.data.polygons))
