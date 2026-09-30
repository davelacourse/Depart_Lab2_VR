// Validation de la scène Station_VR + captures d'écran dans Screenshots/ (racine du projet).
// Menu : Tools/Station/Validate
// Batch : -executeMethod StationValidate.Validate
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StationValidate
{
    static StringBuilder _sb;
    static int _fail;

    [MenuItem("Tools/Station/Validate")]
    public static void Validate()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != StationBuilder.ScenePath)
            scene = EditorSceneManager.OpenScene(StationBuilder.ScenePath, OpenSceneMode.Single);
        Physics.SyncTransforms();
        _sb = new StringBuilder("=== [Station] Validate ===\n");
        _fail = 0;

        CheckCamerasAndListeners();
        CheckMaterials();
        CheckFloor();
        CheckPassages();
        CheckWalls();
        CheckNamedObjects();
        CheckHeights();
        Stats();
        Captures();

        _sb.AppendLine(_fail == 0 ? "RÉSULTAT : toutes les vérifications passent." : $"RÉSULTAT : {_fail} vérification(s) en échec.");
        StationLog.Write("Validate", _sb.ToString());
    }

    static void Result(string name, bool ok, string detail = "")
    {
        if (!ok) _fail++;
        _sb.AppendLine($"[{(ok ? "OK" : "ÉCHEC")}] {name}{(detail.Length > 0 ? " — " + detail : "")}");
    }

    static IEnumerable<T> All<T>() where T : Component =>
        EditorSceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));

    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

    // ---------------------------------------------------------------------
    static void CheckCamerasAndListeners()
    {
        var cams = All<Camera>().Where(c => c.enabled && c.gameObject.activeInHierarchy).ToList();
        var ears = All<AudioListener>().Where(a => a.enabled && a.gameObject.activeInHierarchy).ToList();
        bool camOk = cams.Count == 1 && cams[0].GetComponentInParent<Unity.XR.CoreUtils.XROrigin>() != null;
        bool earOk = ears.Count == 1 && ears[0].GetComponentInParent<Unity.XR.CoreUtils.XROrigin>() != null;
        Result("Caméra unique (XROrigin)", camOk, string.Join(", ", cams.Select(c => PathOf(c.transform))));
        Result("AudioListener unique (XROrigin)", earOk, string.Join(", ", ears.Select(a => PathOf(a.transform))));
    }

    static void CheckMaterials()
    {
        var bad = new List<string>();
        foreach (var r in All<Renderer>())
            foreach (var m in r.sharedMaterials)
                if (m == null || m.shader == null || m.shader.name == "Hidden/InternalErrorShader" || !m.shader.isSupported)
                    bad.Add(PathOf(r.transform) + " : " + (m == null ? "matériau manquant" : m.shader.name));
        Result("Matériaux et shaders valides", bad.Count == 0, bad.Count == 0 ? $"{All<Renderer>().Count()} renderers" : string.Join("; ", bad.Take(20)));
    }

    // Zones praticables (intérieur des murs) et passages de porte
    static readonly (string name, float x0, float x1, float z0, float z1)[] WalkAreas =
    {
        ("EngineRoom", -5.9f, 5.9f, -7.9f, 0f),
        ("Corridor", -1.8f, 1.8f, 1.0f, 11.0f),
        ("ControlRoom", -3.9f, 3.9f, 12f, 19.8f),
        ("Passage Z=0", -1.5f, 1.5f, 0f, 1.0f),
        ("Passage Z=12", -1.5f, 1.5f, 11.0f, 12f),
    };

    static void CheckFloor()
    {
        foreach (var a in WalkAreas)
        {
            int n = 0; var holes = new List<string>(); int covered = 0;
            for (float x = a.x0; x <= a.x1 + 1e-3f; x += 0.5f)
                for (float z = a.z0; z <= a.z1 + 1e-3f; z += 0.5f)
                {
                    n++;
                    var hits = Physics.RaycastAll(new Vector3(x, 2f, z), Vector3.down, 3f);
                    bool floor = hits.Any(h => h.point.y >= -0.05f && h.point.y <= 0.05f);
                    // Un objet posé au sol (caisse, bureau) masque le plancher : on le compte à part
                    bool blocked = hits.Any(h => h.point.y > 0.05f);
                    if (!floor) holes.Add($"({x:0.0},{z:0.0})");
                    else if (blocked) covered++;
                }
            Result($"Sol continu {a.name}", holes.Count == 0,
                $"{n} rayons, {covered} passent aussi par un objet posé" + (holes.Count > 0 ? ", trous : " + string.Join(" ", holes.Take(30)) : ""));
        }
    }

    static void CheckPassages()
    {
        foreach (var (from, to) in new[] { (new Vector3(0, 1, 8), new Vector3(0, 1, -4)), (new Vector3(0, 1, 4), new Vector3(0, 1, 16)) })
        {
            var p1 = from + Vector3.down * 0.6f; var p2 = from + Vector3.up * 0.6f; // capsule de 1,8 m, rayon 0,3
            var d = to - from;
            // Le joueur (XROrigin) est lui-même dans le corridor : ses colliders sont ignorés
            var hits = Physics.CapsuleCastAll(p1, p2, 0.3f, d.normalized, d.magnitude)
                .Where(h => h.collider.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>() == null).ToArray();
            Result($"Passage libre {from} -> {to}", hits.Length == 0, string.Join(", ", hits.Select(h => PathOf(h.collider.transform) + " @" + h.point.ToString("0.00"))));
        }
    }

    static void CheckWalls()
    {
        var centers = new (string, Vector3)[] { ("EngineRoom", new Vector3(0, 1.5f, -4)), ("Corridor", new Vector3(0, 1.5f, 6)), ("ControlRoom", new Vector3(0, 1.5f, 16)) };
        foreach (var (name, c) in centers)
        {
            var open = new List<string>(); int doors = 0;
            for (int i = 0; i < 72; i++)
            {
                var dir = Quaternion.Euler(0, i * 5f, 0) * Vector3.forward;
                bool hit = Physics.Raycast(c, dir, out var h, 10f);
                float dist = hit ? h.distance : 10f;
                if (ThroughDoor(c, dir, dist)) { doors++; continue; }
                if (!hit) open.Add($"{i * 5}°");
            }
            Result($"Murs fermés {name}", open.Count == 0, $"72 rayons, {doors} à travers une porte" + (open.Count > 0 ? ", ouverts : " + string.Join(" ", open) : ""));
        }
    }

    static bool ThroughDoor(Vector3 c, Vector3 dir, float dist)
    {
        foreach (var dz in new[] { StationBuilder.EngineDoorZ, StationBuilder.ControlDoorZ })
        {
            if (Mathf.Abs(dir.z) < 1e-4f) continue;
            float t = (dz - c.z) / dir.z;
            if (t < 0 || t > dist + 0.5f) continue;
            if (Mathf.Abs(c.x + dir.x * t) < StationBuilder.DoorHalfOpening) return true;
        }
        return false;
    }

    static readonly string[] Named =
    {
        "-- VR SETUP --", "-- ENVIRONMENT --", "-- LIGHTING --", "-- GAMEPLAY --",
        "EngineRoom", "Corridor", "ControlRoom", "Starfield", "Lights_Emergency", "Lights_Main",
        "PowerPanel", "EnergySlot_1", "EnergySlot_2", "EnergySlot_3", "Storage",
        "EnergyCell_1", "EnergyCell_2", "EnergyCell_3", "DepletedCell_1", "DepletedCell_2",
        "Console", "ButtonSlot_1", "ButtonSlot_2", "ButtonSlot_3", "ButtonSlot_4", "ScreenMount",
        "AlarmLight_Engine", "AlarmLight_Corridor", "AlarmLight_Control", "Generator", "Specimen",
    };

    static GameObject Find(string name) =>
        All<Transform>().FirstOrDefault(t => t.name == name)?.gameObject;

    static void CheckNamedObjects()
    {
        var missing = Named.Where(n => Find(n) == null).ToList();
        Result("Objets nommés présents", missing.Count == 0, missing.Count == 0 ? $"{Named.Length} objets" : "absents : " + string.Join(", ", missing));

        var xr = All<Unity.XR.CoreUtils.XROrigin>().FirstOrDefault();
        Result("XROrigin sous -- VR SETUP -- à (0,0,9) face à -Z",
            xr != null && xr.transform.root.name == "-- VR SETUP --" && Vector3.Distance(xr.transform.root.GetChild(0).position, new Vector3(0, 0, 9)) < 0.01f
            && Vector3.Dot(xr.transform.root.GetChild(0).forward, Vector3.back) > 0.99f);

        // Cellules, bornes, repères : aucun collider, Rigidbody ni script
        var bare = new[] { "EnergySlot_1", "EnergySlot_2", "EnergySlot_3", "EnergyCell_1", "EnergyCell_2", "EnergyCell_3", "DepletedCell_1", "DepletedCell_2",
            "ButtonSlot_1", "ButtonSlot_2", "ButtonSlot_3", "ButtonSlot_4", "ScreenMount", "SocketPoint" };
        var offenders = new List<string>();
        foreach (var t in All<Transform>().Where(t => bare.Contains(t.name)))
            foreach (var c in t.GetComponentsInChildren<Component>(true))
                if (c is Collider || c is Rigidbody || c is MonoBehaviour) offenders.Add(PathOf(c.transform) + ":" + c.GetType().Name);
        Result("Cellules, bornes et repères sans collider/Rigidbody/script", offenders.Count == 0, string.Join(", ", offenders));

        // -- GAMEPLAY -- : aucun script ni composant XR
        var gp = Find("-- GAMEPLAY --");
        var scripts = gp ? gp.GetComponentsInChildren<MonoBehaviour>(true).Select(m => PathOf(m.transform) + ":" + m.GetType().Name).ToList() : new List<string>();
        Result("-- GAMEPLAY -- sans script", scripts.Count == 0, string.Join(", ", scripts));

        // SocketPoint : un par borne
        var sockets = All<Transform>().Count(t => t.name == "SocketPoint" && t.parent.name.StartsWith("EnergySlot_"));
        Result("SocketPoint sous chaque EnergySlot", sockets == 3, $"{sockets} trouvés");

        // Lumières : Lights_Main désactivé, alarmes éteintes, pas de Directional, une seule ombre
        var lights = All<Light>().ToList();
        Result("Lights_Main désactivé", Find("Lights_Main") && !Find("Lights_Main").activeSelf);
        Result("Lumières d'alarme présentes et éteintes", lights.Count(l => l.transform.parent && l.transform.parent.name.StartsWith("AlarmLight_") && !l.gameObject.activeSelf) == 3);
        Result("Aucune Directional Light", lights.All(l => l.type != LightType.Directional));
        var shadowed = lights.Where(l => l.shadows != LightShadows.None).Select(l => l.name).ToList();
        Result("Au plus une lumière avec ombres", shadowed.Count <= 1, string.Join(", ", shadowed));

        // Pas de téléportation
        var tele = All<MonoBehaviour>().Where(m => m.GetType().Name is "TeleportationArea" or "TeleportationAnchor").ToList();
        Result("Aucune Teleportation Area / Anchor", tele.Count == 0);

        // Statique
        var nonStatic = new[] { "EnergyCell_", "DepletedCell_", "EnergySlot_", "AlarmLight_", "Alien_" };
        var wrong = All<Renderer>().Where(r => nonStatic.Any(p => PathOf(r.transform).Contains(p)) == GameObjectUtility.GetStaticEditorFlags(r.gameObject).HasFlag(StaticEditorFlags.BatchingStatic))
            .Where(r => !(r is ParticleSystemRenderer) && !PathOf(r.transform).StartsWith("-- VR SETUP --")).Select(r => PathOf(r.transform)).ToList();
        Result("Drapeaux Static (décor statique, éléments de jeu mobiles)", wrong.Count == 0, string.Join(", ", wrong.Take(15)));
    }

    static void CheckHeights()
    {
        void Range(string prefix, float lo, float hi, bool bounds)
        {
            var objs = All<Transform>().Where(t => t.name.StartsWith(prefix)).ToList();
            var det = objs.Select(t =>
            {
                float y = bounds && t.GetComponentInChildren<Renderer>() ? t.GetComponentInChildren<Renderer>().bounds.center.y : t.position.y;
                return (t.name, y);
            }).ToList();
            Result($"Hauteur {prefix}* entre {lo} et {hi} m", det.Count > 0 && det.All(d => d.y >= lo && d.y <= hi), string.Join(", ", det.Select(d => $"{d.name}={d.y:0.00}")));
        }
        Range("EnergySlot_", 1.2f, 1.4f, true);
        Range("EnergyCell_", 0.9f, 1.5f, true);
        Range("DepletedCell_", 0.9f, 1.5f, true);
        Range("ButtonSlot_", 0.9f, 1.0f, false);
    }

    static void Stats()
    {
        long tris = 0; int draws = 0, renderers = 0;
        foreach (var r in All<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy))
        {
            var mf = r.GetComponent<MeshFilter>();
            var mesh = mf ? mf.sharedMesh : (r as SkinnedMeshRenderer)?.sharedMesh;
            if (mesh == null) continue;
            renderers++;
            for (int s = 0; s < mesh.subMeshCount; s++) tris += mesh.GetIndexCount(s) / 3;
            draws += mesh.subMeshCount;
        }
        _sb.AppendLine($"[INFO] {renderers} renderers maillés, {tris:N0} triangles, ~{draws} draw calls avant batching (indicatif, par œil ; le static batching et le SRP Batcher réduisent ce nombre).");
    }

    // ---------------------------------------------------------------------
    static void Captures()
    {
        var sf = Find("Starfield")?.GetComponent<ParticleSystem>();
        // Starfield simulé après la vue de dessus (le Prewarm ne s'applique pas tout seul en mode édition)
        var shots = new List<string>();

        // Vue de dessus : les plafonds (plans à une face tournés vers le bas) sont invisibles d'en haut.
        // Lights_Main est allumé le temps de cette seule capture pour lire le plan ; les autres sont dans l'éclairage d'urgence réel.
        var main = Find("Lights_Main");
        if (main) main.SetActive(true);
        if (sf) sf.Clear();                                                // pas d'étoiles géantes en projection orthographique
        shots.Add(StationCapture.Render("01_TopView_MainLightsOn.png", new Vector3(0, 60, 6), new Vector3(0, 0, 6.001f), 60, true, 15f, 1400, 1600));
        if (main) main.SetActive(false);
        if (sf) sf.Simulate(5.5f, true, true);

        shots.Add(StationCapture.Render("02_PlayerView_ToEngineRoom.png", new Vector3(0, 1.7f, 9), new Vector3(0, 1.5f, 0), 90));
        shots.Add(StationCapture.Render("03_PowerPanel.png", new Vector3(-3.4f, 1.5f, -3.0f), new Vector3(-6.1f, 1.15f, -3.0f), 70));
        shots.Add(StationCapture.Render("04_Storage.png", new Vector3(3.2f, 1.5f, -3.0f), new Vector3(5.8f, 1.2f, -3.0f), 70));
        shots.Add(StationCapture.Render("05_ControlRoom_Window.png", new Vector3(0f, 1.7f, 15.9f), new Vector3(0, 1.5f, 20), 80));
        // Vues complémentaires
        shots.Add(StationCapture.Render("06_EngineRoom_Overview.png", new Vector3(0.5f, 2.4f, -0.3f), new Vector3(0, 0.8f, -7f), 95));
        shots.Add(StationCapture.Render("07_ControlRoom_FromWindow.png", new Vector3(2.5f, 1.8f, 19.3f), new Vector3(-2.5f, 1f, 13.5f), 90));
        shots.Add(StationCapture.Render("08_EngineRoom_ToDoor.png", new Vector3(0f, 1.7f, -6.5f), new Vector3(0, 1.4f, 0f), 95));
        shots.Add(StationCapture.Render("09_ControlRoom_ScreenWall.png", new Vector3(1.8f, 1.6f, 16.5f), new Vector3(-4f, 1.1f, 16.5f), 85));
        if (sf) sf.Clear();
        _sb.AppendLine("Captures : " + string.Join(", ", shots.Select(System.IO.Path.GetFileName)));
    }
}
