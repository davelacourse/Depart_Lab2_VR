// Mesure des modules Quaternius utilisés par la station : taille (Renderer.bounds),
// décalage du pivot par rapport au centre et surface des faces par direction (pour repérer la face intérieure des murs).
// Menu : Tools/Station/Measure Modules
// Batch : -executeMethod StationMeasure.MeasureModules
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class StationMeasure
{
    public static readonly string[] Modules =
    {
        "Platform_Metal", "Platform_DarkPlates", "Platform_Squares", "Platform_Metal2",
        "WallAstra_Straight", "WallAstra_Straight_Divided", "WallAstra_Straight_Broken", "WallAstra_Straight_Window",
        "WallAstra_Corner_Square_Inner", "WallAstra_Corner_Square_Outer", "WallBand_Straight", "WallBand_Corner_Square_Inner",
        "WallWindow_Straight", "WallWindow_Corner_Square_Inner",
        "Door_Frame_Square", "Door_Frame_SquareTall", "Door_Simple",
        "BottomMetal_Straight", "BottomMetal_Corner_Square_Inner", "TopAstra_Straight", "TopCables_Straight", "TopCables_Straight_Hanging",
        "Column_Pipes", "Column_Astra", "Column_Simple", "Column_Hollow",
        "Prop_Desk_L", "Prop_Chair", "Prop_Computer", "Prop_AccessPoint", "Prop_Barrel_Large", "Prop_HealthPack", "Prop_HealthPack_Tube",
        "Prop_Locker", "Prop_Shelves_WideTall", "Prop_Crate", "Prop_Crate_Large", "Prop_Crate3", "Prop_Crate4", "Prop_Chest",
        "Prop_Fan_Small", "Prop_Vent_Small", "Prop_Vent_Big", "Prop_Vent_Wide", "Prop_Cable_1", "Prop_Cable_3", "Prop_PipeHolder",
        "Prop_Light_Small", "Prop_Light_Wide", "Prop_Light_Corner", "Prop_Barrel1",
        "Alien_Cyclop", "Alien_Scolitex", "Decal_1", "Decal_2", "Decal_3", "Decal_Line_Straight",
    };

    static Dictionary<string, string> _paths;
    public static string PathOf(string module)
    {
        if (_paths == null)
        {
            _paths = new Dictionary<string, string>();
            foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { StationMaterials.Root }))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                _paths[System.IO.Path.GetFileNameWithoutExtension(p)] = p;
            }
        }
        return _paths.TryGetValue(module, out var r) ? r : null;
    }

    [MenuItem("Tools/Station/Measure Modules")]
    public static void MeasureModules()
    {
        _paths = null;
        var sb = new StringBuilder();
        sb.AppendLine("=== [Station] Measure Modules (m) ===");
        sb.AppendLine("module | taille XYZ | pivot->centre | min | max | surface des faces par normale (+X -X +Y -Y +Z -Z) | scale import");
        foreach (var m in Modules)
        {
            var p = PathOf(m);
            if (p == null) { sb.AppendLine($"{m} : ABSENT"); continue; }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            var go = (GameObject)Object.Instantiate(prefab);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            try
            {
                var rs = go.GetComponentsInChildren<Renderer>();
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                var area = new float[6];
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh = mf.sharedMesh; var v = mesh.vertices; var t = mesh.triangles; var tr = mf.transform;
                    for (int i = 0; i < t.Length; i += 3)
                    {
                        var a = tr.TransformPoint(v[t[i]]); var bb = tr.TransformPoint(v[t[i + 1]]); var c = tr.TransformPoint(v[t[i + 2]]);
                        var n = Vector3.Cross(bb - a, c - a); float ar = n.magnitude * 0.5f; if (ar < 1e-8f) continue; n.Normalize();
                        if (n.x > 0.7f) area[0] += ar; else if (n.x < -0.7f) area[1] += ar;
                        else if (n.y > 0.7f) area[2] += ar; else if (n.y < -0.7f) area[3] += ar;
                        else if (n.z > 0.7f) area[4] += ar; else if (n.z < -0.7f) area[5] += ar;
                    }
                }
                var imp = (ModelImporter)AssetImporter.GetAtPath(p);
                sb.AppendLine($"{m} | {V(b.size)} | {V(b.center)} | {V(b.min)} | {V(b.max)} | {string.Join(" ", area.Select(x => x.ToString("0.0")))} | file={imp.fileScale} global={imp.globalScale} useFile={imp.useFileScale} | renderers={rs.Length}");
            }
            finally { Object.DestroyImmediate(go); }
        }
        StationLog.Write("Measure", sb.ToString());
    }

    static string V(Vector3 v) => $"({v.x:0.00}, {v.y:0.00}, {v.z:0.00})";
}
