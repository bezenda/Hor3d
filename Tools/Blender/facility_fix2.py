# Playtest fixes 2026-11-11: breaker panel + switches, door that fills its frame, valve wheels on the
# pipe risers, battery, wall emergency lamp, papers without the black lump. Same conventions as
# facility_assets.py (Z up, metres, front towards Blender -y = Unity +z, pivot = world origin).
# Run build_fix2() through the Blender bridge.
exec(open(r"C:/Users/Admin/3D projects/My project/Tools/Blender/facility_horror.py").read())


def breaker_panel():
    """Breaker enclosure 0.9 x 0.85 x 0.1, pivot at its centre (the old Panel box), door hanging
    open on the left, a label strip above each switch, conduit up into the wall."""
    P = [box((0.9, 0.1, 0.85), (0, 0, 0), "Paint_Blue", bevel=0.012),
         box((0.82, 0.012, 0.77), (0, -0.05, 0), "Metal_Dark", bevel=0.004)]          # recessed tray
    for x in (-0.26, 0.0, 0.26):
        P.append(box((0.2, 0.008, 0.05), (x, -0.058, 0.25), "Paper"))                  # labels
        P.append(box((0.22, 0.01, 0.34), (x, -0.054, -0.04), "Metal_Mid", bevel=0.004))  # mounts
    P.append(box((0.2, 0.006, 0.12), (0.28, -0.058, -0.32), "Paint_Yellow"))         # warning sticker
    # Open door: hinged on the left edge (Blender +x is Unity -x), turned out ~110 degrees.
    door = box((0.9, 0.025, 0.85), (0, 0, 0), "Paint_Blue", bevel=0.01)
    door.location = (0.45 + 0.45 * 0.342, -0.06 - 0.45 * 0.94, 0)
    door.rotation_euler = (0, 0, math.radians(-70))
    P.append(door)
    P.append(cyl(0.012, 0.85, (0.46, -0.06, 0), "Metal_Dark", verts=10))              # hinge pin
    P.append(cyl(0.035, 0.6, (-0.3, 0.0, 0.72), "Metal_Mid", verts=14))               # conduit
    P.append(box((0.1, 0.06, 0.05), (-0.3, 0.0, 0.43), "Metal_Dark", bevel=0.005))
    return join(P, "Breaker_Panel")


def breaker_switch_base():
    """Housing of one breaker switch, in the switch's own frame: back at Unity z -0.025, boss round
    the lever pivot (Unity 0, -0.02, 0.09). ON/OFF marks above and below the slot."""
    P = [box((0.16, 0.085, 0.26), (0, -0.0175, 0), "Metal_Dark", bevel=0.01),
         box((0.12, 0.03, 0.2), (0, -0.07, -0.01), "Metal_Mid", bevel=0.006),
         box((0.05, 0.012, 0.15), (0, -0.087, -0.01), "Rubber"),                          # slot
         cyl(0.03, 0.04, (0, -0.075, -0.02), "Metal_Mid", verts=16, rot=(90, 0, 0)),
         box((0.07, 0.006, 0.018), (0, -0.088, 0.1), "Emit_Cyan"),                        # ON mark
         box((0.07, 0.006, 0.018), (0, -0.088, -0.115), "Emit_Red")]                      # OFF mark
    for x in (-0.06, 0.06):
        for z in (-0.11, 0.11):
            P.append(cyl(0.008, 0.01, (x, -0.04, z), "Metal_Mid", verts=8, rot=(90, 0, 0)))
    return join(P, "Breaker_SwitchBase")


def breaker_lever():
    """The handle; pivot = the Lever transform, shaft along Unity +y, red grip at the end."""
    P = [cyl(0.012, 0.15, (0, 0, 0.075), "Metal_Mid", verts=12),
         cyl(0.022, 0.07, (0, 0, 0.15), "Paint_Red", verts=16),
         cyl(0.024, 0.012, (0, 0, 0.188), "Rubber", verts=16),
         cyl(0.02, 0.03, (0, 0, 0.0), "Metal_Dark", verts=12, rot=(0, 90, 0))]
    return join(P, "Breaker_Lever")


def breaker_door2():
    """Door leaf sized to the kit frame (1.62 x 2.45, top corners cut to the frame's chamfer).
    Hinge on the origin, the leaf runs to Blender -x (Unity +x); starts 0.09 past the hinge line so
    it covers the jamb on both sides."""
    W, H, T, C = 1.62, 2.45, 0.08, 0.22
    x0 = 0.09                      # leaf edge, Blender +x of the hinge
    cx = x0 - W / 2
    import bmesh as _bm
    mesh = bpy.data.meshes.new("leaf")
    o = bpy.data.objects.new("leaf", mesh)
    bpy.context.collection.objects.link(o)
    bm = _bm.new()
    outline = [(x0, 0), (x0 - W, 0), (x0 - W, H - C), (x0 - W + C, H), (x0 - C, H), (x0, H - C)]
    for side in (-1, 1):
        verts = [bm.verts.new((x, side * T / 2, z)) for x, z in outline]
        bm.faces.new(verts if side > 0 else list(reversed(verts)))
    bm.verts.ensure_lookup_table()
    n = len(outline)
    for i in range(n):
        a, b = i, (i + 1) % n
        bm.faces.new((bm.verts[a], bm.verts[b], bm.verts[n + b], bm.verts[n + a]))
    _bm.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    _finish(o, "Metal_Mid", bevel=0.01)
    P = [o]
    for side in (-1, 1):
        y = side * (T / 2 + 0.006)
        P.append(box((W - 0.2, 0.012, 0.95), (cx, y, 0.6), "Metal_Dark", bevel=0.006))
        P.append(box((W - 0.2, 0.012, 0.6), (cx, y, 1.45), "Metal_Dark", bevel=0.006))
        P.append(box((W - 0.2, 0.014, 0.05), (cx, y, 1.1), "Paint_Yellow"))
        P.append(box((0.56, 0.014, 0.4), (cx, y, 2.02), "Metal_Dark", bevel=0.008))
        P.append(box((0.48, 0.016, 0.32), (cx, y * 1.05, 2.02), "Glass"))
        for k in range(1, 4):
            P.append(box((0.008, 0.018, 0.32), (cx - 0.24 + k * 0.12, y * 1.06, 2.02), "Grate"))
        hy = side * (T / 2 + 0.035)
        P.append(cyl(0.035, 0.05, (x0 - W + 0.16, side * (T / 2 + 0.02), 1.05), "Metal_Dark", verts=16, rot=(90, 0, 0)))
        P.append(box((0.17, 0.03, 0.03), (x0 - W + 0.24, hy, 1.05), "Metal_Dark", bevel=0.008))
        P.append(box((0.08, 0.01, 0.2), (x0 - W + 0.16, side * (T / 2 + 0.008), 0.87), "Metal_Dark"))
    for z in (0.3, 1.2, 2.1):
        P.append(cyl(0.025, 0.18, (0.0, 0.0, z), "Metal_Dark", verts=12))
    return join(P, "Door_Breaker")


def pipe_riser2():
    """9 m pipe bundle; the valve sits on a stem out of the yellow pipe, hand wheel with spokes."""
    P = []
    for i, (dx, r) in enumerate(((-0.25, 0.12), (0.05, 0.09), (0.3, 0.07))):
        P.append(cyl(r, 9.0, (dx, 0.25, 4.5), "Metal_Mid" if i != 1 else "Paint_Yellow", verts=16))
        for z in (1.0, 4.0, 7.0):
            P.append(cyl(r + 0.03, 0.12, (dx, 0.25, z), "Metal_Dark", verts=16, smooth=False))
    for z in (1.6, 4.6, 7.6):
        P.append(box((0.8, 0.3, 0.08), (0.02, 0.4, z), "Metal_Dark"))
    # Valve body round the yellow pipe, bonnet out to the front, wheel on the stem.
    vx, vz = 0.05, 1.35
    P.append(cyl(0.125, 0.22, (vx, 0.25, vz), "Metal_Dark", verts=20, smooth=False))
    P.append(cyl(0.05, 0.14, (vx, 0.1, vz), "Metal_Dark", verts=14, rot=(90, 0, 0)))
    P.append(cyl(0.016, 0.12, (vx, 0.0, vz), "Metal_Mid", verts=10, rot=(90, 0, 0)))
    wy = -0.05
    P.append(torus(0.13, 0.016, (vx, wy, vz), "Paint_Red", rot=(90, 0, 0), major=32, minor=8))
    P.append(cyl(0.028, 0.035, (vx, wy, vz), "Paint_Red", verts=12, rot=(90, 0, 0)))
    for k in range(4):
        P.append(box((0.24, 0.014, 0.016), (vx, wy, vz), "Paint_Red", rot=(0, k * 45 + 22.5, 0)))
    return join(P, "Hall_PipeRiser")


def item_battery():
    """Power cell 0.062 x 0.115, pivot at its centre (as the placeholder cylinder)."""
    P = [cyl(0.031, 0.1, (0, 0, -0.005), "Metal_Dark", verts=20),
         cyl(0.0315, 0.06, (0, 0, -0.005), "Paint_Yellow", verts=20),
         cyl(0.0318, 0.008, (0, 0, 0.028), "Rubber", verts=20),
         cyl(0.024, 0.006, (0, 0, 0.048), "Metal_Mid", verts=16),
         cyl(0.009, 0.01, (0, 0, 0.055), "Metal_Mid", verts=12),
         cyl(0.026, 0.004, (0, 0, -0.057), "Metal_Mid", verts=16),
         box((0.006, 0.002, 0.02), (0, -0.032, 0.005), "Emit_Cyan")]
    return join(P, "Item_Battery")


def emergency_lamp():
    """Wall beacon: plate on the wall (Blender +y = wall side, y 0), red dome out to -y, wire cage."""
    P = [box((0.2, 0.03, 0.2), (0, -0.015, 0), "Metal_Dark", bevel=0.006),
         cyl(0.07, 0.03, (0, -0.045, 0), "Metal_Mid", verts=20, rot=(90, 0, 0))]
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.065, segments=20, ring_count=10, location=(0, -0.06, 0))
    dome = bpy.context.active_object
    dome.rotation_euler = (math.radians(90), 0, 0)
    dome.scale = (1, 1, 1)
    _finish(dome, "Emit_Red", smooth=True)
    P.append(dome)
    for k in range(4):
        P.append(box((0.006, 0.08, 0.006), (0, -0.09, 0), "Metal_Dark", rot=(0, k * 45, 0)))
    P.append(torus(0.075, 0.004, (0, -0.06, 0), "Metal_Dark", rot=(90, 0, 0), major=24, minor=6))
    return join(P, "Prop_EmergencyLamp")


def papers2():
    random.seed(29)
    P = []
    for k in range(18):
        a = random.uniform(0, 6.28)
        d = random.uniform(0, 1.1) ** 1.3
        P.append(box((0.21, 0.297, 0.0015), (math.cos(a) * d, math.sin(a) * d, 0.001 + k * 0.0016), "Paper",
                     rot=(random.uniform(-1, 1), random.uniform(-1, 1), random.uniform(0, 180))))
    P.append(box((0.24, 0.33, 0.012), (0.5, -0.4, 0.006), "Wood", rot=(0, 0, 30)))
    P.append(box((0.21, 0.297, 0.002), (0.5, -0.4, 0.013), "Paper", rot=(0, 0, 30)))
    return join(P, "Horror_Papers")


def catwalk_post(h=4.5):
    """Support post from the floor up to a landing 4.5 m up: yellow box section, base plate, bolts."""
    P = [box((0.2, 0.2, h - 0.03), (0, 0, (h - 0.03) / 2 + 0.015), "Paint_Yellow", bevel=0.01),
         box((0.34, 0.34, 0.015), (0, 0, 0.0075), "Metal_Dark", bevel=0.003),
         box((0.26, 0.26, 0.012), (0, 0, h - 0.006), "Metal_Dark")]
    for x in (-0.13, 0.13):
        for y in (-0.13, 0.13):
            P.append(cyl(0.014, 0.025, (x, y, 0.02), "Metal_Mid", verts=6))
    for z in (0.6, h * 0.5, h - 0.4):
        P.append(box((0.21, 0.21, 0.03), (0, 0, z), "Metal_Dark"))
    return join(P, "Catwalk_Post")


def crate_stack(name="Prop_CrateStack", seed=3, levels=3):
    """Pallet with wrapped cartons stacked up to ~1.7 m, a few boxes off-square."""
    random.seed(seed)
    P = [box((1.2, 1.0, 0.03), (0, 0, 0.125), "Wood"), box((1.2, 1.0, 0.03), (0, 0, 0.015), "Wood")]
    for x in (-0.55, 0, 0.55):
        P.append(box((0.1, 1.0, 0.08), (x, 0, 0.07), "Wood"))
    z = 0.14
    for level in range(levels):
        h = random.choice((0.42, 0.5, 0.55))
        for ix, x in enumerate((-0.3, 0.3)):
            for iy, y in enumerate((-0.25, 0.25)):
                if level == levels - 1 and random.random() < 0.35:
                    continue
                P.append(box((0.58, 0.48, h - 0.01), (x + random.uniform(-0.02, 0.02), y + random.uniform(-0.02, 0.02), z + h / 2),
                             "Cardboard", bevel=0.005, rot=(0, 0, random.uniform(-4, 4))))
        z += h
    P.append(box((1.215, 1.015, 0.36), (0, 0, 0.14 + 0.2), "Wrap"))      # stretch wrap round the bottom
    return join(P, name)


def chain_hoist():
    """Chain hoist hanging from the roof: beam clamp at 8.6 m, chain, hook block at 3.4 m."""
    P = [box((0.5, 0.2, 0.12), (0, 0, 8.62), "Metal_Dark", bevel=0.01),
         box((0.36, 0.3, 0.32), (0, 0, 8.36), "Paint_Yellow", bevel=0.02),
         cyl(0.12, 0.34, (0, 0, 8.36), "Metal_Dark", verts=16, rot=(90, 0, 0))]
    z = 8.15
    k = 0
    while z > 3.75:
        P.append(torus(0.03, 0.009, (0.05, 0, z), "Metal_Mid", rot=(90 if k % 2 else 0, 90, 0), major=10, minor=5))
        z -= 0.05
        k += 1
    z = 8.15
    k = 0
    while z > 6.9:
        P.append(torus(0.03, 0.009, (-0.1, 0, z), "Metal_Mid", rot=(90 if k % 2 else 0, 90, 0), major=10, minor=5))
        z -= 0.05
        k += 1
    P.append(box((0.16, 0.12, 0.28), (0.05, 0, 3.6), "Paint_Yellow", bevel=0.015))
    P.append(torus(0.08, 0.022, (0.05, 0, 3.35), "Metal_Dark", rot=(90, 0, 0), major=20, minor=8))
    return join(P, "Hall_ChainHoist")


def build_fix2():
    made = []
    for fn in (breaker_panel, breaker_switch_base, breaker_lever, breaker_door2, pipe_riser2,
               item_battery, emergency_lamp, papers2, catwalk_post, crate_stack, chain_hoist,
               lambda: crate_stack("Prop_CrateStack_B", seed=8, levels=2)):
        clear()
        o = fn()
        export([o], o.name)
        made.append((o.name, len(o.data.polygons)))
    return made
