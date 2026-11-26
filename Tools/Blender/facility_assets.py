# Facility prop set. Each function models one asset around the origin (Z up, metres) and
# returns the joined object; build_all() exports every asset to Assets/Art/Generated/Facility/Models.
exec(open(r"C:/Users/Admin/3D projects/My project/Tools/Blender/facility_kit.py").read())
import random


def capsule(broken=False, seed=1):
    random.seed(seed)
    P = []
    P.append(cyl(1.25, 0.25, (0, 0, 0.125), "Metal_Dark", verts=8, bevel=0.03, smooth=False))
    P.append(cyl(1.05, 0.2, (0, 0, 0.35), "Metal_Mid", verts=8, bevel=0.02, smooth=False))
    P.append(torus(1.12, 0.025, (0, 0, 0.26), "Emit_Cyan"))
    P.append(cyl(0.92, 0.35, (0, 0, 0.62), "Metal_Mid", verts=32, bevel=0.02))
    P += ring_of(12, 0.88, 0.62, lambda x, y, z, a: cyl(0.035, 0.38, (x * 1.03, y * 1.03, z), "Metal_Dark", verts=6, smooth=False))
    if not broken:
        P.append(cyl(0.78, 2.4, (0, 0, 2.0), "Glass", verts=48))
        P.append(cyl(0.72, 2.1, (0, 0, 1.95), "Liquid", verts=40))
    else:
        for i in range(16):
            a = 2 * math.pi * i / 16
            h = 0.25 + random.random() * 1.1
            P.append(box((0.3, 0.02, h), (math.cos(a) * 0.78, math.sin(a) * 0.78, 0.8 + h / 2), "Glass",
                         rot=(random.uniform(-8, 8), random.uniform(-6, 6), math.degrees(a) + 90)))
        P.append(cyl(0.74, 0.03, (0, 0, 0.81), "Liquid", verts=32))
    P += ring_of(4, 0.86, 2.0, lambda x, y, z, a: box((0.12, 0.16, 2.6), (x, y, z), "Metal_Dark", bevel=0.015, rot=(0, 0, math.degrees(a))))
    for zz in (1.2, 2.85):
        P.append(torus(0.83, 0.035, (0, 0, zz), "Metal_Mid"))
    P.append(cyl(0.96, 0.42, (0, 0, 3.41), "Metal_Mid", verts=32, bevel=0.02))
    P.append(torus(0.97, 0.03, (0, 0, 3.2), "Emit_Cyan"))
    P.append(cyl(0.82, 0.28, (0, 0, 3.76), "Metal_Dark", verts=32, bevel=0.03))
    P.append(box((0.7, 0.55, 0.38), (0, 0, 4.08), "Metal_Dark", bevel=0.03))
    P.append(box((0.3, 0.02, 0.12), (0, -0.28, 4.12), "Emit_Red" if broken else "Emit_Cyan"))
    for ang in (35, 145, 270):
        r = math.radians(ang); c, s = math.cos(r), math.sin(r)
        P.append(tube([(c * 0.95, s * 0.95, 3.45), (c * 1.35, s * 1.35, 3.35), (c * 1.6, s * 1.6, 2.4),
                       (c * 1.55, s * 1.55, 0.6), (c * 1.85, s * 1.85, 0.06)], 0.055, "Rubber"))
    for dx in (-0.18, 0.18):
        P.append(cyl(0.07, 4.7, (dx, 0.1, 6.6), "Metal_Mid", verts=12))
        P.append(cyl(0.1, 0.12, (dx, 0.1, 4.3), "Metal_Dark", verts=12, smooth=False))
    P.append(box((0.45, 0.3, 1.0), (0, -1.55, 0.5), "Metal_Dark", bevel=0.02))
    P.append(box((0.5, 0.36, 0.05), (0, -1.6, 1.05), "Metal_Mid", rot=(-30, 0, 0)))
    P.append(box((0.4, 0.27, 0.01), (0, -1.615, 1.08), "Emit_Red" if broken else "Emit_Cyan", rot=(-30, 0, 0)))
    return join(P, "Lab_CapsuleTank_Broken" if broken else "Lab_CapsuleTank")


def wall_panel(name, panel_mat, accent_mat, light_mat):
    # 6 m wide (x 0..6), 9 m tall (z), depth 0..0.5 towards +y (into the room).
    P = []
    P.append(box((6, 0.06, 9), (3, 0.03, 4.5), "Metal_Dark"))
    for x in (0.0, 3.0, 6.0):
        w = 0.3 if x in (0.0, 6.0) else 0.4
        P.append(box((w, 0.42, 9), (x, 0.21, 4.5), "Metal_Mid", bevel=0.03))
    P.append(box((6, 0.32, 0.9), (3, 0.16, 0.45), "Metal_Dark", bevel=0.02))
    P.append(box((6, 0.36, 0.08), (3, 0.18, 0.94), accent_mat, bevel=0.01))
    for cx in (1.5, 4.5):
        for (z0, z1) in ((1.15, 2.55), (2.7, 4.05)):
            P.append(box((2.5, 0.12, z1 - z0 - 0.08), (cx, 0.1, (z0 + z1) / 2), panel_mat, bevel=0.025))
        for k in range(7):
            P.append(box((2.3, 0.06, 0.07), (cx, 0.12, 5.05 + k * 0.16), "Metal_Dark", rot=(-25, 0, 0)))
        P.append(box((2.5, 0.14, 1.6), (cx, 0.07, 7.4), panel_mat, bevel=0.02))
    P.append(box((5.4, 0.2, 0.22), (3, 0.12, 4.38), "Metal_Dark"))
    P.append(box((5.2, 0.04, 0.08), (3, 0.23, 4.38), light_mat))
    for zz, rr in ((6.25, 0.09), (6.5, 0.06)):
        P.append(cyl(rr, 6.0, (3, 0.42, zz), "Metal_Mid", verts=14, rot=(0, 90, 0)))
    for x in (0.8, 3.0, 5.2):
        P.append(box((0.1, 0.32, 0.45), (x, 0.3, 6.37), "Metal_Dark"))
    P.append(box((6, 0.4, 0.05), (3, 0.3, 8.1), "Grate"))
    for k in range(4):
        P.append(cyl(0.035, 6.0, (3, 0.18 + k * 0.08, 8.15), "Rubber", verts=8, rot=(0, 90, 0)))
    return join(P, name)


def console():
    P = []
    P.append(box((2.4, 0.8, 0.85), (0, 0.1, 0.425), "Metal_Dark", bevel=0.02))
    P.append(box((2.5, 0.95, 0.06), (0, 0, 0.88), "Paint_White", bevel=0.01))
    P.append(box((2.3, 0.3, 0.12), (0, -0.25, 0.95), "Metal_Mid", rot=(-12, 0, 0)))
    for i, dx in enumerate((-0.8, 0, 0.8)):
        P.append(box((0.7, 0.05, 0.44), (dx, 0.32, 1.35), "Metal_Dark", bevel=0.01, rot=(10, 0, -(dx * 14))))
        P.append(box((0.62, 0.01, 0.36), (dx, 0.29, 1.35), "Screen" if i != 1 else "Emit_Cyan", rot=(10, 0, -(dx * 14))))
        P.append(box((0.08, 0.08, 0.35), (dx, 0.36, 1.05), "Metal_Mid"))
    for k in range(5):
        P.append(box((0.04, 0.04, 0.02), (-0.6 + k * 0.3, -0.33, 0.99), "Emit_Red" if k % 2 else "Emit_Cyan"))
    return join(P, "Lab_Console")


def server_rack():
    random.seed(3)
    P = [box((0.8, 1.0, 2.2), (0, 0, 1.1), "Metal_Dark", bevel=0.02), box((0.7, 0.02, 2.0), (0, -0.505, 1.1), "Grate")]
    for k in range(14):
        P.append(box((0.6, 0.03, 0.09), (0, -0.52, 0.2 + k * 0.135), "Metal_Mid"))
        if random.random() < 0.6:
            P.append(box((0.03, 0.01, 0.03), (0.22, -0.54, 0.2 + k * 0.135), "Emit_Cyan" if random.random() < 0.7 else "Emit_Red"))
    return join(P, "Lab_ServerRack")


def truss():
    P = []
    for y in (-0.4, 0.4):
        for z in (-0.08, -0.85):
            P.append(box((6, 0.1, 0.1), (3, y, z), "Metal_Mid"))
    for k in range(7):
        for y in (-0.4, 0.4):
            P.append(box((0.07, 0.07, 0.8), (k, y, -0.46), "Metal_Dark"))
        if k < 6:
            for y in (-0.4, 0.4):
                P.append(box((0.06, 0.06, 1.25), (k + 0.5, y, -0.46), "Metal_Dark", rot=(0, 52 if k % 2 else -52, 0)))
    for k, sag in enumerate((1.6, 2.2, 1.3)):
        P.append(tube([(0, -0.25 + k * 0.2, -0.85), (3, -0.25 + k * 0.2, -0.85 - sag), (6, -0.25 + k * 0.2, -0.85)], 0.03, "Rubber"))
    return join(P, "Ceiling_Truss")


def trench():
    P = [box((6, 1.0, 0.02), (3, 0, 0.01), "Grate")]
    for k in range(25):
        P.append(box((0.03, 0.96, 0.025), (0.12 + k * 0.24, 0, 0.0125), "Metal_Dark"))
    for y in (-0.5, 0.5):
        P.append(box((6, 0.06, 0.03), (3, y, 0.015), "Metal_Mid"))
    P.append(box((6, 0.04, 0.006), (3, 0.47, 0.03), "Emit_Cyan"))
    return join(P, "Lab_FloorTrench")


def containment():
    random.seed(11)
    P = []
    P.append(cyl(3.7, 0.18, (0, 0, 0.09), "Metal_Dark", verts=12, bevel=0.03, smooth=False))
    P.append(cyl(3.25, 0.2, (0, 0, 0.28), "Metal_Mid", verts=12, bevel=0.03, smooth=False))
    P.append(torus(3.3, 0.03, (0, 0, 0.39), "Emit_Red", major=64))
    P.append(cyl(2.1, 0.45, (0, 0, 0.6), "Metal_Dark", verts=40, bevel=0.03))
    P.append(cyl(1.92, 0.03, (0, 0, 0.83), "Liquid", verts=40))
    P += ring_of(16, 2.06, 0.6, lambda x, y, z, a: cyl(0.05, 0.5, (x, y, z), "Metal_Mid", verts=6, smooth=False))
    for i in range(8):
        a = 2 * math.pi * i / 8
        x, y = math.cos(a) * 2.25, math.sin(a) * 2.25
        P.append(box((0.26, 0.38, 5.6), (x, y, 3.6), "Metal_Mid", bevel=0.03, rot=(0, 0, math.degrees(a))))
        P.append(box((0.1, 0.42, 0.5), (x * 1.02, y * 1.02, 1.1), "Paint_Yellow", rot=(0, 0, math.degrees(a))))
    for zz in (2.4, 4.6):
        P.append(torus(2.25, 0.07, (0, 0, zz), "Metal_Dark", major=64))
    P.append(torus(2.3, 0.2, (0, 0, 6.4), "Metal_Dark", major=64, minor=12))
    P.append(cyl(1.7, 1.0, (0, 0, 6.9), "Metal_Mid", verts=40, bevel=0.04))
    P.append(cyl(1.2, 0.5, (0, 0, 7.6), "Metal_Dark", verts=32, bevel=0.03))
    P.append(torus(1.72, 0.03, (0, 0, 6.45), "Emit_Red", major=64))
    for i in range(6):
        a = 2 * math.pi * i / 6
        P.append(cyl(0.12, 1.4, (math.cos(a) * 0.6, math.sin(a) * 0.6, 8.4), "Metal_Mid", verts=12))
    for i in range(5):
        a = 2 * math.pi * i / 5 + 0.3
        c, s = math.cos(a), math.sin(a)
        end = 2.0 + random.random() * 2.0
        P.append(tube([(c * 1.0, s * 1.0, 6.4), (c * 1.25, s * 1.25, 5.2), (c * 0.9 + 0.3, s * 0.9, end)], 0.045, "Rubber"))
    for i in range(26):
        a = 2 * math.pi * i / 26 + random.uniform(-0.05, 0.05)
        h = 0.3 + random.random() ** 2 * 3.0
        P.append(box((0.5, 0.025, h), (math.cos(a) * 2.0, math.sin(a) * 2.0, 0.83 + h / 2), "Glass",
                     rot=(random.uniform(-6, 6), random.uniform(-10, 10), math.degrees(a) + 90)))
    for i in range(18):
        a = random.uniform(0, 2 * math.pi); d = random.uniform(2.4, 4.6)
        P.append(box((random.uniform(0.15, 0.5), random.uniform(0.1, 0.35), 0.015),
                     (math.cos(a) * d, math.sin(a) * d, 0.39 if d < 3.25 else (0.19 if d < 3.7 else 0.01)), "Glass",
                     rot=(random.uniform(-5, 5), 0, random.uniform(0, 180))))
    P.append(box((1.2, 0.03, 2.2), (3.0, -1.6, 0.42), "Glass", rot=(88, 0, 35)))
    P.append(box((0.5, 0.35, 1.1), (0, -3.0, 0.92), "Metal_Dark", bevel=0.02))
    P.append(box((0.45, 0.02, 0.3), (0, -3.18, 1.35), "Emit_Red", rot=(-25, 0, 0)))
    return join(P, "Lab_Containment")


def lab_bench():
    """3 m white lab bench against a wall (back at +y), drawers, a shelf with glassware above."""
    random.seed(5)
    P = [box((3.0, 0.8, 0.86), (1.5, 0.4, 0.43), "Paint_White", bevel=0.015),
         box((3.05, 0.85, 0.05), (1.5, 0.4, 0.885), "Metal_Dark", bevel=0.01)]
    for k in range(4):
        P.append(box((0.68, 0.02, 0.3), (0.4 + k * 0.73, -0.005, 0.62), "Metal_Mid", bevel=0.005))
        P.append(box((0.2, 0.03, 0.025), (0.4 + k * 0.73, -0.03, 0.66), "Metal_Dark"))
    P.append(box((3.0, 0.32, 0.03), (1.5, 0.68, 1.75), "Metal_Mid"))
    for x in (0.05, 2.95):
        P.append(box((0.04, 0.32, 0.95), (x, 0.68, 1.36), "Metal_Dark"))
    for k in range(12):
        x = 0.2 + k * 0.23
        h = random.uniform(0.12, 0.3)
        P.append(cyl(random.uniform(0.04, 0.07), h, (x, 0.66, 1.765 + h / 2), "Glass" if k % 3 else "Liquid", verts=12))
    for k in range(6):
        x = 0.3 + k * 0.15
        P.append(cyl(0.025, 0.14, (x, 0.2, 0.98), "Liquid" if k % 2 else "Glass", verts=10))
    P.append(box((0.6, 0.12, 0.03), (0.6, 0.2, 0.92), "Metal_Dark"))
    P.append(box((0.45, 0.4, 0.4), (2.4, 0.35, 1.11), "Paint_White", bevel=0.02))
    P.append(box((0.3, 0.01, 0.2), (2.4, 0.145, 1.13), "Screen"))
    P.append(box((0.05, 0.01, 0.03), (2.58, 0.14, 1.0), "Emit_Cyan"))
    return join(P, "Lab_Bench")


def switchgear():
    """3 m row of electrical cabinets (front at -y), dials and lamps, conduit to the top."""
    random.seed(9)
    P = []
    for k in range(3):
        x = 0.5 + k * 1.0
        P.append(box((0.96, 0.7, 2.3), (x, 0.35, 1.15), "Metal_Mid", bevel=0.015))
        P.append(box((0.8, 0.02, 1.9), (x, -0.005, 1.1), "Metal_Dark", bevel=0.005))
        P.append(box((0.04, 0.05, 0.25), (x + 0.32, -0.04, 1.1), "Metal_Mid"))
        for j in range(3):
            P.append(cyl(0.06, 0.03, (x - 0.22 + j * 0.2, -0.03, 1.75), "Paint_White", verts=16, rot=(90, 0, 0)))
            P.append(box((0.03, 0.01, 0.03), (x - 0.22 + j * 0.2, -0.03, 1.55), "Emit_Red" if random.random() < 0.4 else "Emit_Cyan"))
        P.append(box((0.5, 0.01, 0.12), (x, -0.02, 0.4), "Paint_Yellow"))
        P.append(cyl(0.05, 1.5, (x, 0.45, 3.05), "Metal_Dark", verts=10))
    P.append(box((3.0, 0.5, 0.12), (1.5, 0.35, 2.36), "Metal_Dark"))
    return join(P, "Elec_Switchgear")


def robot_arm():
    P = [cyl(0.55, 0.35, (0, 0, 0.175), "Metal_Dark", verts=24, bevel=0.02),
         cyl(0.42, 0.45, (0, 0, 0.55), "Paint_Yellow", verts=24, bevel=0.02),
         cyl(0.3, 0.7, (0, 0, 0.95), "Paint_Yellow", verts=20, rot=(0, 90, 0))]
    P.append(box((0.32, 0.32, 1.7), (0.0, 0.45, 1.65), "Paint_Yellow", bevel=0.05, rot=(-25, 0, 0)))
    P.append(cyl(0.24, 0.55, (0, 0.82, 2.4), "Metal_Dark", verts=20, rot=(0, 90, 0)))
    P.append(box((0.26, 0.26, 1.4), (0, 1.35, 2.15), "Paint_Yellow", bevel=0.04, rot=(-115, 0, 0)))
    P.append(cyl(0.14, 0.3, (0, 1.95, 1.9), "Metal_Dark", verts=16, rot=(-25, 0, 0)))
    for dx in (-0.08, 0.08):
        P.append(box((0.04, 0.06, 0.28), (dx, 2.05, 1.72), "Metal_Mid", rot=(-25, 0, 0)))
    P.append(tube([(0.2, 0.0, 0.8), (0.3, 0.6, 2.0), (0.2, 1.2, 2.5), (0.15, 1.8, 2.0)], 0.03, "Rubber"))
    return join(P, "Factory_RobotArm")


def conveyor():
    """6 m conveyor segment along x, belt top at 0.95, legs every 2 m."""
    P = [box((6.0, 1.0, 0.06), (3, 0, 0.92), "Rubber"),
         box((6.0, 1.15, 0.25), (3, 0, 0.75), "Metal_Dark", bevel=0.01)]
    for y in (-0.6, 0.6):
        P.append(box((6.0, 0.08, 0.2), (3, y, 0.98), "Paint_Yellow", bevel=0.01))
    for k in range(13):
        P.append(cyl(0.05, 1.0, (0.23 + k * 0.46, 0, 0.86), "Metal_Mid", verts=10, rot=(90, 0, 0)))
    for x in (0.3, 2.3, 4.3):
        for y in (-0.5, 0.5):
            P.append(box((0.1, 0.1, 0.65), (x, y, 0.32), "Metal_Dark"))
        P.append(box((0.08, 1.0, 0.08), (x, 0, 0.2), "Metal_Dark"))
    return join(P, "Factory_Conveyor")


def pipe_riser():
    """9 m vertical pipe bundle with brackets and a valve wheel (against a wall at +y)."""
    P = []
    for i, (dx, r) in enumerate(((-0.25, 0.12), (0.05, 0.09), (0.3, 0.07))):
        P.append(cyl(r, 9.0, (dx, 0.25, 4.5), "Metal_Mid" if i != 1 else "Paint_Yellow", verts=16))
        for z in (1.0, 4.0, 7.0):
            P.append(cyl(r + 0.03, 0.12, (dx, 0.25, z), "Metal_Dark", verts=16, smooth=False))
    for z in (1.6, 4.6, 7.6):
        P.append(box((0.8, 0.3, 0.08), (0.02, 0.4, z), "Metal_Dark"))
    P.append(torus(0.2, 0.025, (0.05, 0.1, 1.4), "Paint_Red" if False else "Emit_Red", rot=(90, 0, 0)))
    return join(P, "Hall_PipeRiser")


def build_all():
    made = []
    for fn in (lambda: capsule(False), lambda: capsule(True, 7), lambda: wall_panel("Lab_WallPanel", "Paint_White", "Emit_Cyan", "Emit_Cyan"),
               lambda: wall_panel("Hall_WallPanel", "Metal_Mid", "Paint_Yellow", "Emit_Cyan"), console, server_rack, truss, trench,
               containment, lab_bench, switchgear, robot_arm, conveyor, pipe_riser):
        clear()
        o = fn()
        export([o], o.name)
        made.append((o.name, len(o.data.polygons)))
    return made


def transformer():
    """4 x 3 m transformer: tank with cooling fins, bushings with insulator stacks, cable boxes."""
    P = [box((4.0, 2.6, 2.8), (0, 0, 1.6), "Metal_Mid", bevel=0.04), box((4.3, 2.9, 0.2), (0, 0, 0.1), "Metal_Dark", bevel=0.02),
         box((4.1, 2.7, 0.15), (0, 0, 3.07), "Metal_Dark", bevel=0.02)]
    for side in (-1, 1):
        for k in range(14):
            P.append(box((0.06, 0.45, 2.3), (-1.75 + k * 0.27, side * 1.52, 1.55), "Metal_Dark"))
    for k in range(3):
        x = -1.2 + k * 1.2
        P.append(cyl(0.18, 0.3, (x, 0, 3.3), "Metal_Dark", verts=16))
        for j in range(6):
            P.append(cyl(0.2 - (j % 2) * 0.05, 0.12, (x, 0, 3.5 + j * 0.12), "Paint_White", verts=16))
        P.append(cyl(0.06, 0.25, (x, 0, 4.3), "Metal_Mid", verts=10))
    P.append(box((0.9, 0.4, 0.9), (1.4, -1.5, 2.2), "Metal_Dark", bevel=0.02))
    P.append(box((0.7, 0.02, 0.4), (-1.0, -1.31, 2.2), "Paint_Yellow"))
    P.append(box((0.12, 0.02, 0.12), (-1.5, -1.31, 2.2), "Emit_Red"))
    P.append(tube([(1.4, -1.7, 2.0), (1.4, -2.2, 1.2), (1.6, -2.4, 0.05)], 0.07, "Rubber"))
    return join(P, "Elec_Transformer")


def ship_hull():
    """7 m fuselage section along x on a cradle: ribs, stringers, half the skin panels missing."""
    random.seed(21)
    P = []
    for x in (-2.6, 2.6):
        P.append(box((0.5, 3.6, 0.9), (x, 0, 0.45), "Paint_Yellow", bevel=0.03))
        P.append(box((0.3, 2.6, 0.5), (x, 0, 1.1), "Metal_Dark", rot=(0, 0, 0)))
    R = 1.7
    for k in range(8):
        x = -3.3 + k * 0.94
        P.append(torus(R, 0.07, (x, 0, 2.9), "Metal_Dark", rot=(0, 90, 0), major=40, minor=6))
    for j in range(12):
        a = 2 * math.pi * j / 12
        P.append(cyl(0.05, 6.8, (0, math.cos(a) * (R - 0.02), 2.9 + math.sin(a) * (R - 0.02)), "Metal_Mid", verts=6, rot=(0, 90, 0)))
    for k in range(7):
        for j in range(12):
            if random.random() < 0.45:
                continue
            a = 2 * math.pi * (j + 0.5) / 12
            x = -2.83 + k * 0.94
            P.append(box((0.9, 0.86, 0.04), (x, math.cos(a) * R, 2.9 + math.sin(a) * R), "Metal_Mid",
                         rot=(math.degrees(a) + 90, 0, 0)))
    P.append(box((1.2, 0.6, 0.5), (0.5, 0, 1.45), "Metal_Dark"))
    P.append(tube([(-3.4, 0.4, 1.6), (-1.0, 0.8, 1.3), (1.2, -0.6, 1.5), (3.4, -0.2, 2.0)], 0.04, "Rubber"))
    return join(P, "Factory_ShipHull")


def engine():
    """Thruster on a dolly: casing, bell nozzle, intake with blades, plumbing."""
    P = [box((4.6, 2.4, 0.7), (0, 0, 0.35), "Paint_Yellow", bevel=0.04)]
    for x in (-1.2, 1.2):
        P.append(box((0.4, 2.0, 0.9), (x, 0, 1.0), "Metal_Dark", bevel=0.03))
    P.append(cyl(1.2, 2.8, (-0.4, 0, 2.3), "Metal_Mid", verts=40, rot=(0, 90, 0), bevel=0.03))
    for k in range(4):
        P.append(torus(1.22, 0.05, (-1.5 + k * 0.75, 0, 2.3), "Metal_Dark", rot=(0, 90, 0), major=40, minor=6))
    bpy.ops.mesh.primitive_cone_add(vertices=40, radius1=1.35, radius2=0.85, depth=1.6, location=(1.8, 0, 2.3), rotation=(0, math.radians(90), 0))
    cone = bpy.context.active_object
    _finish(cone, "Metal_Dark")
    mod = cone.modifiers.new("solid", "SOLIDIFY"); mod.thickness = 0.06
    bpy.ops.object.shade_smooth()
    P.append(cone)
    P.append(cyl(1.0, 0.08, (-1.85, 0, 2.3), "Metal_Dark", verts=40, rot=(0, 90, 0)))
    P.append(cyl(0.25, 0.3, (-1.95, 0, 2.3), "Metal_Mid", verts=16, rot=(0, 90, 0)))
    for k in range(10):
        a = 360 * k / 10
        P.append(box((0.03, 0.8, 0.16), (-1.9, 0, 2.3), "Metal_Mid", rot=(a, 0, 25)))
    for j, (y, z) in enumerate(((0.9, 3.2), (-0.9, 3.1), (0.0, 3.5))):
        P.append(tube([(-1.2, y, z), (0.2, y * 1.2, z + 0.25), (1.0, y * 0.7, z - 0.2)], 0.06, "Rubber" if j else "Metal_Mid"))
    return join(P, "Factory_Engine")


def jig():
    """Assembly gantry: two A-frame towers and a beam with a hanging hoist (8 m span along x)."""
    P = []
    for x in (-4.0, 4.0):
        for y in (-1.2, 1.2):
            P.append(box((0.3, 0.3, 6.2), (x, y, 3.1), "Paint_Yellow", bevel=0.03, rot=(0, 0, 0)))
        P.append(box((0.25, 2.7, 0.25), (x, 0, 6.1), "Paint_Yellow"))
        P.append(box((0.2, 2.4, 0.2), (x, 0, 2.0), "Metal_Dark"))
        P.append(box((0.9, 3.0, 0.15), (x, 0, 0.075), "Metal_Dark", bevel=0.02))
        for y in (-1.2, 1.2):
            P.append(box((0.12, 0.12, 3.4), (x, y * 0.5, 3.8), "Metal_Dark", rot=(0, 0, 0)))
    P.append(box((8.5, 0.5, 0.6), (0, 0, 6.5), "Paint_Yellow", bevel=0.04))
    P.append(box((0.8, 0.9, 0.6), (1.0, 0, 5.9), "Metal_Dark", bevel=0.03))
    P.append(cyl(0.03, 1.8, (1.0, 0, 4.7), "Metal_Mid", verts=6))
    P.append(box((0.3, 0.3, 0.35), (1.0, 0, 3.7), "Metal_Dark"))
    P.append(box((0.6, 0.02, 0.15), (-4.0, -1.37, 2.0), "Emit_Red"))
    return join(P, "Factory_Jig")


def build_more():
    made = []
    for fn in (transformer, ship_hull, engine, jig):
        clear()
        o = fn()
        export([o], o.name)
        made.append((o.name, len(o.data.polygons)))
    return made


# ------------------------------------------------------------------ storage / electrical (pass 3)

def _pallet(x, y, z, P):
    """Euro pallet, 1.2 deep (x) by 1.0 wide (y), top at z + 0.14."""
    P.append(box((1.2, 1.0, 0.035), (x, y, z + 0.122), "Wood"))
    for dy in (-0.44, 0.0, 0.44):
        P.append(box((1.2, 0.12, 0.1), (x, y + dy, z + 0.05), "Wood"))
    return z + 0.14


def _load(x, y, z, room, P):
    """One random rack load on a pallet: boxes, a wrapped block, drums or a crate."""
    kind = random.random()
    if kind < 0.12:
        return
    top = _pallet(x, y, z, P)
    h = min(room, random.uniform(0.9, 1.5))
    if kind < 0.5:
        layers = max(1, int(h / 0.42))
        for l in range(layers):
            for i in (-1, 1):
                for j in (-1, 1):
                    if l == layers - 1 and random.random() < 0.3:
                        continue
                    w, d = random.uniform(0.5, 0.56), random.uniform(0.42, 0.48)
                    P.append(box((w, d, 0.4), (x + i * 0.29 + random.uniform(-0.02, 0.02),
                                               y + j * 0.24 + random.uniform(-0.02, 0.02), top + l * 0.41 + 0.2),
                                 "Cardboard", rot=(0, 0, random.uniform(-4, 4))))
    elif kind < 0.72:
        P.append(box((1.16, 0.96, h), (x, y, top + h / 2), "Wrap", bevel=0.05))
        P.append(box((1.18, 0.06, 0.08), (x, y, top + h * 0.35), "Paint_Yellow"))
    elif kind < 0.86:
        col = random.choice(("Paint_Blue", "Paint_Red", "Metal_Mid", "Paint_Yellow"))
        for i in (-1, 1):
            for j in (-1, 1):
                P.append(cyl(0.25, 0.88, (x + i * 0.29, y + j * 0.24, top + 0.44), col, verts=16))
    else:
        P.append(box((1.15, 0.95, h), (x, y, top + h / 2), "Wood", bevel=0.02))
        for zz in (0.12, h - 0.12):
            P.append(box((1.18, 0.98, 0.08), (x, y, top + zz), "Metal_Dark"))


def high_rack(name, seed):
    """6 m long, 7.2 m tall pallet rack (length along -y, depth x +-0.55), three beam levels plus the floor."""
    random.seed(seed)
    P = []
    H = 7.2
    frames = (0.06, 3.0, 5.94)
    for t in frames:
        for x in (-0.5, 0.5):
            P.append(box((0.09, 0.08, H), (x, -t, H / 2), "Paint_Blue"))
            P.append(box((0.18, 0.16, 0.02), (x, -t, 0.01), "Metal_Dark"))
        for k in range(7):
            z0 = 0.35 + k * 0.95
            P.append(box((1.0, 0.03, 0.04), (0, -t, z0), "Metal_Mid"))
            P.append(box((1.34, 0.03, 0.04), (0, -t, z0 + 0.475), "Metal_Mid", rot=(0, 45 if k % 2 else -45, 0)))
    levels = (0.0, 1.9, 3.75, 5.6)
    for b in range(2):
        yc = -(frames[b] + frames[b + 1]) / 2
        for li, z in enumerate(levels):
            if z > 0:
                for x in (-0.53, 0.53):
                    P.append(box((0.06, 2.88, 0.13), (x, yc, z - 0.065), "Paint_Yellow"))
                for dy in (-1.0, 0.0, 1.0):
                    P.append(box((1.0, 0.05, 0.04), (0, yc + dy, z - 0.03), "Metal_Mid"))
                for x in (-0.565, 0.565):
                    P.append(box((0.01, 0.16, 0.07), (x, yc + 0.9, z - 0.065), "Paint_White"))
            room = (levels[li + 1] - z - 0.35) if li + 1 < len(levels) else 1.45
            for dy in (-0.72, 0.72):
                _load(0.0, yc + dy, z, room, P)
    return join(P, name)


def forklift():
    """Counterbalance forklift, forks towards -y raised 0.6 m with a pallet of boxes."""
    random.seed(4)
    P = [box((1.1, 1.75, 0.5), (0, 0.95, 0.45), "Paint_Yellow", bevel=0.05),
         box((1.12, 0.5, 0.8), (0, 1.75, 0.75), "Metal_Dark", bevel=0.08),
         box((0.9, 0.75, 0.22), (0, 1.25, 0.81), "Paint_Yellow", bevel=0.04),
         box((0.95, 0.32, 0.45), (0, 0.35, 0.9), "Paint_Yellow", bevel=0.04),
         box((0.46, 0.46, 0.1), (0, 1.2, 1.0), "Rubber", bevel=0.03),
         box((0.46, 0.09, 0.48), (0, 1.47, 1.26), "Rubber", bevel=0.03),
         cyl(0.03, 0.5, (0, 0.5, 1.2), "Metal_Dark", verts=8, rot=(-30, 0, 0)),
         torus(0.16, 0.02, (0, 0.42, 1.42), "Rubber", rot=(-30, 0, 0), major=24, minor=6)]
    for x in (-0.48, 0.48):
        P.append(box((0.06, 0.06, 1.65), (x, 0.22, 1.32), "Metal_Dark"))
        P.append(box((0.06, 0.06, 1.2), (x, 1.62, 1.55), "Metal_Dark"))
    P.append(box((1.02, 1.5, 0.05), (0, 0.92, 2.15), "Metal_Dark"))
    for k in range(5):
        P.append(box((0.04, 1.4, 0.06), (-0.4 + k * 0.2, 0.92, 2.12), "Metal_Mid"))
    for x in (-0.5, 0.5):
        P.append(cyl(0.3, 0.24, (x, 0.22, 0.3), "Rubber", verts=20, rot=(0, 90, 0)))
        P.append(cyl(0.17, 0.25, (x, 0.22, 0.3), "Metal_Mid", verts=12, rot=(0, 90, 0)))
        P.append(cyl(0.23, 0.2, (x, 1.6, 0.23), "Rubber", verts=20, rot=(0, 90, 0)))
    for x in (-0.36, 0.36):
        P.append(box((0.1, 0.1, 2.4), (x, -0.1, 1.2), "Metal_Dark"))
    for z in (0.3, 1.3, 2.35):
        P.append(box((0.82, 0.08, 0.08), (0, -0.1, z), "Metal_Dark"))
    P.append(cyl(0.05, 2.0, (0, -0.02, 1.1), "Metal_Mid", verts=10))
    zf = 0.6
    P.append(box((0.9, 0.06, 0.5), (0, -0.2, zf + 0.25), "Metal_Mid"))
    for x in (-0.25, 0.25):
        P.append(box((0.1, 0.05, 0.5), (x, -0.24, zf + 0.25), "Metal_Dark"))
        P.append(box((0.1, 1.1, 0.045), (x, -0.8, zf), "Metal_Dark"))
    top = _pallet(0, -0.78, zf + 0.02, P)
    for l in range(2):
        for i in (-1, 1):
            for j in (-1, 1):
                P.append(box((0.52, 0.45, 0.4), (i * 0.29, -0.78 + j * 0.24, top + l * 0.41 + 0.2), "Cardboard",
                             rot=(0, 0, random.uniform(-4, 4))))
    for x in (-0.3, 0.3):
        P.append(box((0.02, 0.04, 0.12), (x, 1.98, 1.0), "Emit_Red"))
    return join(P, "Storage_Forklift")


def generator():
    """6 m diesel genset on a skid along x: radiator (-x), engine, alternator, control box (+x), exhaust."""
    P = [box((6.2, 2.2, 0.3), (0, 0, 0.15), "Metal_Dark", bevel=0.03),
         box((5.0, 0.28, 0.35), (0, 0.95, 0.47), "Metal_Mid", bevel=0.02),
         box((2.6, 1.3, 1.4), (-0.6, 0, 1.0), "Paint_Yellow", bevel=0.06)]
    for k in range(6):
        P.append(box((0.3, 0.95, 0.22), (-1.6 + k * 0.4, 0, 1.8), "Metal_Mid", bevel=0.02))
    P.append(cyl(0.75, 1.7, (1.55, 0, 1.1), "Metal_Mid", verts=32, rot=(0, 90, 0), bevel=0.02))
    for k in range(5):
        P.append(torus(0.77, 0.03, (0.85 + k * 0.35, 0, 1.1), "Metal_Dark", rot=(0, 90, 0), major=32, minor=6))
    P.append(box((0.5, 2.0, 2.3), (-2.65, 0, 1.45), "Metal_Dark", bevel=0.03))
    P.append(cyl(0.85, 0.05, (-2.92, 0, 1.45), "Grate", verts=32, rot=(0, 90, 0)))
    for k in range(10):
        P.append(box((0.03, 1.9, 0.05), (-2.95, 0, 0.5 + k * 0.2), "Metal_Mid"))
    P.append(box((0.8, 0.3, 0.3), (-1.9, 0, 1.35), "Metal_Mid"))
    P.append(cyl(0.28, 1.5, (0.1, 0.55, 2.5), "Metal_Dark", verts=20, rot=(0, 90, 0), bevel=0.02))
    P.append(tube([(-1.0, 0.35, 1.75), (-0.9, 0.55, 2.2), (-0.6, 0.55, 2.5)], 0.09, "Metal_Mid"))
    P.append(tube([(0.8, 0.55, 2.5), (1.2, 0.55, 2.6), (1.3, 0.55, 3.2), (1.3, 0.55, 4.6)], 0.11, "Metal_Mid"))
    P.append(cyl(0.16, 0.06, (1.3, 0.55, 4.62), "Metal_Dark", verts=16, rot=(0, 20, 0)))
    for x in (-0.6, 0.8):
        P.append(box((0.08, 0.2, 0.9), (x, 0.55, 2.05), "Metal_Dark"))
    P.append(box((0.6, 0.6, 1.7), (2.8, -0.55, 1.15), "Paint_Yellow", bevel=0.03))
    P.append(box((0.02, 0.4, 0.3), (3.11, -0.55, 1.55), "Screen"))
    P.append(box((0.02, 0.06, 0.06), (3.11, -0.7, 1.25), "Emit_Red"))
    for j in range(3):
        P.append(cyl(0.04, 0.03, (3.11, -0.4 - j * 0.12, 1.25), "Paint_White", verts=10, rot=(0, 90, 0)))
    P.append(box((0.02, 0.45, 0.12), (3.11, -0.55, 0.55), "Paint_Yellow"))
    for k in range(3):
        P.append(tube([(2.3, 0.4 + k * 0.12, 0.9), (2.9, 0.5 + k * 0.12, 0.5), (3.4, 0.5 + k * 0.12, 0.05)], 0.045, "Rubber"))
    for x in (-3.0, 3.0):
        for y in (-1.0, 1.0):
            P.append(box((0.16, 0.16, 0.04), (x, y, 0.32), "Paint_Yellow"))
    return join(P, "Elec_Generator")


def cable_drum():
    """1.6 m cable reel on its rim (axle along x), loose end trailing to the floor."""
    P = []
    for x in (-0.5, 0.5):
        P.append(cyl(0.8, 0.07, (x, 0, 0.8), "Wood", verts=32, rot=(0, 90, 0)))
        P.append(cyl(0.22, 0.09, (x * 1.04, 0, 0.8), "Metal_Dark", verts=16, rot=(0, 90, 0)))
        for k in range(6):
            a = math.pi * k / 6
            P.append(box((0.02, 1.5, 0.06), (x * 1.08, 0, 0.8), "Metal_Dark", rot=(math.degrees(a), 0, 0)))
    P.append(cyl(0.62, 0.92, (0, 0, 0.8), "Rubber", verts=32, rot=(0, 90, 0)))
    for k in range(9):
        P.append(torus(0.62, 0.035, (-0.42 + k * 0.105, 0, 0.8), "Rubber", rot=(0, 90, 0), major=32, minor=6))
    P.append(tube([(0.3, -0.3, 1.38), (0.35, -0.85, 1.0), (0.4, -1.1, 0.3), (0.5, -1.6, 0.04), (0.9, -2.3, 0.04)], 0.035, "Rubber"))
    return join(P, "Elec_CableDrum")


def battery_rack():
    """3 m long double-sided UPS battery rack (length along -y, depth x +-0.4), four shelves."""
    P = []
    for t in (0.03, 1.5, 2.97):
        for x in (-0.38, 0.38):
            P.append(box((0.05, 0.05, 2.1), (x, -t, 1.05), "Metal_Dark"))
    for z in (0.12, 0.62, 1.12, 1.62):
        P.append(box((0.8, 3.0, 0.04), (0, -1.5, z), "Metal_Mid"))
        for k in range(8):
            y = -0.2 - k * 0.37
            for x in (-0.18, 0.18):
                P.append(box((0.32, 0.33, 0.36), (x, y, z + 0.2), "Rubber", bevel=0.01))
                P.append(cyl(0.022, 0.04, (x * 1.9, y - 0.08, z + 0.4), "Paint_Red", verts=8))
                P.append(cyl(0.022, 0.04, (x * 1.9, y + 0.08, z + 0.4), "Metal_Mid", verts=8))
        P.append(tube([(-0.37, -0.1, z + 0.42), (-0.39, -1.5, z + 0.46), (-0.37, -2.9, z + 0.42)], 0.018, "Paint_Red"))
        P.append(tube([(0.37, -0.1, z + 0.42), (0.39, -1.5, z + 0.46), (0.37, -2.9, z + 0.42)], 0.018, "Rubber"))
    P.append(box((0.8, 3.0, 0.05), (0, -1.5, 2.12), "Metal_Dark"))
    P.append(box((0.3, 3.0, 0.08), (0, -1.5, 2.2), "Grate"))
    for k in range(3):
        P.append(cyl(0.03, 3.0, (-0.08 + k * 0.08, -1.5, 2.28), "Rubber", verts=8, rot=(90, 0, 0)))
    for x in (-0.41, 0.41):
        P.append(box((0.02, 0.5, 0.3), (x, -0.4, 1.95), "Screen"))
        P.append(box((0.02, 0.05, 0.05), (x, -0.1, 1.95), "Emit_Red"))
        P.append(box((0.01, 0.6, 0.1), (x, -2.4, 1.95), "Paint_Yellow"))
    return join(P, "Elec_BatteryRack")


def cable_bundle():
    """9 m of heavy cables lying on the floor along -x (Unity +x), with a cable ramp halfway."""
    random.seed(17)
    P = []
    for k in range(5):
        y0 = -0.25 + k * 0.12
        pts = [(-i, y0 + math.sin(i * 0.9 + k) * 0.08 + random.uniform(-0.03, 0.03), 0.045) for i in range(10)]
        P.append(tube(pts, 0.04 if k != 2 else 0.05, "Paint_Yellow" if k == 2 else "Rubber"))
    P.append(box((0.6, 0.9, 0.1), (-4.5, 0, 0.05), "Paint_Yellow", bevel=0.03))
    P.append(box((0.62, 0.92, 0.02), (-4.5, 0, 0.1), "Metal_Dark"))
    return join(P, "Elec_CableBundle")


def build_storage():
    made = []
    for fn in (lambda: high_rack("Storage_HighRack_A", 31), lambda: high_rack("Storage_HighRack_B", 57),
               forklift, generator, cable_drum, battery_rack, cable_bundle):
        clear()
        o = fn()
        export([o], o.name)
        made.append((o.name, len(o.data.polygons)))
    return made
