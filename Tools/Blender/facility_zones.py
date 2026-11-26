# Zone layout pass (2026-11-11): a believable conveyor, its drive end, pallet racking, an operator
# control stand. Conventions as facility_assets.py: Z up, metres, front towards Blender -y (Unity +z),
# pivot = world origin, Blender x -> Unity -x. Run build_zones() through the Blender bridge.
exec(open(r"C:/Users/Admin/3D projects/My project/Tools/Blender/facility_horror.py").read())

BELT = 0.94          # belt top (Unity y); gameplay: low enough to vault, high enough to matter


def _legs(P, x, half=0.5):
    """One leg frame across the conveyor at x: two square posts, levelling feet, brace and diagonal."""
    for y in (-half, half):
        P.append(box((0.07, 0.07, 0.72), (x, y, 0.42), "Metal_Dark", bevel=0.004))
        P.append(cyl(0.012, 0.06, (x, y, 0.05), "Metal_Mid", verts=8))
        P.append(cyl(0.045, 0.015, (x, y, 0.008), "Rubber", verts=12))
    P.append(box((0.05, 2 * half, 0.05), (x, 0, 0.22), "Metal_Dark"))
    P.append(box((0.04, 2 * half * 1.12, 0.04), (x, 0, 0.5), "Metal_Dark", rot=(math.degrees(math.atan2(0.5, 2 * half)), 0, 0)))


def conveyor_segment(name="Factory_Conveyor", boxes=((1.3, 0.12, 8), (4.2, -0.15, -5))):
    """6 m belt section along Blender +x (pivot at x 0 = the Unity +x end), 1.2 m over the frame."""
    P = []
    # Side frames: C-channel with a top lip, shaft ends of the idlers showing every 0.5 m.
    for side in (-1, 1):
        y = side * 0.56
        P.append(box((6.0, 0.05, 0.24), (3, y, 0.8), "Metal_Mid", bevel=0.006))
        P.append(box((6.0, 0.09, 0.025), (3, y - side * 0.02, 0.92), "Metal_Mid"))
        P.append(box((6.0, 0.09, 0.025), (3, y - side * 0.02, 0.68), "Metal_Mid"))
        for k in range(12):
            P.append(cyl(0.022, 0.03, (0.25 + k * 0.5, y + side * 0.035, 0.84), "Metal_Dark", verts=8, rot=(90, 0, 0)))
        # Guide rail on brackets.
        P.append(box((6.0, 0.03, 0.07), (3, side * 0.53, 1.07), "Paint_Yellow", bevel=0.005))
        for x in (0.4, 2.0, 3.6, 5.2):
            P.append(box((0.04, 0.04, 0.14), (x, side * 0.55, 1.0), "Metal_Dark"))
    # Belt: carrying side with cleats, return side underneath.
    P.append(box((6.0, 1.0, 0.02), (3, 0, BELT - 0.01), "Rubber"))
    for k in range(24):
        P.append(box((0.025, 0.94, 0.012), (0.125 + k * 0.25, 0, BELT + 0.006), "Rubber"))
    P.append(box((6.0, 1.0, 0.015), (3, 0, 0.64), "Rubber"))
    for k in range(6):
        P.append(cyl(0.035, 1.04, (0.5 + k * 1.0, 0, 0.6), "Metal_Mid", verts=12, rot=(90, 0, 0)))
    # Legs, cable tray and the emergency pull cord with its switch box.
    for x in (0.35, 3.0, 5.65):
        _legs(P, x)
    P.append(box((6.0, 0.18, 0.05), (3, 0.38, 0.32), "Grate"))
    P.append(cyl(0.025, 6.0, (3, 0.38, 0.37), "Rubber", verts=8, rot=(0, 90, 0)))
    P.append(cyl(0.006, 6.0, (3, -0.66, 1.0), "Paint_Red", verts=6, rot=(0, 90, 0)))
    for x in (0.2, 3.0, 5.8):
        P.append(box((0.02, 0.1, 0.02), (x, -0.61, 1.0), "Metal_Dark"))
    P.append(box((0.16, 0.1, 0.18), (3.0, -0.68, 0.86), "Paint_Yellow", bevel=0.01))
    P.append(cyl(0.035, 0.03, (3.0, -0.745, 0.88), "Paint_Red", verts=14, rot=(90, 0, 0)))
    # Hazard plates on the frame.
    for x in (1.0, 5.0):
        P.append(box((0.4, 0.008, 0.1), (x, -0.59, 0.8), "Paint_Yellow"))
    # Goods riding on the belt.
    for x, y, a in boxes:
        P.append(box((0.42, 0.34, 0.28), (x, y, BELT + 0.15), "Cardboard", bevel=0.006, rot=(0, 0, a)))
        P.append(box((0.43, 0.06, 0.005), (x, y, BELT + 0.292), "Wrap", rot=(0, 0, a)))
    return join(P, name)


def conveyor_drive():
    """Drive end: head pulley under a guard, motor and gearbox hanging off the -y side, one leg pair.
    Pivot at the belt's end; it reaches 0.55 m further (Blender -x = Unity +x)."""
    P = [cyl(0.13, 1.06, (-0.12, 0, 0.8), "Metal_Mid", verts=24, rot=(90, 0, 0))]
    for side in (-1, 1):
        P.append(box((0.5, 0.05, 0.42), (-0.2, side * 0.56, 0.78), "Metal_Mid", bevel=0.008))
        P.append(cyl(0.06, 0.06, (-0.12, side * 0.6, 0.8), "Metal_Dark", verts=16, rot=(90, 0, 0)))
    P.append(box((0.48, 1.16, 0.04), (-0.2, 0, 1.02), "Paint_Yellow", bevel=0.01))               # top guard
    P.append(box((0.04, 1.16, 0.3), (-0.45, 0, 0.86), "Grate"))                                 # end mesh
    # Gear motor.
    P.append(box((0.28, 0.22, 0.28), (-0.15, -0.74, 0.8), "Paint_Blue", bevel=0.015))
    P.append(cyl(0.12, 0.38, (-0.15, -1.04, 0.8), "Paint_Blue", verts=20, rot=(90, 0, 0)))
    for k in range(8):
        P.append(box((0.24, 0.32, 0.01), (-0.15, -1.04, 0.69 + k * 0.03), "Metal_Dark"))
    P.append(cyl(0.125, 0.04, (-0.15, -1.25, 0.8), "Metal_Dark", verts=20, rot=(90, 0, 0)))
    P.append(box((0.12, 0.08, 0.1), (-0.15, -1.0, 0.97), "Metal_Dark"))                        # terminal box
    P.append(tube([(-0.15, -1.0, 1.02), (-0.15, -0.9, 1.1), (-0.3, -0.6, 0.3), (-0.3, -0.4, 0.05)], 0.015))
    _legs(P, -0.3)
    P.append(box((0.3, 0.008, 0.08), (-0.2, -0.59, 0.66), "Paint_Yellow"))
    return join(P, "Factory_ConveyorDrive")


def pallet_rack(name="Storage_PalletRack", seed=1):
    """Pallet racking bay 3.6 x 1.1 x 3.0, front -y: blue uprights, orange beams, wire decks,
    loads on the floor and two levels."""
    random.seed(seed)
    P = []
    for x in (-1.8, 1.8):
        for y in (-0.5, 0.5):
            P.append(box((0.08, 0.06, 3.0), (x, y, 1.5), "Paint_Blue", bevel=0.004))
            P.append(box((0.14, 0.12, 0.01), (x, y, 0.005), "Metal_Dark"))
        for z in (0.4, 1.1, 1.8, 2.5):
            P.append(box((0.03, 1.0, 0.03), (x, 0, z), "Paint_Blue"))
        for z0 in (0.4, 1.8):
            P.append(box((0.025, 1.22, 0.025), (x, 0, z0 + 0.35), "Paint_Blue", rot=(55, 0, 0)))
        P.append(box((0.1, 0.12, 0.6), (x, -0.62, 0.3), "Paint_Yellow", bevel=0.01))           # post guard
    for z in (1.3, 2.6):
        for y in (-0.53, 0.53):
            P.append(box((3.6, 0.05, 0.12), (0, y, z), "Paint_Red", bevel=0.004))
        P.append(box((3.5, 1.0, 0.015), (0, 0, z + 0.065), "Grate"))
    for level, z in enumerate((0.0, 1.38, 2.68)):
        for x in (-0.9, 0.9):
            kind = random.random()
            if level == 2 and kind < 0.35:
                continue
            P.append(box((1.2, 0.95, 0.12), (x, 0, z + 0.06), "Wood"))
            h = 0.55 if level == 2 else random.choice((0.7, 0.85, 0.95))
            if kind < 0.65:
                P.append(box((1.15, 0.9, h), (x, 0, z + 0.12 + h / 2), "Cardboard", bevel=0.01))
                P.append(box((1.16, 0.91, h * 0.5), (x, 0, z + 0.12 + h * 0.3), "Wrap"))
            else:
                for dx in (-0.3, 0.3):
                    for dy in (-0.22, 0.22):
                        P.append(cyl(0.2, min(h, 0.8), (x + dx, dy, z + 0.12 + min(h, 0.8) / 2), "Paint_Blue", verts=16))
    P.append(box((0.3, 0.01, 0.12), (0, -0.56, 1.3), "Paper"))                                  # bay label
    P.append(box((0.3, 0.01, 0.12), (0, -0.56, 2.6), "Paper"))
    return join(P, name)


def control_stand():
    """Operator pedestal: base plate, column, tilted panel with e-stop, buttons and a small screen."""
    P = [box((0.4, 0.4, 0.02), (0, 0, 0.01), "Metal_Dark"),
         box((0.1, 0.1, 1.0), (0, 0, 0.52), "Metal_Mid", bevel=0.006),
         box((0.42, 0.26, 0.3), (0, 0, 1.15), "Paint_Yellow", bevel=0.02, rot=(-20, 0, 0)),
         box((0.34, 0.01, 0.12), (0, -0.14, 1.22), "Screen", rot=(-20, 0, 0)),
         cyl(0.05, 0.04, (0.12, -0.14, 1.07), "Paint_Red", verts=16, rot=(70, 0, 0)),
         cyl(0.03, 0.03, (-0.02, -0.14, 1.07), "Emit_Cyan", verts=12, rot=(70, 0, 0)),
         cyl(0.03, 0.03, (-0.12, -0.14, 1.07), "Metal_Dark", verts=12, rot=(70, 0, 0)),
         cyl(0.02, 0.35, (0, 0.05, 1.45), "Metal_Mid", verts=8),
         cyl(0.045, 0.1, (0, 0.05, 1.66), "Emit_Red", verts=14)]
    P.append(tube([(0, 0.06, 0.95), (0, 0.15, 0.5), (0, 0.2, 0.02)], 0.018))
    return join(P, "Factory_ControlStand")


def build_zones():
    made = []
    for fn in (conveyor_segment, conveyor_drive, pallet_rack,
               lambda: pallet_rack("Storage_PalletRack_B", seed=5), control_stand):
        clear()
        o = fn()
        export([o], o.name)
        made.append((o.name, len(o.data.polygons), tuple(round(d, 2) for d in o.dimensions)))
    return made
