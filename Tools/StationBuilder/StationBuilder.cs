// Construction de la scène Assets/_MyAssets/Scenes/Station_VR.unity (station spatiale en panne).
// Menu : Tools/Station/Build Scene
// Batch : -executeMethod StationBuilder.BuildScene
// Repères : grille de 4 m, sol à Y = 0, plafond à Y = 3.
// Les modules muraux Quaternius ont leur pivot au centre de la tuile : le mur occupe x local 1,56 à 2,77
// (face décorée vers -X local, donc vers le centre de la tuile). Mesure par raycast : la surface des panneaux
// WallAstra est à 2,13 m du centre de la tuile, soit 13 cm au-delà du bord de la grille (moulures jusqu'à 1,56 m).
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class StationBuilder
{
    public const string ScenePath = "Assets/_MyAssets/Scenes/Station_VR.unity";
    public const string LightingPath = "Assets/_MyAssets/Scenes/Station_VR_Lighting.lighting";
    public const string SkyboxPath = "Assets/_MyAssets/Skyboxes/Materials/M_Skybox_Space.mat";
    public const string XROriginPath = "Assets/_MyAssets/Prefabs/XROrigin.prefab";
    const string MatDir = StationMaterials.MatDir;

    public const float CeilingY = 3f;
    public const float DoorHalfOpening = 1.59f;       // ouverture de Door_Frame_Square : 3,18 m
    public const float DoorFrameDrop = -0.03f;        // seuil de 6 cm ramené à 3 cm au-dessus du sol
    public const float WallFace = 2.13f;              // distance centre de tuile -> surface d'un mur WallAstra
    public const float FrameHalfDepth = 0.25f;        // Door_Frame_Square : 0,51 m d'épaisseur
    public const float EngineDoorZ = -2f + WallFace + FrameHalfDepth;  // face du cadre alignée sur le mur de la salle des machines (z = 0,13)
    public const float ControlDoorZ = 14f - WallFace - FrameHalfDepth; // face du cadre alignée sur le mur du poste de commande (z = 11,87)

    const StaticEditorFlags DecorFlags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;

    static readonly Color EmergencyColor = new Color(1f, 0.42f, 0.18f);
    static readonly Color AlarmColor = new Color(1f, 0.05f, 0.03f);
    static readonly Color AmbientColor = new Color(0.08f, 0.09f, 0.17f);

    [MenuItem("Tools/Station/Build Scene")]
    public static void BuildScene()
    {
        StationMeasure.PathOf("Platform_Metal"); // initialise le cache des chemins
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ---------- Hiérarchie racine ----------
        var vrSetup = new GameObject("-- VR SETUP --");
        var env = new GameObject("-- ENVIRONMENT --");
        var lighting = new GameObject("-- LIGHTING --");
        var gameplay = new GameObject("-- GAMEPLAY --");

        var engine = Child(env, "EngineRoom");
        var corridor = Child(env, "Corridor");
        var control = Child(env, "ControlRoom");

        BuildEngineShell(engine);
        BuildCorridorShell(corridor);
        BuildControlShell(control);
        Physics.SyncTransforms();

        BuildEngineContent(engine, gameplay);
        BuildCorridorContent(corridor, gameplay);
        BuildControlContent(control, gameplay);
        BuildStarfield(env);
        BuildLighting(lighting);
        SetupEnvironmentLighting();

        // ---------- Joueur ----------
        var xr = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(XROriginPath), scene);
        xr.transform.SetParent(vrSetup.transform, false);
        xr.transform.SetPositionAndRotation(new Vector3(0f, 0f, 9f), Quaternion.Euler(0f, 180f, 0f));

        // Ordre des racines
        vrSetup.transform.SetSiblingIndex(0); env.transform.SetSiblingIndex(1);
        lighting.transform.SetSiblingIndex(2); gameplay.transform.SetSiblingIndex(3);

        EditorSceneManager.SaveScene(scene, ScenePath);
        SetBuildScenes();
        AssetDatabase.SaveAssets();
        StationLog.Write("Build", "=== [Station] Build Scene ===\nScène construite et sauvegardée : " + ScenePath);
    }

    // =====================================================================
    // Enveloppes
    // =====================================================================

    static void BuildEngineShell(GameObject room)
    {
        var floor = Child(room, "Floor"); var ceiling = Child(room, "Ceiling"); var walls = Child(room, "Walls");
        foreach (var x in new[] { -4f, 0f, 4f })
            foreach (var z in new[] { -6f, -2f })
            {
                Spawn("Platform_DarkPlates", floor, new Vector3(x, 0, z), 0, static_: true);
                Ceiling(ceiling, x, z);
            }
        FloorCollider(floor, new Vector3(0, 0, -4), new Vector2(12, 8));

        Wall("WallAstra_Straight_Divided", walls, -4, -6, Vector3.left);
        Wall("WallAstra_Straight", walls, -4, -2, Vector3.left);           // PowerPanel
        Wall("WallAstra_Straight", walls, 4, -6, Vector3.right);           // alcôve du spécimen
        Wall("WallAstra_Straight_Divided", walls, 4, -2, Vector3.right);   // Storage
        Wall("WallAstra_Straight_Broken", walls, -4, -6, Vector3.back);
        Wall("WallAstra_Straight", walls, 0, -6, Vector3.back);            // Generator
        Wall("WallAstra_Straight", walls, 4, -6, Vector3.back);
        Wall("WallAstra_Straight", walls, -4, -2, Vector3.forward);
        Wall("WallAstra_Straight", walls, 4, -2, Vector3.forward);
        DoorFrame(walls, EngineDoorZ, "DoorFrame_ToCorridor");

        var cols = Child(room, "Columns");
        // Colonnes dans les angles intérieurs (elles masquent aussi la jonction des murs)
        float ex = 4f + WallFace, ezBack = -6f - WallFace, ezFront = -2f + WallFace;
        foreach (var c in new[] { new Vector3(-ex, 0, ezBack), new Vector3(-ex, 0, ezFront), new Vector3(ex, 0, ezFront), new Vector3(ex, 0, ezBack) })
            Spawn("Column_Hollow", cols, c, 0, collider: true, static_: true);
    }

    static void BuildCorridorShell(GameObject room)
    {
        var floor = Child(room, "Floor"); var ceiling = Child(room, "Ceiling"); var walls = Child(room, "Walls");
        var trims = Child(room, "Trims");
        foreach (var z in new[] { 2f, 6f, 10f })
        {
            Spawn("Platform_Squares", floor, new Vector3(0, 0, z), 0, static_: true);
            Ceiling(ceiling, 0, z);
            Wall("WallBand_Straight", walls, 0, z, Vector3.left);
            Wall("WallBand_Straight", walls, 0, z, Vector3.right);
            Wall("BottomMetal_Straight", trims, 0, z, Vector3.left, collider: false);
            Wall("BottomMetal_Straight", trims, 0, z, Vector3.right, collider: false);
        }
        FloorCollider(floor, new Vector3(0, 0, 6), new Vector2(4, 12));
    }

    static void BuildControlShell(GameObject room)
    {
        var floor = Child(room, "Floor"); var ceiling = Child(room, "Ceiling"); var walls = Child(room, "Walls");
        foreach (var x in new[] { -2f, 2f })
            foreach (var z in new[] { 14f, 18f })
            {
                Spawn("Platform_Metal2", floor, new Vector3(x, 0, z), 0, static_: true);
                Ceiling(ceiling, x, z);
            }
        FloorCollider(floor, new Vector3(0, 0, 16), new Vector2(8, 8));

        Wall("WallAstra_Straight", walls, -2, 14, Vector3.left);
        Wall("WallAstra_Straight_Divided", walls, -2, 18, Vector3.left);
        Wall("WallAstra_Straight", walls, 2, 14, Vector3.right);
        Wall("WallAstra_Straight_Divided", walls, 2, 18, Vector3.right);
        // Mur vitré du fond (Z = 20)
        Wall("WallAstra_Straight_Window", walls, -2, 18, Vector3.forward);
        Wall("WallAstra_Straight_Window", walls, 2, 18, Vector3.forward);
        // Mur d'entrée (Z = 12) : la porte est centrée en X = 0, à cheval sur deux tuiles ;
        // les modules de 4 m sont décalés de part et d'autre du cadre (x = ±4, de 2 à 6 m), leur excédent passe derrière les murs latéraux.
        Wall("WallAstra_Straight", walls, -4f, 14, Vector3.back);
        Wall("WallAstra_Straight", walls, 4f, 14, Vector3.back);
        DoorFrame(walls, ControlDoorZ, "DoorFrame_ToCorridor");

        var cols = Child(room, "Columns");
        float cx = 2f + WallFace, czFront = 14f - WallFace, czBack = 20f; // la vitre (WallAstra_Straight_Window) a sa face à 2,0 m
        foreach (var c in new[] { new Vector3(-cx, 0, czBack), new Vector3(cx, 0, czBack), new Vector3(-cx, 0, czFront), new Vector3(cx, 0, czFront) })
            Spawn("Column_Hollow", cols, c, 0, collider: true, static_: true);
    }

    // =====================================================================
    // Contenu
    // =====================================================================

    static void BuildEngineContent(GameObject room, GameObject gameplay)
    {
        // ---- Generator (décor, contre le mur du fond) ----
        // Mur du fond : surface à z = -8,13
        var gen = Child(room, "Generator");
        gen.transform.position = new Vector3(0, 0, -7.5f);
        Spawn("Prop_PipeHolder", gen, new Vector3(0, 0, -7.5f), 0, collider: true, static_: true);
        Spawn("Column_Pipes", gen, new Vector3(-2.35f, 0, -7.6f), 0, collider: true, static_: true);
        Spawn("Column_Pipes", gen, new Vector3(2.35f, 0, -7.6f), 0, collider: true, static_: true);
        foreach (var x in new[] { -1.3f, 0f, 1.3f })
            Spawn("Prop_Barrel_Large", gen, new Vector3(x, 0.94f, -7.5f), 0, collider: true, static_: true);
        var fan = Spawn("Prop_Fan_Small", gen, new Vector3(0, 2.2f, -8.08f), 0, collider: true, static_: true);
        fan.transform.rotation = Quaternion.Euler(90, 0, 0);               // disque vertical, face vers la pièce
        foreach (var x in new[] { -1.45f, 1.45f })
        {
            var v = Spawn("Prop_Vent_Small", gen, new Vector3(x, 2.3f, -8.11f), 0, collider: true, static_: true);
            v.transform.rotation = Quaternion.Euler(90, 0, 0);
        }
        Spawn("Prop_Cable_1", gen, new Vector3(1.3f, 0, -6.4f), 30, static_: true);
        Spawn("Prop_Cable_3", gen, new Vector3(-3.2f, 0, -5.9f), 75, static_: true);

        // ---- Décor (hors des zones dégagées devant le PowerPanel et le Storage : z de -4,5 à -1,5) ----
        var props = Child(room, "Props");
        Spawn("Prop_Crate", props, new Vector3(-5.2f, 0, -7.3f), 0, collider: true, static_: true);
        Spawn("Prop_Crate4", props, new Vector3(-5.2f, 1.5f + 0.56f, -7.3f), 20, collider: true, static_: true);
        Spawn("Prop_Chest", props, new Vector3(-5.0f, 0, -5.7f), 90, collider: true, static_: true);
        Spawn("Prop_Barrel_Large", props, new Vector3(-3.75f, 0, -7.75f), 0, collider: true, static_: true);
        Spawn("Prop_Crate3", props, new Vector3(-3.7f, 0.5f, -6.4f), 15, collider: true, static_: true);
        Spawn("Prop_Barrel_Large", props, new Vector3(-4.9f, 0, -0.45f), 0, collider: true, static_: true);
        Spawn("Prop_Barrel_Large", props, new Vector3(-4.3f, 0, -0.5f), 40, collider: true, static_: true);
        Spawn("Prop_Locker", props, new Vector3(5.0f, 0, -0.15f), 180, collider: true, static_: true);
        Spawn("Prop_Barrel1", props, new Vector3(3.9f, 0, -0.35f), 0, collider: true, static_: true);

        // ---- Specimen : alien derrière une vitre en diagonale dans le coin (+X, -Z) ----
        var spec = Child(room, "Specimen");
        float fx = 4f + WallFace, fz = -6f - WallFace;                   // coin intérieur (6,13 ; -8,13)
        var a = new Vector3(fx, 0, fz + 2.83f); var b = new Vector3(fx - 2.83f, 0, fz);
        var mid = (a + b) * 0.5f;
        var outward = new Vector3(1, 0, -1).normalized;                  // vers le coin
        var glass = Spawn("WallAstra_Straight_Window", spec, mid - outward * 2.25f, 0, collider: true, static_: true);
        glass.transform.rotation = Quaternion.LookRotation(outward) * Quaternion.Euler(0, -90, 0);
        glass.name = "ContainmentGlass";
        var alien = Spawn("Alien_Cyclop", spec, new Vector3(fx - 0.9f, 1.14f, fz + 0.9f), -45);
        alien.name = "Alien_Cyclop";
        var sl = NewLight(spec, "SpecimenLight", LightType.Spot, new Color(0.25f, 1f, 0.35f), 4f, 5f);
        sl.transform.position = new Vector3(fx - 1.0f, 2.85f, fz + 1.0f);
        sl.transform.rotation = Quaternion.Euler(90, 0, 0);
        sl.spotAngle = 80f;
        sl.shadows = LightShadows.Soft;                                    // la seule ombre temps réel de la scène

        // ---- Luminaires de plafond ----
        var fixtures = Child(room, "CeilingLights");
        foreach (var p in EngineFixtures) Spawn("Prop_Light_Wide", fixtures, p, 0, static_: true);

        // ---- GAMEPLAY : PowerPanel sur le mur gauche ----
        // Centré en z = -3,0 : le module WallAstra_Straight a une nervure verticale en son milieu (z = -2,0)
        var panel = Child(gameplay, "PowerPanel");
        float wallX = WallSurfaceX(new Vector3(-3f, 1.3f, -3.0f), Vector3.left);
        panel.transform.SetPositionAndRotation(new Vector3(wallX, 1.3f, -3.0f), Quaternion.Euler(0, 90, 0)); // +Z local vers la pièce
        var slotMat = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_EnergySlot_Empty.mat");
        for (int i = 0; i < 3; i++)
        {
            float z = -3.6f + 0.6f * i;
            float sx = WallSurfaceX(new Vector3(-3f, 1.3f, z), Vector3.left);
            var slot = Spawn("Prop_HealthPack", panel, new Vector3(sx + 0.095f, 1.26f, z), -90);  // centre des bounds à 1,30 m
            slot.name = $"EnergySlot_{i + 1}";
            if (slotMat) slot.GetComponentInChildren<Renderer>().sharedMaterial = slotMat;
            var socket = new GameObject("SocketPoint");
            socket.transform.SetParent(slot.transform, false);
            socket.transform.localPosition = Vector3.zero;                // le tube intégré au modèle a le même pivot que Prop_HealthPack_Tube
            socket.transform.localRotation = Quaternion.identity;         // Y vers le haut de la cellule
            float lx = WallSurfaceX(new Vector3(-3f, 0.86f, z), Vector3.left);
            var label = Spawn($"Decal_{i + 1}", panel, new Vector3(lx + 0.008f, 0.86f, z), 0, static_: true);
            label.name = $"SlotLabel_{i + 1}";
            label.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.right); // normale (+Y local) vers la pièce, chiffre à l'endroit
            label.transform.localScale = Vector3.one * 0.15f;
        }

        // ---- Storage : étagère contre le mur droit + cellules ----
        float rWallX = WallSurfaceX(new Vector3(3f, 1.3f, -3.0f), Vector3.right);
        var shelves = Spawn("Prop_Shelves_WideTall", room, new Vector3(rWallX - 0.29f, 0, -3.0f), 90, collider: true, static_: true);
        shelves.name = "StorageShelves";
        Physics.SyncTransforms();
        var storage = Child(gameplay, "Storage");
        storage.transform.position = shelves.transform.position;
        var charged = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_Cell_Charged.mat");
        var depleted = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_Cell_Depleted.mat");
        var cells = new (string name, float dz, bool full)[]
        {
            ("EnergyCell_1", -0.72f, true), ("DepletedCell_1", -0.36f, false), ("EnergyCell_2", 0f, true),
            ("DepletedCell_2", 0.36f, false), ("EnergyCell_3", 0.72f, true),
        };
        float shelfY = ShelfSurfaceY(shelves.transform.position + new Vector3(0, 0, 0), 0.9f, 1.3f);
        foreach (var c in cells)
        {
            var cell = Spawn("Prop_HealthPack_Tube", storage, new Vector3(shelves.transform.position.x - 0.05f, shelfY + 0.14f, -3.0f + c.dz), c.dz * 90f);
            cell.name = c.name;
            cell.GetComponentInChildren<Renderer>().sharedMaterial = c.full ? charged : depleted;
        }

        // ---- AlarmLight_Engine : au plafond, juste au-dessus de la porte ----
        AlarmLight(gameplay, "AlarmLight_Engine", "Prop_Light_Small", new Vector3(0, CeilingY - 0.03f, -0.6f), 0);
    }

    static void BuildCorridorContent(GameObject room, GameObject gameplay)
    {
        var fixtures = Child(room, "CeilingLights");
        foreach (var p in CorridorFixtures) Spawn("Prop_Light_Wide", fixtures, p, 0, static_: true);

        var deco = Child(room, "Details");
        var cables = Wall("TopCables_Straight_Hanging", deco, 0, 6, Vector3.right, collider: false);
        cables.transform.position += Vector3.up * (CeilingY - 5f);        // haut des câbles au plafond
        cables.transform.position += Vector3.left * 0.015f;              // son panneau arrière est à x = 2,000 comme la face du WallBand : z-fighting
        foreach (var z in new[] { 4f, 8f })
        {
            var v = Spawn("Prop_Vent_Wide", deco, new Vector3(0, CeilingY - 0.01f, z), 0, static_: true);
            v.transform.rotation = Quaternion.Euler(180, 0, 0);
        }
        foreach (var x in new[] { -1.25f, 1.25f })
            foreach (var z in new[] { 2f, 6f, 10f })
                Spawn("Decal_Line_Straight", deco, new Vector3(x, 0.003f, z), 0, static_: true);

        AlarmLight(gameplay, "AlarmLight_Corridor", "Prop_Light_Small", new Vector3(0, CeilingY - 0.03f, 6f), 90);
    }

    static void BuildControlContent(GameObject room, GameObject gameplay)
    {
        var fixtures = Child(room, "CeilingLights");
        foreach (var p in ControlFixtures) Spawn("Prop_Light_Wide", fixtures, p, 0, static_: true);

        // ---- Console : bureau en L, l'utilisateur se tient dans l'encoche face à la vitre (+Z) ----
        var props = Child(room, "Props");
        // Pivot du bureau = angle intérieur de l'encoche ; le bras latéral (+X) commence vers x_pivot - 0,25
        var desk = Spawn("Prop_Desk_L", props, new Vector3(0.8f, 0, 16.6f), 0, collider: true, static_: true);
        desk.name = "ConsoleDesk";
        Spawn("Prop_Chair", props, new Vector3(-1.0f, 0, 15.55f), 165, collider: true, static_: true);
        Physics.SyncTransforms();

        var console = Child(gameplay, "Console");
        console.transform.position = new Vector3(0f, 0, 16.85f);
        float topY = ShelfSurfaceY(new Vector3(0f, 0, 16.85f), 0.8f, 1.1f);
        for (int i = 0; i < 4; i++)
        {
            var s = new GameObject($"ButtonSlot_{i + 1}");
            s.transform.SetParent(console.transform, false);
            s.transform.position = new Vector3(-0.225f + 0.15f * i, topY, 16.85f);
        }

        // ---- ScreenMount + ordinateurs sur le mur gauche ----
        float wallX = WallSurfaceX(new Vector3(-2f, 1.6f, 16.5f), Vector3.left);
        var mount = Child(gameplay, "ScreenMount");
        mount.transform.SetPositionAndRotation(new Vector3(wallX + 0.05f, 1.6f, 16.5f), Quaternion.Euler(0, 90, 0)); // +Z local vers la pièce
        Spawn("Prop_Computer", props, new Vector3(wallX + 0.35f, 0, 14.6f), 90, collider: true, static_: true);
        Spawn("Prop_Computer", props, new Vector3(wallX + 0.35f, 0, 18.4f), 90, collider: true, static_: true);
        Spawn("Prop_AccessPoint", props, new Vector3(wallX + 0.22f, 0, 13.4f), 0, collider: true, static_: true);

        AlarmLight(gameplay, "AlarmLight_Control", "Prop_Light_Small", new Vector3(0, CeilingY - 0.03f, 13.4f), 0);
    }

    // =====================================================================
    // Éclairage et ambiance
    // =====================================================================

    static readonly Vector3[] EngineFixtures = { new Vector3(-3, CeilingY - 0.03f, -4.5f), new Vector3(3, CeilingY - 0.03f, -4.5f), new Vector3(0, CeilingY - 0.03f, -2.6f) };
    static readonly Vector3[] CorridorFixtures = { new Vector3(0, CeilingY - 0.03f, 2f), new Vector3(0, CeilingY - 0.03f, 10f) };
    static readonly Vector3[] ControlFixtures = { new Vector3(-2, CeilingY - 0.03f, 15.5f), new Vector3(2, CeilingY - 0.03f, 15.5f), new Vector3(0, CeilingY - 0.03f, 18.8f) };

    static void BuildLighting(GameObject root)
    {
        var emergency = Child(root, "Lights_Emergency");
        var ePos = new[] { new Vector3(-3.5f, 2.5f, -3.0f), new Vector3(3.5f, 2.5f, -4.0f), new Vector3(0, 2.5f, 6f), new Vector3(-1.5f, 2.5f, 14.5f), new Vector3(1.5f, 2.5f, 18.5f) };
        for (int i = 0; i < ePos.Length; i++)
        {
            var l = NewLight(emergency, $"EmergencyLight_{i + 1}", LightType.Point, EmergencyColor, 1f, 6f);
            l.transform.position = ePos[i];
        }

        var main = Child(root, "Lights_Main");
        int n = 0;
        foreach (var p in EngineFixtures.Concat(CorridorFixtures).Concat(ControlFixtures))
        {
            var l = NewLight(main, $"MainLight_{++n}", LightType.Point, new Color(1f, 0.97f, 0.9f), 2.5f, 7f);
            l.transform.position = p + Vector3.down * 0.3f;
        }
        main.SetActive(false);
    }

    static void SetupEnvironmentLighting()
    {
        // Ciel noir
        var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyboxPath);
        if (sky == null) { sky = new Material(Shader.Find("Skybox/Procedural")); AssetDatabase.CreateAsset(sky, SkyboxPath); }
        sky.shader = Shader.Find("Skybox/Procedural");
        sky.SetFloat("_SunDisk", 0f);
        sky.SetFloat("_AtmosphereThickness", 0f);
        sky.SetColor("_SkyTint", Color.black);
        sky.SetColor("_GroundColor", Color.black);
        sky.SetFloat("_Exposure", 0f);
        EditorUtility.SetDirty(sky);
        RenderSettings.skybox = sky;
        RenderSettings.sun = null;

        // Lumière ambiante : bleu nuit très sombre, mode Color
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = AmbientColor;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = null;
        RenderSettings.reflectionIntensity = 0.3f;
        RenderSettings.fog = false;

        // Pas de lightmaps : ni GI précalculée ni GI temps réel
        var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingPath);
        if (ls == null) { ls = new LightingSettings { name = "Station_VR_Lighting" }; AssetDatabase.CreateAsset(ls, LightingPath); }
        ls.bakedGI = false;
        ls.realtimeGI = false;
        EditorUtility.SetDirty(ls);
        Lightmapping.lightingSettings = ls;
    }

    static void BuildStarfield(GameObject env)
    {
        var go = new GameObject("Starfield");
        go.transform.SetParent(env.transform, false);
        go.transform.position = new Vector3(0, 1.5f, 20f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.duration = 5f;
        main.prewarm = true;
        main.playOnAwake = true;
        main.startLifetime = 20_000f;                                     // ~5,5 h ; au-delà, le Prewarm signale une erreur de précision
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 4f);        // 1,5 à 4 m à 200 m : quelques pixels dans le casque
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.85f, 1f), Color.white);
        main.maxParticles = 2000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var em = ps.emission;
        em.rateOverTime = 2000f / main.duration;                         // le Prewarm remplit les 2 000 étoiles dès le départ
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 200f;
        sh.radiusThickness = 0f;                                          // étoiles sur la coquille de la sphère
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.maxParticleSize = 0.5f;
    }

    static void SetBuildScenes()
    {
        var others = EditorBuildSettings.scenes.Where(s => s.path != ScenePath && s.path != "Assets/_MyAssets/Scenes/Cirque_VR.unity" && System.IO.File.Exists(s.path));
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(others).ToArray();
    }

    // =====================================================================
    // Utilitaires
    // =====================================================================

    static GameObject Child(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    public static GameObject Spawn(string module, GameObject parent, Vector3 pos, float rotY, bool collider = false, bool static_ = false)
    {
        var path = StationMeasure.PathOf(module);
        if (path == null) { Debug.LogError($"[Station] Module absent : {module}"); return Child(parent, module + "_MISSING"); }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent.scene);
        go.transform.SetParent(parent.transform, false);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, rotY, 0));
        if (collider)
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                if (!mf.GetComponent<Collider>()) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        if (static_) SetStatic(go);
        return go;
    }

    // Module mural placé sur le bord « dir » de la tuile centrée en (x, z) : l'axe +X local du module pointe vers dir
    static GameObject Wall(string module, GameObject parent, float x, float z, Vector3 dir, bool collider = true)
    {
        var go = Spawn(module, parent, new Vector3(x, 0, z), 0, collider, true);
        go.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0, -90, 0);
        return go;
    }

    static void DoorFrame(GameObject parent, float z, string name)
    {
        var go = Spawn("Door_Frame_Square", parent, new Vector3(0, DoorFrameDrop, z), 0, collider: true, static_: true);
        go.name = name;
    }

    static void Ceiling(GameObject parent, float x, float z)
    {
        var go = Spawn("Platform_Simple", parent, new Vector3(x, CeilingY, z), 0, static_: true);
        go.transform.rotation = Quaternion.Euler(180, 0, 0);
    }

    static void FloorCollider(GameObject floor, Vector3 center, Vector2 size)
    {
        var go = Child(floor, "FloorCollider");
        go.transform.position = center;
        var bc = go.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, -0.1f, 0);
        bc.size = new Vector3(size.x, 0.2f, size.y);
        SetStatic(go);
    }

    static void AlarmLight(GameObject parent, string name, string module, Vector3 pos, float rotY)
    {
        var fixture = Spawn(module, parent, pos, rotY);
        fixture.name = name;
        var l = NewLight(fixture, "AlarmPointLight", LightType.Point, AlarmColor, 2f, 8f);
        l.transform.localPosition = new Vector3(0, -0.35f, 0);
        l.gameObject.SetActive(false);
    }

    static Light NewLight(GameObject parent, string name, LightType type, Color color, float intensity, float range)
    {
        var go = Child(parent, name);
        var l = go.AddComponent<Light>();
        l.type = type;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        l.lightmapBakeType = LightmapBakeType.Realtime;
        return l;
    }

    static void SetStatic(GameObject go)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, DecorFlags);
    }

    // Surface intérieure d'un mur : rayon horizontal depuis un point de la pièce
    static float WallSurfaceX(Vector3 from, Vector3 dir)
    {
        Physics.SyncTransforms();
        if (Physics.Raycast(from, dir, out var hit, 10f)) return hit.point.x;
        Debug.LogWarning($"[Station] Mur introuvable depuis {from} vers {dir}");
        return from.x + dir.x * 2.5f;
    }

    // Première surface horizontale trouvée entre minY et maxY sous un point (tablette, plateau de bureau)
    static float ShelfSurfaceY(Vector3 xz, float minY, float maxY)
    {
        Physics.SyncTransforms();
        var hits = Physics.RaycastAll(new Vector3(xz.x, maxY + 0.3f, xz.z), Vector3.down, 2f);
        foreach (var h in hits.OrderByDescending(h => h.point.y))
            if (h.point.y >= minY && h.point.y <= maxY + 0.05f) return h.point.y;
        Debug.LogWarning($"[Station] Surface introuvable sous {xz} entre {minY} et {maxY}");
        return minY;
    }
}
