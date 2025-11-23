using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace TABMM
{
    /// <summary>
    /// Stop the compiler from compiling this class into the mod.
    /// </summary>
    public class ExcludeCompilationAttribute : Attribute
    {

    }

    [AddComponentMenu("TABMM/Asset Debundler"), ExcludeCompilation]
    public class ModBundleDecompiler : MonoBehaviour
    {
        GameObject mods;
        public List<GameObject> allMods = new List<GameObject>();
        public Shader defaultShader;

        Dictionary<string, Shader> loadedShaders = new Dictionary<string, Shader>();
        Dictionary<string, MaterialPropertyData> shaderProperties = new Dictionary<string, MaterialPropertyData>();

        IEnumerator LoadShaders(string modFolder)
        {
            string shadersDir = Path.Combine(modFolder, "Shaders");

            if (!Directory.Exists(shadersDir))
                yield break;

            string jsonPath = Path.Combine(shadersDir, "shader_data.json");
            if (!File.Exists(jsonPath))
                yield break;

            string json = File.ReadAllText(jsonPath);
            ShaderDataContainer data = JsonUtility.FromJson<ShaderDataContainer>(json);

            foreach (var shaderData in data.shaders)
            {
                string shaderPath = Path.Combine(shadersDir, shaderData.shaderName);
                if (!File.Exists(shaderPath))
                {
                    Debug.LogWarning($"Shader file not found: {shaderData.shaderName}");
                    continue;
                }

                Shader shader = Shader.Find(shaderData.originalName);
                
                if (shader == null)
                {
                    if (shaderData.isShaderGraph)
                    {
                        string[] possibleNames = new string[]
                        {
                            shaderData.originalName,
                            "Shader Graphs/" + shaderData.originalName.Split('/').Last(),
                            shaderData.originalName.Split('/').Last(),
                            "Hidden/" + shaderData.originalName
                        };

                        foreach (var name in possibleNames)
                        {
                            shader = Shader.Find(name);
                            if (shader != null)
                            {
                                Debug.Log($"Found Shader Graph with alternate name: {name}");
                                break;
                            }
                        }
                    }
                    
                    if (shader == null)
                    {
                        Debug.LogWarning($"Could not load shader: {shaderData.originalName}, using default");
                        shader = defaultShader;
                    }
                }
                else
                {
                    Debug.Log($"Loaded shader: {shaderData.originalName}");
                }

                if (shader != null)
                {
                    loadedShaders[shaderData.originalName] = shader;
                    
                    if (shaderData.isShaderGraph)
                    {
                        string propFileName = shaderData.shaderName.Replace(".shadergraph", "_properties.json");
                        string propPath = Path.Combine(shadersDir, propFileName);
                        
                        if (File.Exists(propPath))
                        {
                            string propJson = File.ReadAllText(propPath);
                            MaterialPropertyData propData = JsonUtility.FromJson<MaterialPropertyData>(propJson);
                            
                            if (!shaderProperties.ContainsKey(shaderData.originalName))
                            {
                                shaderProperties[shaderData.originalName] = propData;
                            }
                        }
                    }
                }
            }

            yield return null;
        }

        IEnumerator LoadScripts(string modFolder)
        {
            string scriptsDir = Path.Combine(modFolder, "Scripts");

            if (!Directory.Exists(scriptsDir))
                yield break;

            string jsonPath = Path.Combine(scriptsDir, "script_data.json");
            if (!File.Exists(jsonPath))
                yield break;

            string json = File.ReadAllText(jsonPath);
            ScriptDataContainer data = JsonUtility.FromJson<ScriptDataContainer>(json);

            foreach (var scriptData in data.scripts)
            {
                string scriptPath = Path.Combine(scriptsDir, scriptData.scriptName);
                if (!File.Exists(scriptPath))
                {
                    Debug.LogWarning($"Script file not found: {scriptData.scriptName}");
                    continue;
                }

                string scriptCode = File.ReadAllText(scriptPath);
                Debug.Log($"Found script: {scriptData.scriptName}");
                
                // Note: Runtime script compilation is complex and platform-dependent
                // You might need a runtime compilation solution or pre-compile scripts into assemblies
                // For Android builds, you'll need to include compiled DLLs instead
            }

            yield return null;
        }

        IEnumerator LoadLightmaps(string modFolder, Transform parent)
        {
            string lightmapDir = Path.Combine(modFolder, "Lightmaps");

            if (!Directory.Exists(lightmapDir))
                yield break;

            string jsonPath = Path.Combine(lightmapDir, "lightmap_data.json");
            if (!File.Exists(jsonPath))
                yield break;

            string[] colorMaps = Directory.GetFiles(lightmapDir, "*_color.png");
            List<LightmapData> lightmapList = new List<LightmapData>();

            foreach (string colorPath in colorMaps)
            {
                Texture2D colorTex = new Texture2D(2, 2);
                colorTex.LoadImage(File.ReadAllBytes(colorPath));

                string dirPath = colorPath.Replace("_color.png", "_dir.png");
                Texture2D dirTex = null;

                if (File.Exists(dirPath))
                {
                    dirTex = new Texture2D(2, 2);
                    dirTex.LoadImage(File.ReadAllBytes(dirPath));
                }

                lightmapList.Add(new LightmapData
                {
                    lightmapColor = colorTex,
                    lightmapDir = dirTex
                });
            }

            LightmapSettings.lightmaps = lightmapList.ToArray();

            string json = File.ReadAllText(jsonPath);
            LightmapDataContainer data = JsonUtility.FromJson<LightmapDataContainer>(json);

            foreach (var rendererData in data.renderers)
            {
                // Strip the root object name from the path since it changes when loaded as a mod
                string[] pathParts = rendererData.objectPath.Split('/');

                // Skip the first part (original root object name) and search from parent
                string relPath = pathParts.Length > 1 ?
                    string.Join("/", pathParts, 1, pathParts.Length - 1) :
                    pathParts[0];

                Transform found = FindChildRecursive(parent, relPath);

                // If not found with relative path, try searching by the object name alone
                if (found == null)
                {
                    string objectName = pathParts[pathParts.Length - 1];
                    MeshRenderer[] allRenderers = parent.GetComponentsInChildren<MeshRenderer>(true);
                    foreach (var r in allRenderers)
                    {
                        if (r.gameObject.name == objectName)
                        {
                            found = r.transform;
                            Debug.Log($"Found by name search: {objectName}");
                            break;
                        }
                    }
                }

                if (found != null)
                {
                    MeshRenderer renderer = found.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        renderer.lightmapIndex = rendererData.lightmapIndex;
                        renderer.lightmapScaleOffset = rendererData.lightmapScaleOffset;
                        Debug.Log($"Applied lightmap to: {rendererData.objectPath}");
                    }
                }
                else
                {
                    Debug.LogWarning($"Could not find object: {rendererData.objectPath} (searched as: {relPath})");
                }
            }

            yield return null;
        }

        void ApplyLoadedShaders(Transform parent)
        {
            if (loadedShaders.Count == 0)
                return;

            Renderer[] allRenderers = parent.GetComponentsInChildren<Renderer>(true);
            
            foreach (var renderer in allRenderers)
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null) continue;

                    string shaderName = material.shader != null ? material.shader.name : "";
                    
                    if (loadedShaders.ContainsKey(shaderName))
                    {
                        material.shader = loadedShaders[shaderName];
                        Debug.Log($"Applied custom shader {shaderName} to material {material.name}");
                        
                        // Apply saved properties if available (for Shader Graph)
                        if (shaderProperties.ContainsKey(shaderName))
                        {
                            ApplyMaterialProperties(material, shaderProperties[shaderName]);
                        }
                    }
                }
            }
        }

        void ApplyMaterialProperties(Material material, MaterialPropertyData propData)
        {
            foreach (var prop in propData.properties)
            {
                try
                {
                    switch (prop.type)
                    {
                        case "Color":
                            if (prop.colorValue != null && prop.colorValue.Length == 4)
                            {
                                material.SetColor(prop.name, new Color(
                                    prop.colorValue[0],
                                    prop.colorValue[1],
                                    prop.colorValue[2],
                                    prop.colorValue[3]
                                ));
                            }
                            break;
                        case "Vector":
                            if (prop.vectorValue != null && prop.vectorValue.Length == 4)
                            {
                                material.SetVector(prop.name, new Vector4(
                                    prop.vectorValue[0],
                                    prop.vectorValue[1],
                                    prop.vectorValue[2],
                                    prop.vectorValue[3]
                                ));
                            }
                            break;
                        case "Float":
                        case "Range":
                            material.SetFloat(prop.name, prop.floatValue);
                            break;
                        case "TexEnv":
                            // Texture references need to be handled separately
                            // as they require the texture to be loaded
                            if (prop.textureOffset != null && prop.textureOffset.Length == 2)
                            {
                                material.SetTextureOffset(prop.name, new Vector2(
                                    prop.textureOffset[0],
                                    prop.textureOffset[1]
                                ));
                            }
                            if (prop.textureScale != null && prop.textureScale.Length == 2)
                            {
                                material.SetTextureScale(prop.name, new Vector2(
                                    prop.textureScale[0],
                                    prop.textureScale[1]
                                ));
                            }
                            break;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to apply property {prop.name}: {e.Message}");
                }
            }
            
            Debug.Log($"Applied material properties to {material.name}");
        }

        Transform FindChildRecursive(Transform parent, string path)
        {
            string[] pathParts = path.Split('/');

            Transform current = parent;

            foreach (string part in pathParts)
            {
                Transform found = null;

                foreach (Transform child in current)
                {
                    if (child.name == part)
                    {
                        found = child;
                        break;
                    }
                }

                if (found == null)
                    return null;

                current = found;
            }

            return current;
        }

        public IEnumerator Start()
        {
            mods = gameObject;
#if UNITY_ANDROID && !UNITY_EDITOR
            string targetDest = Path.Combine(Application.persistentDataPath, "TABMM");
#else
            string targetDest = $"C:/{Application.productName}/ModdingSDK/";
#endif
            string baseModPath = Path.Combine(targetDest, "Mods");
            string tempModPath = Path.Combine(targetDest, "Temp");

            if (!Directory.Exists(baseModPath))
            {
                Directory.CreateDirectory(baseModPath);
                yield break;
            }

            if (!Directory.Exists(tempModPath))
                Directory.CreateDirectory(tempModPath);

            string[] modFiles = Directory.GetFiles(baseModPath, "*." + BundlerInfo.extension);
            if (modFiles.Length == 0)
            {
                Debug.LogWarning("No mods found.");
                yield break;
            }

            foreach (var mod in modFiles)
            {
                var stuff = new GameObject(Path.GetFileNameWithoutExtension(mod));

                string cleanZipPath = Path.Combine(tempModPath, Path.GetFileNameWithoutExtension(mod) + "_clean." + BundlerInfo.extension);
                File.Copy(mod, cleanZipPath, overwrite: true);

                string export = Path.Combine(tempModPath, Path.GetFileNameWithoutExtension(mod));

                if (Directory.Exists(export))
                    Directory.Delete(export, true);


                ReverseFile(cleanZipPath);

                ZipFile.ExtractToDirectory(cleanZipPath, export);

                yield return StartCoroutine(LoadShaders(export));

                yield return StartCoroutine(LoadBundle(export, stuff.transform));

                ApplyLoadedShaders(stuff.transform);

                Directory.Delete(export, true);
                File.Delete(cleanZipPath);
                stuff.transform.localPosition = Vector3.zero;
                stuff.SetActive(false);
                allMods.Add(stuff);
            }
        }

        static void ReverseFile(string filePath)
        {
            byte[] data = File.ReadAllBytes(filePath);
            Array.Reverse(data);
            File.WriteAllBytes(filePath, data);
        }

        IEnumerator LoadBundle(string modFolder, Transform parent)
        {
            string[] bundleFiles = Directory.GetFiles(modFolder, "*.bundle", SearchOption.TopDirectoryOnly);

            List<AssetBundle> loadedBundles = new List<AssetBundle>();

            foreach (var file in bundleFiles)
            {
                var bundle = AssetBundle.LoadFromFile(file);
                if (bundle == null)
                {
                    Debug.LogError($"Failed to load bundle: {file}");
                    continue;
                }

                loadedBundles.Add(bundle);
                Debug.Log($"Loaded bundle: {Path.GetFileName(file)}");
            }

            List<GameObject> objs = new List<GameObject>();

            foreach (var bundle in loadedBundles)
            {
                try
                {
                    foreach (var assetName in bundle.GetAllAssetNames())
                    {
                        UnityEngine.Object asset = bundle.LoadAsset(assetName);
                        if (asset is GameObject go)
                        {
                            var obj = Instantiate(go);
                            obj.transform.name = obj.transform.name.Replace("(Clone)", "");
                            string[] disallowedKeywords =
                            {
                                "File",
                                "FileStream"
                            };
                            // Replace this section in your LoadBundle method:
                            // FROM:
                            //     foreach (var sc in obj.GetComponentsInChildren<MonoBehaviour>())
                            //     {
                            //         if (sc.GetType().GetCustomAttributes(typeof(ExcludeCompilationAttribute), true).Length > 0)
                            //             DestroyImmediate(sc);
                            //     }
                            //
                            // TO:

                            foreach (var sc in obj.GetComponentsInChildren<MonoBehaviour>())
                            {
                                if (sc == null) continue;

                                var componentType = sc.GetType();
                                string typeName = componentType.FullName ?? componentType.Name;
                                string namespaceName = componentType.Namespace ?? "";

                                bool shouldRemove = false;

                                // Check for ExcludeCompilation attribute (your original check)
                                if (componentType.GetCustomAttributes(typeof(ExcludeCompilationAttribute), true).Length > 0)
                                {
                                    Debug.LogWarning($"Removing component with ExcludeCompilation: {typeName}");
                                    shouldRemove = true;
                                }

                                // Check for dangerous namespaces
                                if (!shouldRemove && !string.IsNullOrEmpty(namespaceName))
                                {
                                    string[] disallowedNamespaces = new string[]
                                    {
            "System.IO",
            "System.Net",
            "System.Reflection",
            "System.Diagnostics",
            "System.Security",
            "System.Threading",
            "System.Runtime.InteropServices"
                                    };

                                    foreach (var ns in disallowedNamespaces)
                                    {
                                        if (namespaceName.StartsWith(ns))
                                        {
                                            Debug.LogError($"Removing component from disallowed namespace: {typeName} ({namespaceName})");
                                            shouldRemove = true;
                                            break;
                                        }
                                    }
                                }

                                if (!shouldRemove)
                                {
                                    string[] disallowedTypeKeywords = new string[]
                                    {
            "File",
            "FileStream",
            "Directory",
            "Process",
            "Socket",
            "WebClient",
            "HttpClient",
            "NetworkStream",
            "TcpClient",
            "UdpClient"
                                    };

                                    foreach (var keyword in disallowedTypeKeywords)
                                    {
                                        if (typeName.Contains(keyword))
                                        {
                                            Debug.LogError($"Removing component with disallowed type keyword: {typeName}");
                                            shouldRemove = true;
                                            break;
                                        }
                                    }
                                }

                                if (!shouldRemove)
                                {
                                    try
                                    {
                                        var fields = componentType.GetFields(
                                            System.Reflection.BindingFlags.Public |
                                            System.Reflection.BindingFlags.NonPublic |
                                            System.Reflection.BindingFlags.Instance
                                        );

                                        foreach (var field in fields)
                                        {
                                            string fieldTypeName = field.FieldType.FullName ?? field.FieldType.Name;
                                            string fieldNamespace = field.FieldType.Namespace ?? "";

                                            if (fieldNamespace.StartsWith("System.IO") ||
                                                fieldNamespace.StartsWith("System.Net") ||
                                                fieldNamespace.StartsWith("System.Diagnostics"))
                                            {
                                                Debug.LogError($"Component {typeName} has dangerous field: {field.Name} of type {fieldTypeName}");
                                                shouldRemove = true;
                                                break;
                                            }

                                            if (fieldTypeName.Contains("FileStream") ||
                                                fieldTypeName.Contains("Process") ||
                                                fieldTypeName.Contains("Socket") ||
                                                fieldTypeName.Contains("WebClient"))
                                            {
                                                Debug.LogError($"Component {typeName} has dangerous field: {field.Name} of type {fieldTypeName}");
                                                shouldRemove = true;
                                                break;
                                            }
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        Debug.LogWarning($"Error checking fields for {typeName}: {e.Message}");
                                    }
                                }

                                if (shouldRemove)
                                {
                                    DestroyImmediate(sc);
                                }
                            }
                            objs.Add(obj);
                        }
                    }
                }
                catch
                {
                    continue;
                }

                bundle.Unload(false);
            }

            foreach (var obj in objs)
                if (obj.transform.parent == null)
                {
                    obj.transform.parent = parent;
                    foreach (var rend in obj.GetComponentsInChildren<Renderer>())
                        foreach (var mat in rend.sharedMaterials)
                            if (mat && !mat.shader)
                                mat.shader = defaultShader;
                }

            yield return StartCoroutine(LoadLightmaps(modFolder, parent));
            yield return null;
        }
    }

}

public class BundlerInfo
{
    public readonly static string extension = "tmod";
    public readonly static string uncompileExtension = "readable";
}

[System.Serializable]
public class LightmapRendererData
{
    public string objectPath;
    public int lightmapIndex;
    public Vector4 lightmapScaleOffset;
}

[System.Serializable]
public class LightmapDataContainer
{
    public List<LightmapRendererData> renderers;
}

[System.Serializable]
public class ScriptData
{
    public string scriptName;
    public string className;
}

[System.Serializable]
public class ScriptDataContainer
{
    public List<ScriptData> scripts;
}

[System.Serializable]
public class ShaderData
{
    public string shaderName;
    public string originalName;
    public bool isShaderGraph;
    public string shaderGraphPath;
}

[System.Serializable]
public class ShaderDataContainer
{
    public List<ShaderData> shaders;
}

[System.Serializable]
public class MaterialProperty
{
    public string name;
    public string type;
    public float floatValue;
    public float[] colorValue;
    public float[] vectorValue;
    public string textureName;
    public float[] textureOffset;
    public float[] textureScale;
}

[System.Serializable]
public class MaterialPropertyData
{
    public string materialName;
    public string shaderName;
    public List<MaterialProperty> properties;
}