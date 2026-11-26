using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using AshenWoods.World;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Title screen v2 (2026-11-19): the research station the game is set in, at night, in the burnt forest
    /// that gives the game its name. A long concrete hall with the loading bay - the way out, the last
    /// objective - half open and lit from inside, sodium floodlights over an empty yard, a chain fence with
    /// the gate left open, charred trunks all round, ash drifting down and embers still glowing in the
    /// undergrowth. The mansion set is not deleted: its parts are switched off once (marker Station_v1),
    /// so the owner can bring them back.
    /// </summary>
    public static partial class MenuSceneBuilder
    {
        private const string StationMarker = "Station_v1";
        private static readonly string[] MansionParts = { "Ground", "Mansion", "Fountain", "Garden", "Fence", "Sky" };

        private static Material Concrete => Surface("Station_Concrete", "painted_concrete", 3f, 0.12f, new Color(0.46f, 0.46f, 0.45f));
        private static Material Yard => Surface("Station_Yard", "painted_concrete", 4f, 0.2f, new Color(0.24f, 0.24f, 0.24f));
        private static Material Ashland => Surface("Station_Ashland", "gravel_floor", 2.5f, 0.05f, new Color(0.2f, 0.19f, 0.18f));
        private static Material Panel => AtmosphereAssets.Lit("Station_Panel", new Color(0.3f, 0.3f, 0.29f), 0.2f);
        private static Material Metal => AtmosphereAssets.Lit("Station_Metal", new Color(0.16f, 0.17f, 0.18f), 0.5f);
        private static Material Shutter => AtmosphereAssets.Lit("Station_Shutter", new Color(0.36f, 0.34f, 0.3f), 0.35f);
        private static Material Rust => AtmosphereAssets.Lit("Station_Rust", new Color(0.22f, 0.11f, 0.06f), 0.25f);
        private static Material Hazard => AtmosphereAssets.Lit("Station_Hazard", new Color(0.62f, 0.46f, 0.08f), 0.3f);
        private static Material StationGlass => AtmosphereAssets.Lit("Station_Glass", new Color(0.015f, 0.02f, 0.025f), 0.92f);
        private static Material Charred => AtmosphereAssets.Lit("Station_Charred", new Color(0.03f, 0.027f, 0.025f), 0.04f);
        private static Material Ashen => AtmosphereAssets.Lit("Station_AshenWood", new Color(0.13f, 0.125f, 0.12f), 0.04f);
        private static Material Fluorescent => AtmosphereAssets.Unlit("Station_Fluorescent", new Color(0.035f, 0.05f, 0.055f), null);
        private static Material Emergency => AtmosphereAssets.Unlit("Station_Emergency", new Color(0.7f, 0.05f, 0.03f), null);
        private static Material Sodium => AtmosphereAssets.Unlit("Station_Sodium", new Color(2.8f, 1.55f, 0.55f), null);
        private static Material BayGlow => AtmosphereAssets.Unlit("Station_BayGlow", new Color(1.9f, 1.05f, 0.42f), null);
        private static Material Beacon => AtmosphereAssets.Unlit("Station_Beacon", new Color(4f, 0.25f, 0.1f), null);
        private static Material ExitSign => AtmosphereAssets.Unlit("Station_Exit", new Color(0.25f, 1.9f, 0.7f), null);
        private static Material Ember => AtmosphereAssets.Unlit("Station_Ember", new Color(3.2f, 0.9f, 0.18f), null);

        /// <summary>Switches the mansion set off once and builds the station parts that are missing.</summary>
        private static int BuildStation(Transform set)
        {
            int changed = 0;
            if (set.Find(StationMarker) == null)
            {
                foreach (string name in MansionParts)
                {
                    Transform part = set.Find(name);
                    if (part != null)
                    {
                        part.gameObject.SetActive(false);
                    }
                }

                var marker = new GameObject(StationMarker);
                marker.transform.SetParent(set, false);
                marker.SetActive(false);
                changed++;
            }

            changed += Part(set, "Station_Ground", StationGround);
            changed += Part(set, "Station_Building", StationBuilding);
            changed += Part(set, "Station_Yard", StationYard);
            changed += Part(set, "Station_Forest", BurntForest);
            changed += Part(set, "Station_Sky", BuildSky);
            changed += Part(set, "Station_Ash", AshFall);
            changed += Part(set, "Station_Embers", Embers);
            changed += Part(set, "Station_Horizon", Horizon);
            changed += BuildWinter(set); // title screen v3: snow, clutter, details, VFX
            return changed;
        }

        // ------------------------------------------------------------------ parts

        private static void StationGround(Transform part)
        {
            Box(part, "Ashland", new Vector3(0f, -0.05f, 0f), new Vector3(600f, 0.1f, 600f), Ashland);
            Box(part, "Apron", new Vector3(10f, 0.02f, 4f), new Vector3(64f, 0.04f, 16f), Yard);
            Box(part, "Road", new Vector3(16f, 0.025f, 24f), new Vector3(8f, 0.05f, 24f), Yard);
        }

        private static void StationBuilding(Transform part)
        {
            const float front = -4f;

            // The hall, an office wing set back on the left, a parapet, a ventilation stack.
            Box(part, "Hall", new Vector3(10f, 5f, -14f), new Vector3(36f, 10f, 20f), Concrete);
            Box(part, "Plinth", new Vector3(10f, 0.4f, front + 0.12f), new Vector3(36.4f, 0.8f, 0.3f), Panel);
            Box(part, "Parapet", new Vector3(10f, 10.25f, -14f), new Vector3(36.6f, 0.5f, 20.6f), Panel);
            Box(part, "Wing", new Vector3(-13f, 3.5f, -19f), new Vector3(10f, 7f, 14f), Concrete);
            Box(part, "Wing_Parapet", new Vector3(-13f, 7.2f, -19f), new Vector3(10.4f, 0.4f, 14.4f), Panel);
            Box(part, "Roof_Unit_A", new Vector3(3f, 11.2f, -12f), new Vector3(5f, 1.9f, 3f), Metal);
            Box(part, "Roof_Unit_B", new Vector3(17f, 11.1f, -17f), new Vector3(3f, 1.7f, 3f), Metal);
            Cylinder(part, "Roof_Fan", new Vector3(17f, 12.1f, -17f), 1.1f, 0.3f, Metal);
            Cylinder(part, "Stack", new Vector3(-10f, 11f, -24f), 1.1f, 22f, Concrete);
            Cylinder(part, "Stack_Lip", new Vector3(-10f, 21.9f, -24f), 1.3f, 0.3f, Metal);
            GameObject bulb = Sphere(part, "Stack_Beacon", new Vector3(-10f, 22.35f, -24f), 0.3f, Beacon);
            Lamp(part, "Stack_Beacon_Light", new Vector3(-10f, 22.6f, -24f), new Color(1f, 0.12f, 0.06f), 2.2f, 14f,
                SceneryLight.Mode.Beacon, 2.6f, bulb.GetComponent<Renderer>());

            // Facade rhythm: pilasters, a pipe run, a strip of windows - mostly dark, two lit.
            for (int i = 0; i < 7; i++)
            {
                float x = -7.5f + i * 6f;
                Box(part, $"Pilaster_{i}", new Vector3(x, 5f, front + 0.15f), new Vector3(0.6f, 10f, 0.3f), Panel);
            }

            GameObject pipe = Cylinder(part, "Pipe_Run", new Vector3(10f, 8.9f, front + 0.45f), 0.16f, 35f, Rust);
            pipe.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            Cylinder(part, "Pipe_Down", new Vector3(-2.6f, 4.5f, front + 0.45f), 0.12f, 8.8f, Rust);
            for (int i = 0; i < 11; i++)
            {
                float x = -6f + i * 3f;
                if (x > 12f && x < 20f)
                {
                    continue; // above the bay
                }

                Material glass = i == 3 ? Fluorescent : i == 8 ? Emergency : StationGlass;
                Box(part, $"Window_{i}", new Vector3(x, 7.4f, front + 0.02f), new Vector3(2f, 1.1f, 0.08f), glass);
            }

            Lamp(part, "Window_3_Light", new Vector3(3f, 7.4f, front + 1.2f), new Color(0.55f, 0.75f, 0.8f), 1.2f, 6f, SceneryLight.Mode.Stutter, 1f, null);

            // The loading bay: the shutter stuck a metre up, warm light under it.
            Box(part, "Bay_Frame", new Vector3(16f, 3.1f, front + 0.05f), new Vector3(7.8f, 6.2f, 0.32f), Metal);
            Box(part, "Bay_Shutter", new Vector3(16f, 3.55f, front + 0.24f), new Vector3(7f, 5.1f, 0.1f), Shutter);
            for (int i = 0; i < 9; i++)
            {
                Box(part, $"Bay_Slat_{i}", new Vector3(16f, 1.3f + i * 0.55f, front + 0.3f), new Vector3(7f, 0.05f, 0.04f), Metal);
            }

            Box(part, "Bay_Inside", new Vector3(16f, 0.5f, front - 0.2f), new Vector3(7f, 1f, 0.1f), BayGlow);
            Lamp(part, "Bay_Light", new Vector3(16f, 0.7f, front + 1.6f), new Color(1f, 0.62f, 0.3f), 4.5f, 9f, SceneryLight.Mode.Smoulder, 1f, null);
            Box(part, "Bay_Plate", new Vector3(16f, 6.75f, front + 0.25f), new Vector3(4.2f, 0.55f, 0.06f), Hazard);
            foreach (float x in new[] { 11.6f, 20.4f })
            {
                Cylinder(part, $"Bollard_{x}", new Vector3(x, 0.6f, front + 1.4f), 0.17f, 1.2f, Hazard);
            }

            // A staff door with its lamp and exit sign, in the middle of the 4.5..10.5 bay (it stood behind the pilaster).
            Box(part, "Door_Frame", new Vector3(7.5f, 1.3f, front + 0.08f), new Vector3(1.5f, 2.6f, 0.3f), Metal);
            Box(part, "Door_Leaf", new Vector3(7.5f, 1.2f, front + 0.25f), new Vector3(1.1f, 2.3f, 0.06f), Panel);
            Box(part, "Door_Exit", new Vector3(7.5f, 2.78f, front + 0.26f), new Vector3(0.62f, 0.2f, 0.06f), ExitSign);
            GameObject doorLamp = Box(part, "Door_Lamp", new Vector3(7.5f, 3.25f, front + 0.35f), new Vector3(0.5f, 0.16f, 0.32f), Sodium);
            Lamp(part, "Door_Light", new Vector3(7.5f, 3.1f, front + 0.8f), new Color(1f, 0.64f, 0.32f), 2.4f, 7f,
                SceneryLight.Mode.Stutter, 1f, doorLamp.GetComponent<Renderer>());
        }

        private static void StationYard(Transform part)
        {
            // Floodlights: sodium cones over the yard, one of them failing.
            Floodlight(part, "Flood_West", new Vector3(-2f, 0f, 9f), new Vector3(6f, 2.5f, -4f), false);
            Floodlight(part, "Flood_East", new Vector3(28f, 0f, 7f), new Vector3(18f, 2f, -4f), true);

            // Chain fence along the front with the gate left open, wires on top.
            for (float x = -40f; x <= 54f; x += 3f)
            {
                if (x > 12f && x < 20f)
                {
                    continue;
                }

                Cylinder(part, $"Fence_Post_{x}", new Vector3(x, 1.4f, 12f), 0.05f, 2.8f, Metal);
                GameObject arm = Box(part, $"Fence_Arm_{x}", new Vector3(x, 2.9f, 11.85f), new Vector3(0.05f, 0.4f, 0.05f), Metal);
                arm.transform.rotation = Quaternion.Euler(-35f, 0f, 0f);
            }

            foreach (var (from, to) in new[] { (-40f, 12f), (20f, 54f) })
            {
                float length = to - from;
                float mid = (from + to) * 0.5f;
                foreach (float y in new[] { 0.15f, 1.4f, 2.75f, 3.0f, 3.12f })
                {
                    GameObject wire = Cylinder(part, $"Fence_Wire_{from}_{y}", new Vector3(mid, y, y > 2.8f ? 11.75f : 12f), y < 2f ? 0.03f : 0.012f, length, Metal);
                    wire.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                }
            }

            Gate(part, "Gate_Left", new Vector3(12f, 0f, 12f), 62f);
            Gate(part, "Gate_Right", new Vector3(20f, 0f, 12f), 180f - 75f);

            // A guard booth by the gate, a truck left at the bay, barrels and pallets.
            Box(part, "Booth", new Vector3(24.5f, 1.4f, 14.5f), new Vector3(2.6f, 2.8f, 2.4f), Concrete);
            Box(part, "Booth_Roof", new Vector3(24.5f, 2.9f, 14.5f), new Vector3(3.2f, 0.2f, 3f), Metal);
            Box(part, "Booth_Window", new Vector3(24.5f, 1.8f, 13.28f), new Vector3(1.8f, 0.8f, 0.05f), StationGlass);

            Box(part, "Truck_Body", new Vector3(27.5f, 2.05f, -0.5f), new Vector3(2.6f, 3.1f, 7.2f), Panel);
            Box(part, "Truck_Cab", new Vector3(27.5f, 1.35f, 4.3f), new Vector3(2.4f, 2.2f, 2.2f), Metal);
            Box(part, "Truck_Windscreen", new Vector3(27.5f, 1.9f, 5.41f), new Vector3(2.1f, 0.8f, 0.04f), StationGlass);
            foreach (float z in new[] { -2.8f, 1.6f, 4.4f })
            {
                foreach (float x in new[] { 26.25f, 28.75f })
                {
                    GameObject wheel = Cylinder(part, $"Wheel_{x}_{z}", new Vector3(x, 0.45f, z), 0.45f, 0.35f, Metal);
                    wheel.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                }
            }

            var random = new System.Random(11);
            for (int i = 0; i < 7; i++)
            {
                Vector3 at = new Vector3(8.6f + (float)random.NextDouble() * 2.2f, 0.45f, -2.4f + (float)random.NextDouble() * 1.8f);
                GameObject barrel = Cylinder(part, $"Barrel_{i}", at, 0.3f, 0.9f, i % 3 == 0 ? Hazard : Rust);
                if (i == 6)
                {
                    barrel.transform.position = new Vector3(10.4f, 0.3f, 0.2f);
                    barrel.transform.rotation = Quaternion.Euler(0f, 30f, 90f); // one knocked over
                }
            }

            Box(part, "Pallets", new Vector3(22.5f, 0.35f, -2.2f), new Vector3(1.2f, 0.7f, 1.2f), Rust);
            Box(part, "Crate", new Vector3(22.4f, 1.05f, -2.25f), new Vector3(1f, 0.7f, 1f), Panel);
        }

        private static void BurntForest(Transform part)
        {
            var random = new System.Random(2026);
            int count = 0;

            // Where trees may stand: around the clearing, thick behind the station and close to the camera on the left.
            var areas = new[]
            {
                (min: new Vector2(-48f, 14f), max: new Vector2(-10f, 46f), n: 26),  // foreground left
                (min: new Vector2(32f, 16f), max: new Vector2(70f, 46f), n: 18),    // foreground right
                (min: new Vector2(-70f, -26f), max: new Vector2(-20f, 10f), n: 22),  // left flank
                (min: new Vector2(34f, -30f), max: new Vector2(80f, 12f), n: 20),    // right flank
                (min: new Vector2(-60f, -90f), max: new Vector2(90f, -30f), n: 70),  // behind the station
            };

            foreach (var area in areas)
            {
                for (int i = 0; i < area.n; i++)
                {
                    Vector3 foot = new Vector3(
                        Mathf.Lerp(area.min.x, area.max.x, (float)random.NextDouble()), 0f,
                        Mathf.Lerp(area.min.y, area.max.y, (float)random.NextDouble()));
                    if (InClearing(foot))
                    {
                        continue;
                    }

                    double kind = random.NextDouble();
                    if (kind < 0.62)
                    {
                        CharredTrunk(part, $"Trunk_{count}", foot, random);
                    }
                    else if (kind < 0.82)
                    {
                        SnappedTrunk(part, $"Snag_{count}", foot, random);
                    }
                    else if (kind < 0.93)
                    {
                        Cylinder(part, $"Stump_{count}", foot + Vector3.up * 0.35f, 0.3f + (float)random.NextDouble() * 0.2f, 0.7f, Charred);
                    }
                    else
                    {
                        float length = 4f + (float)random.NextDouble() * 6f;
                        GameObject log = Cylinder(part, $"Log_{count}", foot + Vector3.up * 0.25f, 0.25f, length, Charred);
                        log.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 180f, 90f);
                    }

                    count++;
                }
            }
        }

        /// <summary>The station, its yard and the road stay clear; so does the view from the camera to the bay.</summary>
        private static bool InClearing(Vector3 p)
        {
            bool building = p.x > -22f && p.x < 34f && p.z > -28f && p.z < 13f;
            bool road = p.x > 10f && p.x < 22f && p.z > 10f && p.z < 40f;
            Vector3 camera = CameraPosition;
            Vector3 toTarget = CameraTarget - camera;
            toTarget.y = 0f;
            Vector3 toPoint = p - camera;
            toPoint.y = 0f;
            float along = Vector3.Dot(toPoint, toTarget.normalized);
            float side = Vector3.Cross(toTarget.normalized, toPoint).y;
            bool sightLine = along > 0f && along < 48f && Mathf.Abs(side) < 3f + along * 0.18f;
            bool atCamera = toPoint.magnitude < 4f;
            return building || road || sightLine || atCamera;
        }

        private static void CharredTrunk(Transform part, string name, Vector3 foot, System.Random random)
        {
            float height = 7f + (float)random.NextDouble() * 10f;
            float radius = 0.14f + (float)random.NextDouble() * 0.2f;
            Material material = random.NextDouble() < 0.25 ? Ashen : Charred;
            Quaternion lean = Quaternion.Euler((float)(random.NextDouble() - 0.5) * 10f, 0f, (float)(random.NextDouble() - 0.5) * 10f);
            GameObject trunk = Cylinder(part, name, foot + lean * Vector3.up * height * 0.5f, radius, height, material);
            trunk.transform.rotation = lean;

            // A few burnt stubs where branches were.
            int stubs = random.Next(0, 4);
            for (int i = 0; i < stubs; i++)
            {
                float y = height * (0.35f + 0.55f * (float)random.NextDouble());
                Quaternion turn = lean * Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f) * Quaternion.Euler(55f + (float)random.NextDouble() * 25f, 0f, 0f);
                float length = 0.6f + (float)random.NextDouble() * 1.6f;
                Vector3 start = foot + lean * Vector3.up * y;
                GameObject stub = Cylinder(part, $"{name}_Stub_{i}", start + turn * Vector3.up * length * 0.5f, radius * 0.35f, length, material);
                stub.transform.rotation = turn;
            }
        }

        private static void SnappedTrunk(Transform part, string name, Vector3 foot, System.Random random)
        {
            float height = 2f + (float)random.NextDouble() * 4f;
            float radius = 0.2f + (float)random.NextDouble() * 0.2f;
            Cylinder(part, name, foot + Vector3.up * height * 0.5f, radius, height, Charred);
            GameObject splinter = Cone(part, name + "_Splinter", foot + Vector3.up * height, radius, 0.8f + (float)random.NextDouble(), Charred);
            splinter.transform.rotation = Quaternion.Euler((float)(random.NextDouble() - 0.5) * 30f, 0f, (float)(random.NextDouble() - 0.5) * 30f);
        }

        private static void AshFall(Transform part)
        {
            var go = new GameObject("Ash");
            go.transform.SetParent(part, false);
            go.transform.position = new Vector3(-8f, 9f, 21f); // around the view, not the whole valley
            ParticleSystem system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 20f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(20f, 28f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.62f, 0.6f, 0.58f, 0.55f), new Color(0.85f, 0.83f, 0.8f, 0.9f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 3000;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 90f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(34f, 2f, 30f);
            ParticleSystem.VelocityOverLifetimeModule fall = system.velocityOverLifetime;
            fall.enabled = true;
            fall.space = ParticleSystemSimulationSpace.World;
            fall.x = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            fall.y = new ParticleSystem.MinMaxCurve(-0.75f, -0.45f);
            fall.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            ParticleSystem.NoiseModule drift = system.noise;
            drift.enabled = true;
            drift.strength = 0.35f;
            drift.frequency = 0.25f;
            drift.scrollSpeed = 0.1f;
            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AtmosphereAssets.Dust;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void Embers(Transform part)
        {
            foreach (var (name, at) in new[]
                     {
                         ("Ember_Left", new Vector3(-26f, 0.12f, 4f)),
                         ("Ember_Right", new Vector3(36f, 0.12f, 20f)),
                         ("Ember_Back", new Vector3(-4f, 0.12f, -34f)),
                     })
            {
                var spot = new GameObject(name).transform;
                spot.SetParent(part, false);
                spot.position = at;
                // Soft glow over the coals (particles, not hard spheres), the char they burn in, light and sparks.
                var glowObject = new GameObject("Coals");
                glowObject.transform.SetParent(spot, false);
                glowObject.transform.position = at + Vector3.up * 0.08f;
                ParticleSystem coals = glowObject.AddComponent<ParticleSystem>();
                coals.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule coalMain = coals.main;
                coalMain.loop = true;
                coalMain.prewarm = true;
                coalMain.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
                coalMain.startSpeed = 0f;
                coalMain.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
                coalMain.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.35f, 0.08f, 0.35f), new Color(1f, 0.55f, 0.15f, 0.6f));
                coalMain.maxParticles = 20;
                ParticleSystem.EmissionModule coalRate = coals.emission;
                coalRate.rateOverTime = 4f;
                ParticleSystem.ShapeModule coalShape = coals.shape;
                coalShape.shapeType = ParticleSystemShapeType.Circle;
                coalShape.radius = 0.6f;
                coalShape.rotation = new Vector3(-90f, 0f, 0f);
                ParticleSystem.ColorOverLifetimeModule coalFade = coals.colorOverLifetime;
                coalFade.enabled = true;
                var coalGradient = new Gradient();
                coalGradient.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.4f), new GradientAlphaKey(0f, 1f) });
                coalFade.color = coalGradient;
                ParticleSystemRenderer coalRenderer = glowObject.GetComponent<ParticleSystemRenderer>();
                coalRenderer.sharedMaterial = AtmosphereAssets.Dust;
                coalRenderer.shadowCastingMode = ShadowCastingMode.Off;

                Cylinder(spot, "Char", at + new Vector3(0.3f, 0.12f, 0.1f), 0.18f, 1.6f, Charred).transform.rotation = Quaternion.Euler(0f, 40f, 90f);
                Lamp(spot, "Glow", at + Vector3.up * 0.35f, new Color(1f, 0.42f, 0.12f), 1.4f, 4.5f, SceneryLight.Mode.Smoulder, 1f, null);

                // A few sparks lifting off.
                var sparks = new GameObject("Sparks");
                sparks.transform.SetParent(spot, false);
                sparks.transform.position = at + Vector3.up * 0.15f;
                ParticleSystem system = sparks.AddComponent<ParticleSystem>();
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = system.main;
                main.loop = true;
                main.prewarm = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
                main.startColor = new Color(1f, 0.55f, 0.18f, 1f);
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 40;
                ParticleSystem.EmissionModule emission = system.emission;
                emission.rateOverTime = 5f;
                ParticleSystem.ShapeModule shape = system.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.7f;
                shape.rotation = new Vector3(-90f, 0f, 0f);
                ParticleSystem.NoiseModule noise = system.noise;
                noise.enabled = true;
                noise.strength = 0.4f;
                ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
                fade.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(new Color(1f, 0.7f, 0.3f), 0f), new GradientColorKey(new Color(1f, 0.25f, 0.05f), 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
                fade.color = gradient;
                ParticleSystemRenderer renderer = sparks.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = AtmosphereAssets.Dust;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        /// <summary>
        /// Smoke lit by the moon along the horizon, so the burnt trunks read as silhouettes, and far off to the
        /// right a dull orange where the forest is still burning.
        /// </summary>
        private static void Horizon(Transform part)
        {
            Texture2D band = AtmosphereAssets.Texture("Tex_HorizonBand", 128, size =>
            {
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    float v = y / (size - 1f);
                    float a = Mathf.Clamp01(1f - v);
                    a = a * a * (3f - 2f * a) * Mathf.Clamp01(v * 6f);
                    for (int x = 0; x < size; x++)
                    {
                        float u = Mathf.Abs(x / (size - 1f) * 2f - 1f);
                        float side = Mathf.Clamp01((1f - u) * 3f);
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255f * a * side));
                    }
                }

                return pixels;
            });

            // Sprites/Default like the moon: fog would turn sheets this far away into the (near-black) fog colour.
            Sheet(part, "Haze", new Vector3(30f, 26f, -230f), new Vector2(760f, 110f), SpriteSheet("Station_Haze", band, new Color(0.3f, 0.34f, 0.4f, 0.32f)));
            Sheet(part, "FarFire", new Vector3(-70f, 12f, -160f), new Vector2(240f, 46f), SpriteSheet("Station_FarFire", band, new Color(0.85f, 0.3f, 0.08f, 0.22f)));
        }

        private static Material SpriteSheet(string name, Texture2D texture, Color color)
        {
            string path = $"{AtmosphereAssets.Folder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Sprites/Default")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = Shader.Find("Sprites/Default"); // an older copy may still be URP Unlit (fogged)
            material.mainTexture = texture;
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void Sheet(Transform part, string name, Vector3 at, Vector2 size, Material material)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.name = name;
            quad.transform.SetParent(part, false);
            quad.transform.position = at;
            Vector3 away = at - CameraPosition;
            away.y = 0f;
            quad.transform.rotation = Quaternion.LookRotation(away, Vector3.up);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            Renderer renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // ------------------------------------------------------------------ pieces

        private static void Floodlight(Transform part, string name, Vector3 foot, Vector3 aim, bool failing)
        {
            const float height = 9f;
            Cylinder(part, name + "_Pole", foot + Vector3.up * height * 0.5f, 0.13f, height, Metal);
            Vector3 head = foot + Vector3.up * (height + 0.15f);
            GameObject housing = Box(part, name + "_Housing", head, new Vector3(0.9f, 0.35f, 0.6f), Metal);
            housing.transform.rotation = Quaternion.LookRotation(new Vector3(aim.x - foot.x, 0f, aim.z - foot.z), Vector3.up);
            GameObject lens = Box(part, name + "_Lens", head - Vector3.up * 0.2f, new Vector3(0.75f, 0.05f, 0.45f), Sodium);
            lens.transform.rotation = housing.transform.rotation;

            var lightObject = new GameObject(name + "_Light");
            lightObject.transform.SetParent(part, false);
            lightObject.transform.position = head - Vector3.up * 0.3f;
            lightObject.transform.rotation = Quaternion.LookRotation(aim - lightObject.transform.position, Vector3.up);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(1f, 0.66f, 0.34f);
            light.intensity = 380f; // URP falls off with the square of distance: the lamp is ~14 m from the yard
            light.range = 34f;
            light.spotAngle = 78f;
            light.innerSpotAngle = 40f;
            light.shadows = failing ? LightShadows.None : LightShadows.Soft;
            if (failing)
            {
                lightObject.AddComponent<SceneryLight>().Configure(SceneryLight.Mode.Stutter, 1f, lens.GetComponent<Renderer>());
            }
        }

        private static void Gate(Transform part, string name, Vector3 hinge, float yaw)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(part, false);
            pivot.position = hinge;
            pivot.rotation = Quaternion.Euler(0f, yaw, 0f);
            LocalBox(pivot, "Top", new Vector3(2f, 2.5f, 0f), new Vector3(4f, 0.08f, 0.08f), Metal);
            LocalBox(pivot, "Bottom", new Vector3(2f, 0.2f, 0f), new Vector3(4f, 0.08f, 0.08f), Metal);
            LocalBox(pivot, "Post", new Vector3(0.04f, 1.35f, 0f), new Vector3(0.1f, 2.7f, 0.1f), Metal);
            LocalBox(pivot, "End", new Vector3(3.96f, 1.35f, 0f), new Vector3(0.08f, 2.4f, 0.08f), Metal);
            GameObject brace = LocalBox(pivot, "Brace", new Vector3(2f, 1.35f, 0f), new Vector3(4.6f, 0.06f, 0.06f), Metal);
            brace.transform.localRotation = Quaternion.Euler(0f, 0f, 31f);
        }

        private static void Lamp(Transform parent, string name, Vector3 at, Color color, float intensity, float range,
            SceneryLight.Mode mode, float period, Renderer glow)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            go.AddComponent<SceneryLight>().Configure(mode, period, glow);
        }

        /// <summary>A Poly Haven surface of its own (HouseBuilder's surfaces are shared by texture id).</summary>
        private static Material Surface(string name, string textureId, float tileMeters, float smoothness, Color tint)
        {
            string path = $"{AtmosphereAssets.Folder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            string folder = $"Assets/Art/PolyHaven/Textures/{textureId}";
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{textureId}_diff.jpg");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{textureId}_nor_gl.jpg");
            material.SetColor("_BaseColor", tint);
            material.SetTexture("_BaseMap", albedo);
            material.SetTextureScale("_BaseMap", Vector2.one / tileMeters);
            material.SetFloat("_Smoothness", smoothness);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }

            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
