# Props pass 2 (2026-11-25, owner: "no plain boxes, everything in one style"): models for the pieces that were still
# built from Unity primitives - fuel can, sample vial, valve wheel, steam vent, security terminal, laptop, keypad,
# keycard, desk lamp, push pin, generator control box + starter lever, clipboard.
#
# Every model is authored in the Unity-local frame of the object it dresses (pivot = that object's origin), using the
# U* helpers below that take Unity coordinates (x right, y up, z forward) and convert to Blender (Unity x/y/z =
# Blender -x/z/-y, the export maps it back). Same materials/slots and bevel style as facility_kit.py.
# Run build_props2() through the Blender bridge; FacilityBuilder.Props2 swaps them in (marker Props2_v1).
exec(open(r"C:/Users/Admin/3D projects/My project/Tools/Blender/facility_kit.py").read())
import random


def _b(p):
    """Unity position -> Blender position."""
    return (-p[0], -p[2], p[1])


def _bs(s):
    """Unity size -> Blender size."""
    return (s[0], s[2], s[1])


def UB(size, center, m="Metal_Dark", bevel=0.0, rot=(0, 0, 0)):
    """Box in Unity coordinates. rot = Unity euler (x, y, z) in degrees."""
    o = box(_bs(size), _b(center), m, bevel)
    _urot(o, rot)
    return o


def UC(r, length, center, axis="y", m="Metal_Dark", verts=24, bevel=0.0, rot=None):
    """Cylinder in Unity coordinates along a Unity axis."""
    brot = {"y": (0, 0, 0), "x": (0, 90, 0), "z": (90, 0, 0)}[axis]
    o = cyl(r, length, _b(center), m, verts=verts, rot=brot, bevel=bevel)
    if rot:
        _urot(o, rot)
    return o


def UT(R, r, center, axis="y", m="Metal_Mid", major=40, minor=10):
    brot = {"y": (0, 0, 0), "x": (0, 90, 0), "z": (90, 0, 0)}[axis]
    return torus(R, r, _b(center), m, rot=brot, major=major, minor=minor)


def _umatrix(rx, ry, rz):
    """A Unity rotation (radians, Unity applies z, then x, then y) as a Blender matrix. The axis map
    (x, y, z) -> (-x, -z, y) is a reflection, so each turn keeps its axis line and flips its sense."""
    return (Matrix.Rotation(-ry, 4, Vector((0, 0, 1))) @ Matrix.Rotation(rx, 4, Vector((1, 0, 0)))
            @ Matrix.Rotation(rz, 4, Vector((0, 1, 0))))


def _urot(o, rot):
    """Apply a Unity euler rotation (degrees, applied z, x, y like Unity) about the object's own centre."""
    if not any(rot):
        return
    rx, ry, rz = [math.radians(a) for a in rot]
    m = _umatrix(rx, ry, rz)
    # The kit's box() applies location too (transform_apply defaults), so the object sits at the origin with its
    # geometry offset: turn about the geometry's centre. matrix_basis, as the world matrix is stale until an update.
    corners = [o.matrix_basis @ Vector(c) for c in o.bound_box]
    loc = sum(corners, Vector()) / 8.0
    o.matrix_basis = Matrix.Translation(loc) @ m @ Matrix.Translation(-loc) @ o.matrix_basis


def bolts(points, r=0.006, depth=0.006, axis="z", m="Metal_Mid"):
    return [UC(r, depth, p, axis, m, verts=8) for p in points]


# ------------------------------------------------------------------ fuel can (FuelCan_* root: pivot on the floor)

def fuel_can():
    """Steel jerrycan 0.17 x 0.44 x 0.34 (x thick, z long): pressed X ribs on both flat sides, a raised weld seam,
    three-bar carry handle, spout with a bayonet cap and a yellow hazard label."""
    P = [UB((0.17, 0.40, 0.34), (0, 0.205, 0), "Paint_Red", bevel=0.018)]
    P.append(UB((0.176, 0.012, 0.346), (0, 0.205, 0), "Paint_Red", bevel=0.004))          # weld seam round the middle
    for sx in (-1, 1):
        x = sx * 0.088
        # pressed X ribs (two diagonal bars per side)
        P.append(UB((0.008, 0.30, 0.026), (x, 0.2, 0), "Paint_Red", bevel=0.004, rot=(34, 0, 0)))
        P.append(UB((0.008, 0.30, 0.026), (x, 0.2, 0), "Paint_Red", bevel=0.004, rot=(-34, 0, 0)))
        P.append(UB((0.006, 0.33, 0.29), (sx * 0.084, 0.2, 0), "Paint_Red", bevel=0.01))  # recess rim
    # top: three handle bars on posts
    for z in (-0.11, 0.0, 0.11):
        P.append(UB((0.03, 0.05, 0.022), (0, 0.43, z - 0.03), "Paint_Red", bevel=0.006))
    P.append(UB((0.032, 0.022, 0.25), (0, 0.465, -0.03), "Paint_Red", bevel=0.008))
    P.append(UB((0.036, 0.02, 0.12), (0, 0.477, -0.03), "Rubber", bevel=0.006))           # worn grip
    # spout at the front top corner, tilted forward, with a cap and a locking lever
    P.append(UC(0.028, 0.05, (0, 0.425, 0.125), "y", "Metal_Dark", verts=20, rot=(30, 0, 0)))
    P.append(UC(0.034, 0.03, (0, 0.452, 0.14), "y", "Metal_Mid", verts=20, rot=(30, 0, 0), bevel=0.004))
    P.append(UB((0.012, 0.012, 0.06), (0, 0.47, 0.115), "Metal_Mid", bevel=0.003, rot=(30, 0, 0)))
    P.append(UB((0.18, 0.012, 0.012), (0, 0.405, -0.16), "Metal_Dark", bevel=0.003))       # air vent bar
    # hazard label on the +x side
    P.append(UB((0.004, 0.12, 0.16), (0.091, 0.25, 0), "Paint_Yellow", bevel=0.002))
    P.append(UB((0.005, 0.03, 0.12), (0.092, 0.25, 0), "Rubber"))
    return join(P, "Prop2_FuelCan")


# ------------------------------------------------------------------ sample vial (Sample_*/Visuals frame)

def sample_vial():
    """Sealed sample tube in a little steel holder: glass with rounded bottom, glowing contents, ribbed red cap,
    paper label, two-ring stand."""
    P = [UC(0.019, 0.095, (0, 0.06, 0), "y", "Glass", verts=24),
         UC(0.015, 0.07, (0, 0.05, 0), "y", "Liquid", verts=20),
         UC(0.022, 0.024, (0, 0.118, 0), "y", "Paint_Red", verts=24, bevel=0.003)]
    for y in (0.11, 0.118, 0.126):
        P.append(UT(0.0225, 0.0018, (0, y, 0), "y", "Paint_Red", major=24, minor=6))
    P.append(UC(0.0195, 0.03, (0, 0.07, 0), "y", "Paper", verts=24))
    P.append(UC(0.03, 0.006, (0, 0.003, 0), "y", "Metal_Mid", verts=24, bevel=0.002))    # stand foot
    P.append(UT(0.022, 0.003, (0, 0.03, 0), "y", "Metal_Mid", major=24, minor=6))
    for a in range(3):
        x, z = math.cos(a * 2.094) * 0.024, math.sin(a * 2.094) * 0.024
        P.append(UC(0.002, 0.03, (x, 0.018, z), "y", "Metal_Mid", verts=6))
    return join(P, "Prop2_SampleVial")


# ------------------------------------------------------------------ valve wheel (Valve_*/Lever frame, axis = Unity x)

def valve_wheel():
    """Cast hand wheel, 0.34 across, turning about Unity x: rim, five spokes, hub with nut, a knob at the top."""
    P = [UT(0.155, 0.015, (0.02, 0, 0), "x", "Paint_Red", major=48, minor=10)]
    for i in range(5):
        a = math.radians(90 + i * 72)
        y, z = math.sin(a) * 0.08, math.cos(a) * 0.08
        sp = UC(0.008, 0.15, (0.02, y, z), "y", "Paint_Red", verts=10)
        _urot(sp, (math.degrees(-a) + 90, 0, 0))
        P.append(sp)
    P.append(UC(0.032, 0.05, (0.025, 0, 0), "x", "Paint_Red", verts=20, bevel=0.004))
    P.append(UC(0.016, 0.02, (0.05, 0, 0), "x", "Metal_Mid", verts=6))                  # hex nut
    P.append(UC(0.012, 0.06, (0.05, 0.13, 0), "x", "Paint_Yellow", verts=12, bevel=0.003))  # turning knob
    return join(P, "Prop2_ValveWheel")


# ------------------------------------------------------------------ steam vent (Steam_* root frame, front = +z)

def steam_vent():
    """Steam release valve on its riser: flanged yellow body with hazard stripes, pressure gauge with needle,
    lever with a rubber grip, nozzle pipe with a flange, bolts."""
    P = [UB((0.22, 0.22, 0.1), (0, 0, 0.02), "Paint_Yellow", bevel=0.012)]
    for y in (-0.12, 0.12):
        P.append(UB((0.16, 0.025, 0.12), (0, y, 0.02), "Metal_Mid", bevel=0.006))         # flanges
    P += bolts([(x, y, 0.08) for x in (-0.06, 0.06) for y in (-0.12, 0.12)], 0.008, 0.012)
    for i, (x, length) in enumerate(((-0.05, 0.12), (0.0, 0.2), (0.05, 0.12))):          # hazard stripes, inside the face
        P.append(UB((0.03, length, 0.006), (x, 0, 0.072), "Rubber", rot=(0, 0, 45)))
    # pressure gauge on top, facing the player
    P.append(UC(0.05, 0.03, (0, 0.22, 0.04), "z", "Metal_Mid", verts=28, bevel=0.004))
    P.append(UC(0.043, 0.006, (0, 0.22, 0.056), "z", "Paint_White", verts=28))
    P.append(UB((0.004, 0.034, 0.003), (0.008, 0.228, 0.06), "Emit_Red", rot=(0, 0, -35)))
    P.append(UC(0.045, 0.004, (0, 0.22, 0.06), "z", "Glass", verts=28))
    P.append(UC(0.012, 0.06, (0, 0.17, 0.04), "y", "Metal_Mid", verts=12))               # gauge stem
    # lever arm with a rubber grip
    lv = UB((0.24, 0.032, 0.03), (0.14, 0.08, 0.09), "Metal_Dark", bevel=0.006, rot=(0, 0, 35))
    P.append(lv)
    P.append(UC(0.022, 0.07, (0.24, 0.15, 0.09), "x", "Rubber", verts=14, rot=(0, 0, 35)))
    P.append(UC(0.02, 0.035, (0.04, 0.02, 0.09), "z", "Metal_Mid", verts=14))            # pivot boss
    # nozzle: a short pipe straight down out of the body, a flange where it leaves, a flared outlet at the end
    P.append(UC(0.03, 0.02, (0, -0.118, 0.02), "y", "Metal_Mid", verts=20, bevel=0.003))
    P.append(UC(0.022, 0.09, (0, -0.17, 0.02), "y", "Metal_Mid", verts=16))
    P.append(UT(0.026, 0.005, (0, -0.205, 0.02), "y", "Metal_Dark", major=20, minor=6))
    P.append(UC(0.028, 0.012, (0, -0.218, 0.02), "y", "Metal_Dark", verts=16))
    return join(P, "Prop2_SteamVent")


# ------------------------------------------------------------------ security terminal (root frame, screen = +z)

def terminal():
    """Desk monitor: weighted foot, neck, deep back housing with vents, bezel round the 0.47 x 0.29 screen
    (the screen surface itself stays the scene's Screen object), status lights and buttons, a cable."""
    P = [UB((0.22, 0.02, 0.16), (0, 0.01, -0.01), "Metal_Dark", bevel=0.008),           # foot
         UB((0.06, 0.16, 0.05), (0, 0.1, -0.04), "Metal_Mid", bevel=0.008),             # neck
         UB((0.52, 0.34, 0.03), (0, 0.32, 0.01), "Metal_Dark", bevel=0.01),             # front shell
         UB((0.44, 0.28, 0.1), (0, 0.32, -0.05), "Metal_Dark", bevel=0.02)]             # back housing
    # bezel frame round the screen opening (screen 0.47 x 0.29 at z 0.027)
    for (sx, sy, cx, cy) in ((0.52, 0.026, 0, 0.477), (0.52, 0.026, 0, 0.163),
                             (0.026, 0.34, 0.247, 0.32), (0.026, 0.34, -0.247, 0.32)):
        P.append(UB((sx, sy, 0.012), (cx, cy, 0.03), "Metal_Mid", bevel=0.004))
    for i in range(6):                                                                   # back vents
        P.append(UB((0.3, 0.008, 0.01), (0, 0.27 + i * 0.022, -0.101), "Grate"))
    P.append(UB((0.05, 0.008, 0.004), (0.18, 0.156, 0.036), "Emit_Cyan"))                # status light
    for x in (-0.2, -0.17, -0.14):
        P.append(UC(0.006, 0.008, (x, 0.155, 0.036), "z", "Rubber", verts=10))          # buttons
    P.append(tube([_b((0, 0.26, -0.1)), _b((0, 0.12, -0.18)), _b((0.05, 0.0, -0.22))], 0.008))
    return join(P, "Prop2_Terminal")


# ------------------------------------------------------------------ laptop (Laptop root frame)

def laptop():
    """Rugged laptop: base with a key grid and a touchpad, hinge barrel, lid tilted back 12 degrees with a bezel
    round the screen (the screen surface is the scene's Screen object)."""
    P = [UB((0.38, 0.024, 0.26), (0, 0.012, 0), "Metal_Dark", bevel=0.008)]
    P.append(UB((0.34, 0.004, 0.13), (0, 0.025, 0.035), "Rubber"))                      # key well
    for row in range(5):
        for col in range(13):
            P.append(UB((0.02, 0.004, 0.019), (-0.144 + col * 0.024, 0.028, -0.012 + row * 0.022),
                        "Metal_Mid", bevel=0.002))
    P.append(UB((0.09, 0.002, 0.06), (0, 0.025, -0.085), "Metal_Mid", bevel=0.002))      # touchpad
    P.append(UC(0.009, 0.32, (0, 0.024, 0.125), "x", "Metal_Mid", verts=14))            # hinge
    # lid: built upright round the origin, then turned like the scene's Lid (x 348) and moved to (0, 0.14, 0.13)
    lid = [UB((0.38, 0.25, 0.012), (0, 0, 0.004), "Metal_Dark", bevel=0.005)]
    for (sx, sy, cx, cy) in ((0.38, 0.02, 0, 0.115), (0.38, 0.02, 0, -0.115),
                             (0.02, 0.25, 0.18, 0), (0.02, 0.25, -0.18, 0)):
        lid.append(UB((sx, sy, 0.006), (cx, cy, -0.004), "Metal_Mid", bevel=0.002))
    lid.append(UB((0.02, 0.006, 0.004), (0, 0.112, -0.008), "Rubber"))                   # webcam strip
    for o in lid:
        _urot_about(o, (348, 0, 0), (0, 0, 0))
        o.location = o.location + Vector(_b((0, 0.14, 0.13)))
    P += lid
    return join(P, "Prop2_Laptop")


def _urot_about(o, rot, pivot):
    """Rotate an object by a Unity euler about a Unity-space pivot."""
    rx, ry, rz = [math.radians(a) for a in rot]
    m = _umatrix(rx, ry, rz)
    p = Vector(_b(pivot))
    o.matrix_basis = Matrix.Translation(p) @ m @ Matrix.Translation(-p) @ o.matrix_basis


# ------------------------------------------------------------------ keypad (Keypad_Office frame without its scale; face = -x)

def keypad():
    """Wall code lock 0.05 x 0.22 x 0.15 with its face towards Unity -x: bevelled housing on a back plate, sloped
    hood over the display, a 3 x 4 grid of rubber keys in a recess, screws. The display surface and the LED lens
    stay the scene's own objects."""
    P = [UB((0.012, 0.26, 0.18), (0.019, 0, 0), "Metal_Mid", bevel=0.004),               # wall plate
         UB((0.042, 0.22, 0.15), (-0.004, 0, 0), "Metal_Dark", bevel=0.008)]             # housing
    P.append(UB((0.02, 0.012, 0.15), (-0.03, 0.105, 0), "Metal_Dark", bevel=0.003, rot=(0, 0, -25)))  # hood
    P.append(UB((0.006, 0.11, 0.12), (-0.026, -0.035, 0), "Rubber", bevel=0.003))         # key recess
    for r in range(4):
        for c in range(3):
            P.append(UB((0.01, 0.019, 0.026), (-0.031, 0.004 - r * 0.026, -0.034 + c * 0.034), "Metal_Mid",
                        bevel=0.003))
    P += bolts([(0.012, y, z) for y in (-0.118, 0.118) for z in (-0.078, 0.078)], 0.005, 0.008, "x")
    return join(P, "Prop2_Keypad")


# ------------------------------------------------------------------ keycard (Keycard root frame; card lies flat)

def keycard():
    """Plastic security card 0.09 x 0.058 with rounded corners, a chip, a magnetic stripe, a photo and a clip hole."""
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 0, 0))
    card = bpy.context.active_object
    card.scale = (0.09, 0.058, 1)
    bpy.ops.object.transform_apply(scale=True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.bevel(offset=0.006, segments=4, affect="VERTICES")
    bpy.ops.mesh.extrude_region_move(TRANSFORM_OT_translate={"value": (0, 0, 0.0016)})
    bpy.ops.object.mode_set(mode="OBJECT")
    card.location = (0, 0, 0.022)
    _finish(card, "Paint_White")
    P = [card,
         UB((0.06, 0.0004, 0.02), (0.0, 0.0238, 0.012), "Paint_Blue"),                   # header band
         UB((0.012, 0.0004, 0.01), (0.03, 0.0238, -0.006), "Paint_Yellow"),              # chip
         UB((0.024, 0.0004, 0.028), (-0.025, 0.0238, -0.008), "Cloth"),                  # photo
         UB((0.09, 0.0004, 0.012), (0, 0.0218, 0.012), "Rubber"),                        # stripe (back)
         UT(0.004, 0.0008, (0.036, 0.0238, 0.02), "y", "Metal_Mid", major=12, minor=4)]
    return join(P, "Prop2_Keycard")


# ------------------------------------------------------------------ desk lamp (DeskLamp root = the bulb)

def desk_lamp():
    """Articulated desk lamp: heavy round foot on the desk (0.42 below the bulb), two arm segments with springs and
    joints, a conical shade round the bulb opening towards the torch (+x, down, -z)."""
    foot = (0.05, -0.415, 0.05)
    elbow = (-0.05, -0.12, 0.08)
    P = [UC(0.075, 0.02, foot, "y", "Metal_Dark", verts=32, bevel=0.006),
         UC(0.02, 0.03, (foot[0], foot[1] + 0.025, foot[2]), "y", "Metal_Mid", verts=16)]
    P.append(tube([_b((foot[0], foot[1] + 0.03, foot[2])), _b(elbow)], 0.007, "Metal_Mid", res=2))
    P.append(tube([_b(elbow), _b((-0.03, 0.03, 0.03))], 0.007, "Metal_Mid", res=2))
    P.append(tube([_b((foot[0] + 0.012, foot[1] + 0.05, foot[2])), _b((elbow[0] + 0.012, elbow[1] - 0.04, elbow[2]))],
                  0.003, "Metal_Dark", res=2))                                            # spring
    P.append(UC(0.014, 0.025, elbow, "z", "Metal_Dark", verts=14))                      # joint
    # shade: an open cone pointing at the desk in front of the lamp
    bpy.ops.mesh.primitive_cone_add(vertices=28, radius1=0.07, radius2=0.03, depth=0.09, end_fill_type="NOTHING",
                                    location=_b((0.0, 0.0, 0.0)))
    shade = bpy.context.active_object
    sol = shade.modifiers.new("thick", "SOLIDIFY")
    sol.thickness = 0.003
    _finish(shade, "Paint_Yellow", smooth=True)
    # The cone is made wide end down (Blender radius1 at -z): tip it 35 degrees towards +x, where the torch lies.
    _urot(shade, (0, 0, 35))
    P.append(shade)
    P.append(UC(0.018, 0.035, (-0.03, 0.05, 0.0), "y", "Metal_Dark", verts=14, rot=(0, 0, 35)))  # socket
    return join(P, "Prop2_DeskLamp")


# ------------------------------------------------------------------ push pin (a pin's own frame; head towards +z)

def push_pin():
    P = [UC(0.012, 0.008, (0, 0, 0.012), "z", "Paint_Red", verts=20, bevel=0.002),
         UC(0.007, 0.01, (0, 0, 0.006), "z", "Paint_Red", verts=16),
         UC(0.0012, 0.012, (0, 0, -0.004), "z", "Metal_Mid", verts=6)]
    return join(P, "Prop2_PushPin")


# ------------------------------------------------------------------ generator control (GeneratorPanel root frame, front = +z)

def generator_control():
    """Starter post: square steel post on a bolted base plate, a yellow control box with a door seam, hinges,
    vents, a hazard label and the recessed dark face; fuel filler neck with a chained cap; conduit to the floor."""
    P = [UB((0.26, 0.02, 0.26), (0, 0.01, 0), "Metal_Dark", bevel=0.005)]                # base plate
    P += bolts([(x, 0.024, z) for x in (-0.1, 0.1) for z in (-0.1, 0.1)], 0.012, 0.012, "y")
    P.append(UB((0.12, 0.94, 0.12), (0, 0.49, 0), "Metal_Dark", bevel=0.008))            # post
    P.append(UB((0.46, 0.42, 0.2), (0, 1.15, 0), "Paint_Yellow", bevel=0.014))           # box
    P.append(UB((0.42, 0.38, 0.01), (0, 1.15, 0.098), "Paint_Yellow", bevel=0.006))       # door
    P.append(UB((0.4, 0.36, 0.006), (0, 1.15, 0.104), "Metal_Dark", bevel=0.003))         # recessed face
    for y in (1.04, 1.26):
        P.append(UC(0.008, 0.05, (-0.215, y, 0.1), "y", "Metal_Mid", verts=10))         # hinges
    for i in range(4):
        P.append(UB((0.006, 0.12, 0.012), (0.231, 1.1 + 0.0 * i, -0.06 + i * 0.035), "Grate"))  # side vents
    P.append(UB((0.12, 0.05, 0.004), (0.12, 1.33, 0.108), "Paint_White"))                # label
    P.append(UB((0.06, 0.06, 0.004), (-0.15, 1.31, 0.108), "Paint_Yellow"))
    P.append(UC(0.016, 0.5, (0.17, 0.7, -0.05), "y", "Metal_Mid", verts=12))            # conduit
    # fuel filler: neck, cap, chain
    P.append(UC(0.05, 0.08, (-0.32, 0.82, 0), "y", "Metal_Dark", verts=24, bevel=0.004))
    P.append(UC(0.062, 0.03, (-0.32, 0.875, 0), "y", "Paint_Red", verts=24, bevel=0.005))
    for i in range(6):
        P.append(UB((0.012, 0.03, 0.012), (-0.32 + math.cos(i) * 0.062, 0.875, math.sin(i) * 0.062), "Paint_Red", bevel=0.003))
    P.append(UB((0.12, 0.03, 0.04), (-0.2, 0.83, 0), "Metal_Dark", bevel=0.005))          # bracket to the post
    # status lamp on the roof of the box (the scene's Led light sits inside its glass)
    P.append(UC(0.028, 0.018, (-0.1, 1.369, 0.03), "y", "Metal_Dark", verts=20, bevel=0.003))
    P.append(UC(0.021, 0.03, (-0.1, 1.392, 0.03), "y", "Glass", verts=20))
    P.append(UT(0.022, 0.0025, (-0.1, 1.392, 0.03), "y", "Metal_Mid", major=20, minor=5))
    return join(P, "Prop2_GenControl")


def generator_lever():
    """Starter pull lever in the Lever frame (it tilts as a whole): flat bar with a round grip."""
    P = [UB((0.03, 0.17, 0.016), (0, 0.08, 0.02), "Paint_Red", bevel=0.006),
         UC(0.02, 0.09, (0, 0.17, 0.02), "x", "Rubber", verts=16, bevel=0.004),
         UC(0.018, 0.04, (0, 0, 0.0), "x", "Metal_Mid", verts=16)]
    return join(P, "Prop2_GenLever")


# ------------------------------------------------------------------ clipboard (Note_Janitor frame without its scale)

def clipboard():
    """Clipboard 0.22 x 0.3 lying on the desk: hardboard with rounded corners, a sprung steel clip, a few sheets."""
    P = [UB((0.22, 0.006, 0.3), (0, 0.003, 0), "Wood", bevel=0.004),
         UB((0.2, 0.002, 0.26), (0, 0.007, -0.012), "Paper"),
         UB((0.196, 0.002, 0.25), (0.004, 0.0085, -0.01), "Paper", rot=(0, 2, 0)),
         UB((0.1, 0.012, 0.035), (0, 0.012, 0.125), "Metal_Mid", bevel=0.004),
         UC(0.006, 0.1, (0, 0.019, 0.112), "x", "Metal_Mid", verts=10),
         UT(0.012, 0.002, (0, 0.016, 0.14), "z", "Metal_Mid", major=14, minor=5)]
    return join(P, "Prop2_Clipboard")


# ------------------------------------------------------------------ metal shelf v2 (replaces facility_horror.metal_shelf)

def _carton(x, y, z, w, d, h, rot):
    P = [box((w, d, h), (x, y, z + h / 2), "Cardboard", bevel=0.004, rot=(0, 0, rot)),
         box((w + 0.002, 0.05, 0.002), (x, y, z + h + 0.001), "Wrap", rot=(0, 0, rot)),                 # tape over the flaps
         box((w * 0.45, 0.002, h * 0.35), (x - w * 0.15, y - d / 2 - 0.001, z + h * 0.55), "Paper", rot=(0, 0, rot))]
    return P


def _jug(x, y, z, h, m):
    r = 0.055
    P = [cyl(r, h * 0.75, (x, y, z + h * 0.375), m, verts=20, bevel=0.01),
         cyl(r * 0.75, h * 0.12, (x, y, z + h * 0.81), m, verts=20),
         cyl(0.02, h * 0.08, (x, y, z + h * 0.91), m, verts=12),
         cyl(0.024, 0.03, (x, y, z + h + 0.01), "Rubber", verts=12, bevel=0.003),
         cyl(r + 0.0015, h * 0.3, (x, y, z + h * 0.38), "Paper", verts=20)]                                # label band
    return P


def _paint_can(x, y, z, m):
    P = [cyl(0.075, 0.12, (x, y, z + 0.06), "Tin", verts=24),
         torus(0.074, 0.004, (x, y, z + 0.12), "Tin", major=24, minor=6),
         cyl(0.0765, 0.07, (x, y, z + 0.06), m, verts=24)]
    P.append(tube([(x - 0.075, y, z + 0.1), (x, y, z + 0.16), (x + 0.075, y, z + 0.1)], 0.003, "Metal_Mid", res=6))
    return P


def _binders(x, y, z, n):
    P = []
    for i in range(n):
        m = ("Paint_Blue", "Paint_Red", "Rubber", "Paint_White")[i % 4]
        P.append(box((0.045, 0.28, 0.3), (x + i * 0.048, y, z + 0.15), m, bevel=0.004, rot=(0, (i % 3 - 1) * 2, 0)))
        P.append(box((0.03, 0.002, 0.08), (x + i * 0.048, y - 0.141, z + 0.2), "Paper"))
    return P


def _toolbox(x, y, z):
    return [box((0.42, 0.2, 0.16), (x, y, z + 0.08), "Paint_Red", bevel=0.008),
            box((0.43, 0.205, 0.006), (x, y, z + 0.12), "Metal_Dark"),
            box((0.2, 0.025, 0.025), (x, y, z + 0.185), "Rubber", bevel=0.006),
            box((0.03, 0.03, 0.03), (x - 0.1, y, z + 0.17), "Metal_Mid"), box((0.03, 0.03, 0.03), (x + 0.1, y, z + 0.17), "Metal_Mid"),
            box((0.04, 0.012, 0.02), (x - 0.15, y - 0.103, z + 0.12), "Metal_Mid"), box((0.04, 0.012, 0.02), (x + 0.15, y - 0.103, z + 0.12), "Metal_Mid")]


def metal_shelf2(name="Furn_MetalShelf", seed=2):
    """1.6 x 0.5 x 1.9 m steel shelving (same footprint and pivot as the old one): angle posts with hole rows and feet,
    lipped shelves with cross braces, and detailed supplies - taped and labelled cartons, chemical jugs with caps and
    labels, paint tins with bails, binders, a toolbox, folded cloths. The middle of the second shelf stays clear."""
    random.seed(seed)
    P = []
    for x in (-0.78, 0.78):
        for y in (-0.23, 0.23):
            P.append(box((0.04, 0.04, 1.9), (x, y, 0.95), "Metal_Dark", bevel=0.004))
            P.append(box((0.07, 0.07, 0.012), (x, y, 0.006), "Metal_Mid", bevel=0.003))                   # foot
            for k in range(12):
                P.append(box((0.012, 0.002, 0.02), (x, y - 0.021 * (1 if y > 0 else -1), 0.2 + k * 0.14), "Rubber"))
    P.append(box((1.52, 0.012, 0.012), (0, 0.235, 1.2), "Metal_Dark", rot=(0, 34, 0)))                    # back brace only
    levels = (0.12, 0.62, 1.12, 1.62)
    for li, z in enumerate(levels):
        P.append(box((1.6, 0.5, 0.025), (0, 0, z), "Metal_Mid", bevel=0.003))
        for y in (-0.25, 0.25):
            P.append(box((1.6, 0.02, 0.05), (0, y, z + 0.01), "Metal_Dark", bevel=0.003))
        x = -0.7
        top = z + 0.0125
        while x < 0.65:
            if li == 1 and -0.3 < x < 0.3:
                x += 0.1
                continue
            r = random.random()
            if r < 0.28:
                w, d, h = random.uniform(0.25, 0.36), random.uniform(0.3, 0.4), random.uniform(0.18, 0.32)
                P += _carton(x + w / 2, random.uniform(-0.03, 0.03), top, w, d, h, random.uniform(-6, 6))
                x += w + 0.04
            elif r < 0.48:
                m = random.choice(("Paint_Blue", "Paint_White", "Paint_Red", "Paint_Yellow"))
                P += _jug(x + 0.06, random.uniform(-0.1, 0.1), top, random.uniform(0.22, 0.3), m)
                x += 0.14
            elif r < 0.6:
                P += _paint_can(x + 0.08, random.uniform(-0.08, 0.08), top, random.choice(("Paint_Blue", "Paint_Red", "Paint_White")))
                x += 0.18
            elif r < 0.7 and x < 0.4:
                n = random.randint(3, 6)
                P += _binders(x + 0.03, 0.0, top, n)
                x += n * 0.048 + 0.05
            elif r < 0.78 and x < 0.3:
                P += _toolbox(x + 0.21, 0.0, top)
                x += 0.46
            elif r < 0.88:
                for k in range(random.randint(2, 3)):
                    P.append(box((0.28, 0.24, 0.03), (x + 0.14, 0, top + 0.015 + k * 0.03), "Cloth", bevel=0.008,
                                 rot=(0, 0, random.uniform(-12, 12))))
                x += 0.3
            else:
                x += 0.12
    return join(P, name)


def build_shelves2():
    out = []
    for name, seed in (("Furn_MetalShelf", 2), ("Furn_MetalShelf_B", 9)):
        clear()
        o = metal_shelf2(name, seed)
        out.append(export([o], o.name))
    return out


BUILDERS = [fuel_can, sample_vial, valve_wheel, steam_vent, terminal, laptop, keypad, keycard, desk_lamp, push_pin,
            generator_control, generator_lever, clipboard]


def build_props2(only=None):
    out = []
    for fn in BUILDERS:
        if only and fn.__name__ not in only:
            continue
        clear()
        o = fn()
        out.append(export([o], o.name))
    return out
