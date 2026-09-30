// Création des matériaux URP Lit pour les modèles Quaternius (MegaKit + EssentialsKit).
// Menu : Tools/Station/Create Materials
// Batch : Unity.exe -batchmode -projectPath <projet> -executeMethod StationMaterials.CreateMaterials -logFile <fichier> -quit
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;

public static class StationMaterials
{
    public const string Root = "Assets/ImportedAssets/Quaternius";
    public const string MatDir = Root + "/Materials";
    public const string GenTexDir = Root + "/Materials/GeneratedTextures";

    static readonly Color MidGrey = new Color(0.5f, 0.5f, 0.5f, 1f);

    // Couleurs pour les matériaux sans texture dont le nom indique une couleur (aliens, verre)
    static readonly Dictionary<string, Color> NamedColors = new Dictionary<string, Color>
    {
        { "M_Alien_Body_Blue", new Color(0.18f, 0.32f, 0.75f) },
        { "M_Alien_Cyan", new Color(0.10f, 0.75f, 0.75f) },
        { "M_Alien_Orange", new Color(0.95f, 0.45f, 0.10f) },
        { "M_Alien_Eyes", new Color(0.95f, 0.90f, 0.30f) },
    };

    // Teinte des décalques selon le nom
    static readonly Dictionary<string, Color> DecalTints = new Dictionary<string, Color>
    {
        { "M_Decal_White", Color.white },
        { "MI_Decal_Grey", new Color(0.45f, 0.45f, 0.45f) },
        { "MI_Decal_Line", new Color(0.95f, 0.75f, 0.15f) },
        { "MI_Decal_Red", new Color(0.8f, 0.08f, 0.05f) },
        { "M_Decal_Screen", new Color(0.35f, 0.9f, 1f) },
    };

    [MenuItem("Tools/Station/Create Materials")]
    public static void CreateMaterials()
    {
        var log = new StringBuilder();
        log.AppendLine("=== [Station] Create Materials ===");
        EnsureFolder(MatDir);
        EnsureFolder(GenTexDir);

        // 1) Inventaire des textures (en cas de doublon entre kits, les fichiers sont identiques : on garde MegaKit)
        var textures = new Dictionary<string, string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/MegaKit/Textures", Root + "/EssentialsKit/Textures" }))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            var n = Path.GetFileNameWithoutExtension(p);
            if (!textures.ContainsKey(n) || p.Contains("/MegaKit/")) textures[n] = p;
        }
        // Faute de frappe dans le pack : T_PaddingWall_Emissive pour T_PaddedWall
        if (textures.ContainsKey("T_PaddingWall_Emissive") && !textures.ContainsKey("T_PaddedWall_Emissive"))
            textures["T_PaddedWall_Emissive"] = textures["T_PaddingWall_Emissive"];

        // 2) Noms de matériaux référencés par les FBX
        var models = AssetDatabase.FindAssets("t:Model", new[] { Root + "/MegaKit/Models", Root + "/EssentialsKit/Models" })
            .Select(AssetDatabase.GUIDToAssetPath).ToList();
        var matNames = new SortedDictionary<string, List<string>>();
        foreach (var p in models)
            foreach (var n in EmbeddedMaterialNames(p))
            {
                if (!matNames.TryGetValue(n, out var l)) matNames[n] = l = new List<string>();
                l.Add(Path.GetFileNameWithoutExtension(p));
            }
        log.AppendLine($"{models.Count} FBX, {matNames.Count} matériaux distincts référencés :");

        // 3) Préparation des textures (normal maps, alpha des décalques)
        if (textures.TryGetValue("T_Decals", out var decalPath))
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(decalPath);
            if (ti.alphaSource != TextureImporterAlphaSource.FromGrayScale || !ti.alphaIsTransparency)
            {
                ti.alphaSource = TextureImporterAlphaSource.FromGrayScale;
                ti.alphaIsTransparency = true;
                ti.SaveAndReimport();
            }
        }
        // Filtrage trilinéaire + anisotrope : évite le « grouillement » des textures en VR
        foreach (var kv in textures)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(kv.Value);
            if (ti.filterMode != FilterMode.Trilinear || ti.anisoLevel < 8) { ti.filterMode = FilterMode.Trilinear; ti.anisoLevel = 8; ti.SaveAndReimport(); }
        }
        foreach (var kv in textures.Where(k => k.Key.EndsWith("_Normal")))
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(kv.Value);
            if (ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
        }

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var created = new Dictionary<string, Material>();
        var untextured = new List<string>();
        var ormCache = new Dictionary<string, (Texture2D ms, Texture2D occ)>();

        foreach (var kv in matNames)
        {
            var name = kv.Key;
            var mat = LoadOrCreate(name, shader);
            ResetLit(mat);
            string desc;

            if (name == "M_Black")
            {
                mat.SetColor("_BaseColor", new Color(0.02f, 0.02f, 0.02f));
                mat.SetFloat("_Smoothness", 0.85f);
                desc = "noir lisse";
            }
            else if (name == "M_Light")
            {
                mat.SetColor("_BaseColor", Color.white);
                SetEmission(mat, null, Color.white);                 // pas de HDR > 1 : les tubes fins scintillaient en VR
                desc = "blanc, émissif (x1)";
            }
            else if (name == "M_LightFade_Red")
            {
                mat.SetColor("_BaseColor", new Color(1f, 0.1f, 0.05f, 0.6f));
                SetTransparent(mat);
                SetEmission(mat, null, new Color(1f, 0.08f, 0.03f));
                desc = "rouge émissif, transparent";
            }
            else if (DecalTints.TryGetValue(name, out var tint) && decalPath != null)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(decalPath);
                mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", tint);
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0.5f);
                mat.SetFloat("_Smoothness", 0.3f);
                if (name == "M_Decal_Screen") SetEmission(mat, tex, tint);
                desc = "T_Decals + Alpha Clipping (alpha depuis les niveaux de gris), teinte " + ColorUtility.ToHtmlStringRGB(tint)
                       + (name == "M_Decal_Screen" ? ", émission cyan (écran)" : "");
            }
            else if (name == "M_Glass")
            {
                mat.SetColor("_BaseColor", new Color(0.55f, 0.75f, 0.9f, 0.12f));
                mat.SetFloat("_Smoothness", 0.95f);
                SetTransparent(mat);
                desc = "aucune texture : verre transparent bleuté";
                untextured.Add(name + " (verre transparent au lieu du gris, sinon les fenêtres seraient opaques)");
            }
            else if (NamedColors.TryGetValue(name, out var col))
            {
                mat.SetColor("_BaseColor", col);
                mat.SetFloat("_Smoothness", 0.55f);
                if (name == "M_Alien_Eyes") SetEmission(mat, null, col);
                desc = "aucune texture : couleur unie " + ColorUtility.ToHtmlStringRGB(col);
                untextured.Add(name + " (couleur tirée du nom au lieu du gris)");
            }
            else
            {
                desc = AssignTextures(mat, name, textures, ormCache, log);
                if (desc == null)
                {
                    mat.SetColor("_BaseColor", MidGrey);
                    desc = "aucune texture : gris moyen";
                    untextured.Add(name + " (gris moyen)");
                }
            }

            ApplyKeywords(mat);
            EditorUtility.SetDirty(mat);
            created[name] = mat;
            log.AppendLine($"  {name}  [{kv.Value.Count} FBX]  -> {desc}");
        }

        // 4) Variantes des cellules d'énergie
        if (created.TryGetValue("MI_Props_Batch2", out var batch2))
        {
            var charged = LoadOrCreate("M_Cell_Charged", shader);
            charged.CopyPropertiesFromMaterial(batch2);
            var mask = BuildGreenMask(batch2.GetTexture("_BaseMap") as Texture2D);
            SetEmission(charged, mask, new Color(0.1f, 1f, 0.25f) * 2.5f);
            ApplyKeywords(charged);
            EditorUtility.SetDirty(charged);

            var depleted = LoadOrCreate("M_Cell_Depleted", shader);
            depleted.CopyPropertiesFromMaterial(batch2);
            depleted.SetColor("_BaseColor", new Color(0.75f, 0.55f, 0.52f));
            depleted.SetColor("_EmissionColor", Color.black);
            depleted.SetTexture("_EmissionMap", null);
            depleted.DisableKeyword("_EMISSION");
            ApplyKeywords(depleted);
            depleted.DisableKeyword("_EMISSION");
            depleted.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(depleted);
            // Borne vide : le maillage Prop_HealthPack contient déjà un tube ; on remplace le vert lime du tube par du verre sombre
            var slot = LoadOrCreate("M_EnergySlot_Empty", shader);
            slot.CopyPropertiesFromMaterial(batch2);
            slot.SetTexture("_BaseMap", BuildEmptySlotTexture(batch2.GetTexture("_BaseMap") as Texture2D));
            ApplyKeywords(slot);
            EditorUtility.SetDirty(slot);

            log.AppendLine("  M_EnergySlot_Empty -> copie de MI_Props_Batch2, tube lime remplacé par du verre sombre (borne vide)");
            log.AppendLine("  M_Cell_Charged  -> copie de MI_Props_Batch2, émission verte vive (masque des zones vertes de la texture)");
            log.AppendLine("  M_Cell_Depleted -> copie de MI_Props_Batch2, émission coupée, teinte rouge-gris sombre");
        }
        AssetDatabase.SaveAssets();

        // 5) Remap des matériaux dans tous les FBX, par nom
        int remapped = 0;
        foreach (var p in models)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(p);
            bool changed = false;
            foreach (var n in EmbeddedMaterialNames(p))
            {
                if (!created.TryGetValue(n, out var m)) continue;
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), n);
                var map = imp.GetExternalObjectMap();
                if (map.TryGetValue(id, out var cur) && cur == m) continue;
                imp.RemoveRemap(id);
                imp.AddRemap(id, m);
                changed = true;
            }
            if (changed) { imp.SaveAndReimport(); remapped++; }
        }
        log.AppendLine($"Remap : {remapped} FBX réimportés.");
        log.AppendLine("Matériaux sans texture correspondante : " + (untextured.Count == 0 ? "aucun" : string.Join("; ", untextured)));
        log.AppendLine("Textures ORM converties : " + (ormCache.Count == 0 ? "aucune" : string.Join(", ", ormCache.Keys)));
        StationLog.Write("Materials", log.ToString());
    }

    // ---------- Textures par préfixe ----------

    static string AssignTextures(Material mat, string name, Dictionary<string, string> tex,
        Dictionary<string, (Texture2D, Texture2D)> ormCache, StringBuilder log)
    {
        var core = name.StartsWith("MI_") ? name.Substring(3) : name.StartsWith("M_") ? name.Substring(2) : name;

        // Texture de couleur explicitement nommée (ex. MI_Trim_03_Dark -> T_Trim_03_Dark)
        string explicitBase = null;
        var set = core;
        while (true)
        {
            if (tex.ContainsKey("T_" + set + "_BaseColor")) break;
            if (explicitBase == null && tex.ContainsKey("T_" + set)) explicitBase = tex["T_" + set];
            int i = set.LastIndexOf('_');
            if (i <= 0) { set = null; break; }
            set = set.Substring(0, i);
        }
        if (set == null) return null;

        var parts = new List<string>();
        var basePath = explicitBase ?? tex["T_" + set + "_BaseColor"];
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(basePath));
        mat.SetColor("_BaseColor", core.EndsWith("_Blue") ? new Color(0.55f, 0.7f, 1f) : Color.white);
        parts.Add("_BaseMap=" + Path.GetFileNameWithoutExtension(basePath) + (core.EndsWith("_Blue") ? " (teinte bleue)" : ""));

        if (tex.TryGetValue("T_" + set + "_Normal", out var nrm))
        {
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(nrm));
            mat.SetFloat("_BumpScale", 0.8f);                  // atténue le scintillement spéculaire
            parts.Add("_BumpMap=" + Path.GetFileNameWithoutExtension(nrm));
        }
        if (tex.TryGetValue("T_" + set + "_ORM", out var orm))
        {
            var key = "T_" + set;
            if (!ormCache.TryGetValue(key, out var pair)) ormCache[key] = pair = ConvertOrm(orm, key);
            mat.SetFloat("_WorkflowMode", 1f);
            mat.SetTexture("_MetallicGlossMap", pair.Item1);
            mat.SetFloat("_Smoothness", 0.75f); // multiplicateur quand la map est présente (< 1 : moins de scintillement)
            mat.SetFloat("_SmoothnessTextureChannel", 0f); // alpha de la map métal
            mat.SetTexture("_OcclusionMap", pair.Item2);
            mat.SetFloat("_OcclusionStrength", 1f);
            parts.Add("_MetallicGlossMap=" + pair.Item1.name + ", _OcclusionMap=" + pair.Item2.name);
        }
        else mat.SetFloat("_Smoothness", 0.4f);

        if (tex.TryGetValue("T_" + set + "_Emissive", out var em))
        {
            SetEmission(mat, AssetDatabase.LoadAssetAtPath<Texture2D>(em), Color.white);
            parts.Add("_EmissionMap=" + Path.GetFileNameWithoutExtension(em));
        }
        if (set != core && explicitBase == null) parts.Add("(textures de T_" + set + ")");
        return string.Join(", ", parts);
    }

    // ORM (R=AO, G=rugosité, B=métal) -> _MetallicSmoothness (RGB=métal, A=1-rugosité) + _Occlusion (gris=AO)
    static (Texture2D, Texture2D) ConvertOrm(string ormPath, string key)
    {
        var msPath = $"{GenTexDir}/{key}_MetallicSmoothness.png";
        var occPath = $"{GenTexDir}/{key}_Occlusion.png";
        var ti = (TextureImporter)AssetImporter.GetAtPath(ormPath);
        bool wasReadable = ti.isReadable;
        var oldCompression = ti.textureCompression;
        if (!wasReadable || oldCompression != TextureImporterCompression.Uncompressed)
        {
            ti.isReadable = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
        try
        {
            var src = AssetDatabase.LoadAssetAtPath<Texture2D>(ormPath);
            var px = src.GetPixels32();
            var ms = new Color32[px.Length];
            var occ = new Color32[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                ms[i] = new Color32(c.b, c.b, c.b, (byte)(255 - c.g));
                occ[i] = new Color32(c.r, c.r, c.r, 255);
            }
            WritePng(msPath, src.width, src.height, ms);
            WritePng(occPath, src.width, src.height, occ);
        }
        finally
        {
            ti.isReadable = wasReadable;
            ti.textureCompression = oldCompression;
            ti.SaveAndReimport();
        }
        ConfigureLinear(msPath, true);
        ConfigureLinear(occPath, false);
        return (AssetDatabase.LoadAssetAtPath<Texture2D>(msPath), AssetDatabase.LoadAssetAtPath<Texture2D>(occPath));
    }

    // Masque d'émission des cellules chargées : zones nettement vertes de la texture de base
    static Texture2D BuildGreenMask(Texture2D baseTex)
    {
        var path = $"{GenTexDir}/T_Cell_EmissiveMask.png";
        var srcPath = AssetDatabase.GetAssetPath(baseTex);
        var ti = (TextureImporter)AssetImporter.GetAtPath(srcPath);
        bool wasReadable = ti.isReadable;
        var oldCompression = ti.textureCompression;
        ti.isReadable = true; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.SaveAndReimport();
        try
        {
            var src = AssetDatabase.LoadAssetAtPath<Texture2D>(srcPath);
            var px = src.GetPixels32();
            var o = new Color32[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                bool green = c.g > 120 && c.g > c.r + 30 && c.g > c.b + 60;
                o[i] = green ? c : new Color32(0, 0, 0, 255);
            }
            WritePng(path, src.width, src.height, o);
        }
        finally { ti.isReadable = wasReadable; ti.textureCompression = oldCompression; ti.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Texture de base de la borne vide : pixels vert-jaune (tube) -> gris-bleu sombre
    static Texture2D BuildEmptySlotTexture(Texture2D baseTex)
    {
        var path = $"{GenTexDir}/T_Props_Batch2_BaseColor_SlotEmpty.png";
        var srcPath = AssetDatabase.GetAssetPath(baseTex);
        var ti = (TextureImporter)AssetImporter.GetAtPath(srcPath);
        bool wasReadable = ti.isReadable;
        var oldCompression = ti.textureCompression;
        ti.isReadable = true; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.SaveAndReimport();
        try
        {
            var src = AssetDatabase.LoadAssetAtPath<Texture2D>(srcPath);
            var px = src.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                bool lime = c.g > 140 && c.r > 80 && c.b < 90 && c.g > c.b + 80;
                if (lime) { byte v = (byte)(c.g / 8); px[i] = new Color32(v, (byte)(v + 4), (byte)(v + 10), 255); }
            }
            WritePng(path, src.width, src.height, px);
        }
        finally { ti.isReadable = wasReadable; ti.textureCompression = oldCompression; ti.SaveAndReimport(); }
        var ni = (TextureImporter)AssetImporter.GetAtPath(path);
        ni.sRGBTexture = true; ni.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ---------- Utilitaires ----------

    public static IEnumerable<string> EmbeddedMaterialNames(string modelPath)
    {
        // Matériaux encore embarqués + ceux déjà remappés (le script doit pouvoir être relancé)
        var imp = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        var remapped = imp.GetExternalObjectMap().Keys.Where(k => k.type == typeof(Material)).Select(k => k.name);
        return AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>().Select(m => m.name).Concat(remapped).Distinct();
    }

    static Material LoadOrCreate(string name, Shader shader)
    {
        var path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader) { name = name }; AssetDatabase.CreateAsset(m, path); }
        else m.shader = shader;
        return m;
    }

    static void ResetLit(Material m)
    {
        foreach (var t in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap", "_EmissionMap" }) m.SetTexture(t, null);
        m.SetColor("_BaseColor", Color.white);
        m.SetColor("_EmissionColor", Color.black);
        m.SetFloat("_Surface", 0f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_AlphaClip", 0f);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Smoothness", 0.5f);
        m.SetFloat("_WorkflowMode", 1f);
        m.DisableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
    }

    static void SetEmission(Material m, Texture tex, Color hdr)
    {
        m.SetTexture("_EmissionMap", tex);
        m.SetColor("_EmissionColor", hdr);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
    }

    static void SetTransparent(Material m)
    {
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
    }

    // Recalcule mots-clés, render queue et états de blending comme l'inspecteur URP
    static void ApplyKeywords(Material m)
    {
        bool emissive = m.IsKeywordEnabled("_EMISSION");
        BaseShaderGUI.SetMaterialKeywords(m, LitGUI.SetMaterialKeywords);
        if (emissive) m.EnableKeyword("_EMISSION");
    }

    static void WritePng(string assetPath, int w, int h, Color32[] px)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
        t.SetPixels32(px);
        t.Apply();
        File.WriteAllBytes(Path.GetFullPath(assetPath), t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
    }

    static void ConfigureLinear(string path, bool alpha)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.sRGBTexture = false;
        ti.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        ti.mipmapEnabled = true;
        ti.filterMode = FilterMode.Trilinear;
        ti.anisoLevel = 8;
        ti.SaveAndReimport();
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}

// Journal : console Unity + fichier Logs/Station_<nom>.txt (dossier Logs/ ignoré par Git)
public static class StationLog
{
    public static void Write(string name, string text)
    {
        Debug.Log(text);
        Directory.CreateDirectory("Logs");
        File.WriteAllText($"Logs/Station_{name}.txt", text);
    }
}
