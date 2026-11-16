// SPT-Stalker: builds EFT item prefabs from prepared models, following the WTT
// "Intro to Static Objects" guide (sections 4-6), for every model at once.
//
// Setup (in the Escape From Tarkov SDK Unity project):
//   1. Copy this file to   Assets/Editor/SptStalkerItemPrefabs.cs
//   2. Copy each folder from the repo's art/artifacts/unity/<key>/ to   Assets/SPTStalker/Models/<key>/
//      (needs <key>.fbx and <key>_diffuse.png; <key>_normal.png is optional)
//   3. Menu:  SPT-Stalker > Build Item Prefabs
//   4. Per the guide: Window > Item Preview to set each PreviewPivot rotation + Render Icon,
//      then Window > Asset Bundle Browser > Build.  Wait for Unity to become fully responsive.
//
// For every model it: creates a material with Tarkov's "Bumped Specular SMap" shader and the guide's
// baseline values, builds  item_sptstalker_<key>  (empty root at 0,0,0 > model child with a convex
// MeshCollider, PreviewPivot on the root), saves it to Assets/SPTStalker/Prefabs/ and sets its
// AssetBundle label to  item_sptstalker_<key>.bundle  (prefab name + .bundle, as in the guide).

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SptStalkerItemPrefabs
{
    private const string ModelsFolder = "Assets/SPTStalker/Models";
    private const string MaterialsFolder = "Assets/SPTStalker/Materials";
    private const string PrefabsFolder = "Assets/SPTStalker/Prefabs";

    [MenuItem("SPT-Stalker/Build Item Prefabs")]
    public static void BuildAll()
    {
        if (!AssetDatabase.IsValidFolder(ModelsFolder))
        {
            EditorUtility.DisplayDialog("SPT-Stalker", "Put the prepared models in " + ModelsFolder + "/<key>/<key>.fbx first.", "OK");
            return;
        }

        EnsureFolder(MaterialsFolder);
        EnsureFolder(PrefabsFolder);

        var shader = FindTarkovShader();
        if (shader == null)
        {
            Debug.LogWarning("[SPT-Stalker] 'Bumped Specular SMap' shader not found in this project, using Standard. Assign the Tarkov shader manually (guide section 4.3).");
            shader = Shader.Find("Standard");
        }
        else
        {
            LogShaderProperties(shader);
        }

        var pivotType = FindType("PreviewPivot");
        if (pivotType == null)
        {
            Debug.LogWarning("[SPT-Stalker] PreviewPivot script not found. Is this the Escape From Tarkov SDK project? Add it manually to each prefab root.");
        }

        var built = 0;
        foreach (var dir in Directory.GetDirectories(ModelsFolder))
        {
            var key = Path.GetFileName(dir);
            var modelPath = new[] { ".fbx", ".blend", ".obj" }
                .Select(ext => dir.Replace('\\', '/') + "/" + key + ext)
                .FirstOrDefault(p => AssetDatabase.LoadAssetAtPath<GameObject>(p) != null);
            if (modelPath == null)
            {
                Debug.LogWarning("[SPT-Stalker] No model found for " + key);
                continue;
            }

            try
            {
                BuildOne(key, dir.Replace('\\', '/'), modelPath, shader, pivotType);
                built++;
            }
            catch (Exception e)
            {
                Debug.LogError("[SPT-Stalker] " + key + ": " + e);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SPT-Stalker] Built " + built + " prefabs. Next: Window > Item Preview (pivot + icon), then Asset Bundle Browser > Build.");
    }

    private static void BuildOne(string key, string dir, string modelPath, Shader shader, Type pivotType)
    {
        var prefabName = "item_sptstalker_" + key;

        // --- material (guide 4.3)
        var matPath = MaterialsFolder + "/" + key + "_mat.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, matPath);
        }

        mat.shader = shader;
        var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/" + key + "_diffuse.png");
        var normalPath = dir + "/" + key + "_normal.png";
        if (File.Exists(normalPath))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(normalPath);
            if (importer != null && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
        }

        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        SetTexture(mat, diffuse, "_MainTex");
        SetTexture(mat, normal, "_BumpMap");
        // Guide baseline: Main Color white, Specularness 0.35, Glossiness 1, Reflection colour (almost) black
        SetColor(mat, Color.white, "_Color");
        SetColor(mat, new Color(0.35f, 0.35f, 0.35f, 1f), "_SpecColor");
        SetFloat(mat, 1f, "_Glossiness", "_Shininess");
        SetColor(mat, new Color(0.02f, 0.02f, 0.02f, 1f), "_ReflectColor");
        EditorUtility.SetDirty(mat);

        // --- hierarchy (guide 5): empty root at 0,0,0 > model child
        var root = new GameObject(prefabName);
        root.transform.position = Vector3.zero;

        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        model.transform.SetParent(root.transform, false);
        model.transform.localPosition = Vector3.zero;

        foreach (var renderer in model.GetComponentsInChildren<Renderer>())
        {
            renderer.sharedMaterials = Enumerable.Repeat(mat, renderer.sharedMaterials.Length).ToArray();
        }

        // Convex mesh collider on the child mesh(es) so the item doesn't fall through the ground
        foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
        {
            var collider = filter.gameObject.GetComponent<MeshCollider>();
            if (collider == null)
            {
                collider = filter.gameObject.AddComponent<MeshCollider>();
            }

            collider.sharedMesh = filter.sharedMesh;
            collider.convex = true;
        }

        if (pivotType != null && root.GetComponent(pivotType) == null)
        {
            root.AddComponent(pivotType);
        }

        // --- prefab + AssetBundle label (guide 6)
        var prefabPath = PrefabsFolder + "/" + prefabName + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        UnityEngine.Object.DestroyImmediate(root);

        var prefabImporter = AssetImporter.GetAtPath(prefabPath);
        prefabImporter.assetBundleName = prefabName + ".bundle";
        prefabImporter.SaveAndReimport();

        Debug.Log("[SPT-Stalker] " + prefabPath + "  ->  bundle " + prefabName + ".bundle");
    }

    private static Shader FindTarkovShader()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Shader"))
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
            if (shader != null && shader.name.Replace(" ", "").IndexOf("BumpedSpecularSMap", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return shader;
            }
        }

        return null;
    }

    private static void LogShaderProperties(Shader shader)
    {
        var names = Enumerable.Range(0, ShaderUtil.GetPropertyCount(shader)).Select(i => ShaderUtil.GetPropertyName(shader, i));
        Debug.Log("[SPT-Stalker] Using shader '" + shader.name + "'. Properties: " + string.Join(", ", names.ToArray()));
    }

    private static Type FindType(string name)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
            .FirstOrDefault(t => t.Name == name && typeof(Component).IsAssignableFrom(t));
    }

    private static void SetTexture(Material mat, Texture tex, string property)
    {
        if (tex != null && mat.HasProperty(property)) mat.SetTexture(property, tex);
    }

    private static void SetColor(Material mat, Color color, string property)
    {
        if (mat.HasProperty(property)) mat.SetColor(property, color);
    }

    private static void SetFloat(Material mat, float value, params string[] properties)
    {
        foreach (var name in properties.Where(n => mat.HasProperty(n))) mat.SetFloat(name, value);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
