using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using System.Text.RegularExpressions;



#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

namespace TABMM
{
    [InitializeOnLoad]
    public static class MapImportantizationer
    {
        static MapImportantizationer()
        {
            new StyleHierarchy("Assets/TABMM/ModdingSDK v2/new.png", new Color(0.5f, 0.25f, 0.25f), Color.white, "Descriptor", true);
        }
    }

    [CustomEditor(typeof(MapDescriptor))]
    public class ModMappingEditor : Editor
    {
        public void DrawInspectorExcept(string fieldToSkip)
        {
            DrawInspectorExcept(new string[1] { fieldToSkip });
        }

        public void DrawInspectorExcept(string[] fieldsToSkip)
        {
            serializedObject.Update();
            SerializedProperty prop = serializedObject.GetIterator();
            if (prop.NextVisible(true))
            {
                do
                {
                    if (fieldsToSkip.Any(prop.name.Contains))
                        continue;

                    EditorGUILayout.PropertyField(serializedObject.FindProperty(prop.name), true);
                }
                while (prop.NextVisible(false));
            }
            serializedObject.ApplyModifiedProperties();
        }

        public override void OnInspectorGUI()
        {
            DrawInspectorExcept("m_Script");

            MapDescriptor script = (MapDescriptor)target;

            if (GUILayout.Button("Compile"))
            {
                script.BuildCurrentSceneBundle();
            }
        }
    }


    public class ScriptSecurityChecker
    {
        private static readonly HashSet<string> DisallowedNamespaces = new HashSet<string>
        {
            "System.IO",
            "System.Net",
            "System.Reflection",
            "System.Diagnostics",
            "System.Threading",
            "System.Runtime.InteropServices",
            "System.Security",
            "System.CodeDom",
            "System.Xml.Serialization",
            "Microsoft.CSharp",
            "System.Runtime.CompilerServices",
            "System.AppDomain"
        };

        private static readonly HashSet<string> DisallowedTypes = new HashSet<string>
        {
            "File",
            "FileStream",
            "FileInfo",
            "Directory",
            "DirectoryInfo",
            "Path",
            "StreamWriter",
            "StreamReader",
            "BinaryWriter",
            "BinaryReader",
            "FileSystemWatcher",
            "DriveInfo",
            "Process",
            "ProcessStartInfo",
            "Thread",
            "Task",
            "WebClient",
            "HttpClient",
            "Socket",
            "TcpClient",
            "UdpClient",
            "Assembly",
            "Type.GetType",
            "Activator.CreateInstance",
            "AppDomain",
            "DllImport",
            "UnmanagedCode",
            "Registry",
            "RegistryKey",
            "Environment.Exit",
            "Application.Quit"
        };

        private static readonly string[] DisallowedPatterns = new string[]
        {
            @"\bextern\b",                    // External methods
            @"\bunsafe\b",                    // Unsafe code blocks
            @"DllImport",                     // P/Invoke
            @"Marshal\.",                     // Marshalling
            @"GCHandle",                      // GC manipulation
            @"Pointer",                       // Pointer operations
            @"fixed\s*\(",                    // Fixed statement (unsafe)
            @"stackalloc",                    // Stack allocation
            @"System\.Runtime\.CompilerServices",
            @"__makeref",                     // TypedReference operations
            @"__reftype",
            @"__refvalue",
            @"Activator\.CreateInstance",     // Dynamic type creation
            @"Assembly\.Load",                // Assembly loading
            @"Type\.GetType",                 // Reflection type lookup
            @"\.Invoke\(",                    // Method invocation via reflection
            @"\.GetMethod\(",                 // Reflection method lookup
            @"\.GetField\(",                  // Reflection field access
            @"\.GetProperty\(",               // Reflection property access
            @"System\.CodeDom",               // Code compilation
            @"CSharpCodeProvider",            // Runtime compilation
            @"CompileAssembly",               // Compilation
            @"Environment\.Exit",             // Application exit
            @"Application\.Quit",             // Unity quit
            @"System\.Diagnostics\.Process",  // Process execution
            @"\.Start\(\)",                   // Process start
            @"cmd\.exe",                      // Command execution
            @"powershell",                    // PowerShell
            @"/bin/",                         // Unix binaries
            @"PlayerPrefs\.DeleteAll",        // Destructive operations
            @"Resources\.UnloadUnusedAssets", // Potential disruption
            @"System\.GC\.Collect",           // GC manipulation (excessive calls)
        };

        public static bool IsScriptSafe(string scriptContent, out List<string> violations)
        {
            violations = new List<string>();

            CheckUsingStatements(scriptContent, violations);
            CheckDisallowedTypes(scriptContent, violations);
            CheckDisallowedPatterns(scriptContent, violations);
            CheckObfuscation(scriptContent, violations);

            return violations.Count == 0;
        }

        private static void CheckUsingStatements(string content, List<string> violations)
        {
            var usingMatches = Regex.Matches(content, @"using\s+([\w\.]+)\s*;");

            foreach (Match match in usingMatches)
            {
                string namespaceName = match.Groups[1].Value;

                foreach (var disallowed in DisallowedNamespaces)
                {
                    if (namespaceName == disallowed || namespaceName.StartsWith(disallowed + "."))
                    {
                        violations.Add($"Disallowed namespace: {namespaceName}");
                    }
                }
            }
        }

        private static void CheckDisallowedTypes(string content, List<string> violations)
        {
            foreach (var type in DisallowedTypes)
            {
                string pattern = @"\b" + Regex.Escape(type) + @"\b";

                if (Regex.IsMatch(content, pattern))
                {
                    violations.Add($"Disallowed type found: {type}");
                }
            }
        }

        private static void CheckDisallowedPatterns(string content, List<string> violations)
        {
            foreach (var pattern in DisallowedPatterns)
            {
                if (Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase))
                {
                    violations.Add($"Disallowed pattern detected: {pattern}");
                }
            }
        }

        private static void CheckObfuscation(string content, List<string> violations)
        {
            var base64Pattern = @"""[A-Za-z0-9+/]{50,}={0,2}""";
            if (Regex.IsMatch(content, base64Pattern))
            {
                violations.Add("Potential obfuscation detected: suspicious base64 string");
            }

            var concatPattern = @"(\+\s*""[^""]*""\s*){10,}";
            if (Regex.IsMatch(content, concatPattern))
            {
                violations.Add("Potential obfuscation detected: excessive string concatenation");
            }

            var hexPattern = @"0x[0-9A-Fa-f]{2}(\s*,\s*0x[0-9A-Fa-f]{2}){20,}";
            if (Regex.IsMatch(content, hexPattern))
            {
                violations.Add("Potential obfuscation detected: suspicious hex array");
            }
        }

        public static void LogViolations(string scriptName, List<string> violations)
        {
            Debug.LogError($"Script '{scriptName}' failed security check:");
            foreach (var violation in violations)
            {
                Debug.LogError($"  - {violation}");
            }
        }
    }

    [AddComponentMenu("TABMM/Map Descriptor"), ExcludeCompilation]
    public class MapDescriptor : MonoBehaviour
    {
        public string mapName = DateTime.Now.ToString("yyyyMMddHHmmss");
		
        void SaveScripts(string modOutputDir)
        {
            var scene = EditorSceneManager.GetActiveScene();
            var allObjects = scene.GetRootGameObjects();

            HashSet<MonoScript> collectedScripts = new HashSet<MonoScript>();

            foreach (var rootObj in allObjects)
            {
                MonoBehaviour[] scripts = rootObj.GetComponentsInChildren<MonoBehaviour>(true);
                foreach (var script in scripts)
                {
                    if (script == null) continue;

                    MonoScript monoScript = MonoScript.FromMonoBehaviour(script);
                    Type type = monoScript.GetClass();
                    if (type != null && type.GetCustomAttributes(typeof(ExcludeCompilationAttribute), true).Length > 0)
                    {
                        Debug.Log($"{monoScript.name} is marked with ExcludeCompilation, skipping...");
                        continue;
                    }

                    if (!ScriptSecurityChecker.IsScriptSafe(monoScript.text, out List<string> violations))
                    {
                        Debug.LogWarning($"{monoScript.name} failed security check.");
                        Debug.LogWarning($"{monoScript.name} violates the following: \n" + string.Join(";\n ", violations));
                        continue;
                    }

                    collectedScripts.Add(monoScript);
                }
            }

            if (collectedScripts.Count == 0)
            {
                Debug.Log("No scripts found in scene.");
                return;
            }

            string scriptsDir = Path.Combine(modOutputDir, "Scripts");
            if (!Directory.Exists(scriptsDir))
                Directory.CreateDirectory(scriptsDir);

            List<ScriptData> scriptDataList = new List<ScriptData>();

            foreach (var monoScript in collectedScripts)
            {
                string assetPath = AssetDatabase.GetAssetPath(monoScript);
                if (string.IsNullOrEmpty(assetPath)) continue;

                string scriptContent = File.ReadAllText(assetPath);
                string scriptName = monoScript.name + ".cs";
                string scriptPath = Path.Combine(scriptsDir, scriptName);

                File.WriteAllText(scriptPath, scriptContent);

                scriptDataList.Add(new ScriptData
                {
                    scriptName = scriptName,
                    className = monoScript.GetClass()?.FullName ?? monoScript.name
                });

                Debug.Log($"Saved script: {scriptName}");
            }

            string jsonPath = Path.Combine(scriptsDir, "script_data.json");
            File.WriteAllText(jsonPath, JsonUtility.ToJson(new ScriptDataContainer { scripts = scriptDataList }, true));
        }

        void SaveShaders(string modOutputDir)
        {
            var scene = EditorSceneManager.GetActiveScene();
            var allObjects = scene.GetRootGameObjects();

            HashSet<Shader> collectedShaders = new HashSet<Shader>();
            Dictionary<Shader, Material> shaderToMaterial = new Dictionary<Shader, Material>();

            foreach (var rootObj in allObjects)
            {
                Renderer[] renderers = rootObj.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in renderers)
                {
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material != null && material.shader != null)
                        {
                            if (!collectedShaders.Contains(material.shader))
                            {
                                collectedShaders.Add(material.shader);
                                shaderToMaterial[material.shader] = material;
                            }
                        }
                    }
                }
            }

            if (collectedShaders.Count == 0)
            {
                Debug.Log("No custom shaders found in scene.");
                return;
            }

            string shadersDir = Path.Combine(modOutputDir, "Shaders");
            if (!Directory.Exists(shadersDir))
                Directory.CreateDirectory(shadersDir);

            List<ShaderData> shaderDataList = new List<ShaderData>();

            foreach (var shader in collectedShaders)
            {
                string assetPath = AssetDatabase.GetAssetPath(shader);

                if (string.IsNullOrEmpty(assetPath) || assetPath.StartsWith("Resources/unity_builtin_extra"))
                {
                    Debug.Log($"Skipping built-in shader: {shader.name}");
                    continue;
                }

                bool isShaderGraph = assetPath.EndsWith(".shadergraph");
                string shaderName = shader.name.Replace("/", "_");
                string extension = isShaderGraph ? ".shadergraph" : ".shader";
                string fileName = shaderName + extension;
                string shaderPath = Path.Combine(shadersDir, fileName);

                if (isShaderGraph)
                {
                    if (File.Exists(assetPath))
                    {
                        File.Copy(assetPath, shaderPath, true);

                        shaderDataList.Add(new ShaderData
                        {
                            shaderName = fileName,
                            originalName = shader.name,
                            isShaderGraph = true,
                            shaderGraphPath = assetPath
                        });

                        Debug.Log($"Saved Shader Graph: {shader.name}");
                    }
                    else
                    {
                        Debug.LogWarning($"Could not find Shader Graph file for: {shader.name}");
                    }
                }
				
                else if (File.Exists(assetPath))
                {
                    string shaderContent = File.ReadAllText(assetPath);
                    File.WriteAllText(shaderPath, shaderContent);

                    shaderDataList.Add(new ShaderData
                    {
                        shaderName = fileName,
                        originalName = shader.name,
                        isShaderGraph = false
                    });

                    Debug.Log($"Saved shader: {shader.name}");
                }
                else
                {
                    Debug.LogWarning($"Could not find shader file for: {shader.name}");
                }

                if (isShaderGraph && shaderToMaterial.ContainsKey(shader))
                {
                    Material mat = shaderToMaterial[shader];
                    string matPropsPath = Path.Combine(shadersDir, shaderName + "_properties.json");
                    SaveMaterialProperties(mat, matPropsPath);
                }
            }

            if (shaderDataList.Count > 0)
            {
                string jsonPath = Path.Combine(shadersDir, "shader_data.json");
                File.WriteAllText(jsonPath, JsonUtility.ToJson(new ShaderDataContainer { shaders = shaderDataList }, true));
            }
        }

        void SaveMaterialProperties(Material material, string path)
        {
            MaterialPropertyData propData = new MaterialPropertyData();
            propData.materialName = material.name;
            propData.shaderName = material.shader.name;
            propData.properties = new List<MaterialProperty>();

            Shader shader = material.shader;
            int propertyCount = ShaderUtil.GetPropertyCount(shader);

            for (int i = 0; i < propertyCount; i++)
            {
                string propName = ShaderUtil.GetPropertyName(shader, i);
                ShaderUtil.ShaderPropertyType propType = ShaderUtil.GetPropertyType(shader, i);

                MaterialProperty prop = new MaterialProperty();
                prop.name = propName;
                prop.type = propType.ToString();

                switch (propType)
                {
                    case ShaderUtil.ShaderPropertyType.Color:
                        Color col = material.GetColor(propName);
                        prop.colorValue = new float[] { col.r, col.g, col.b, col.a };
                        break;
                    case ShaderUtil.ShaderPropertyType.Vector:
                        Vector4 vec = material.GetVector(propName);
                        prop.vectorValue = new float[] { vec.x, vec.y, vec.z, vec.w };
                        break;
                    case ShaderUtil.ShaderPropertyType.Float:
                    case ShaderUtil.ShaderPropertyType.Range:
                        prop.floatValue = material.GetFloat(propName);
                        break;
                    case ShaderUtil.ShaderPropertyType.TexEnv:
                        Texture tex = material.GetTexture(propName);
                        if (tex != null)
                        {
                            prop.textureName = tex.name;
                            Vector2 offset = material.GetTextureOffset(propName);
                            Vector2 scale = material.GetTextureScale(propName);
                            prop.textureOffset = new float[] { offset.x, offset.y };
                            prop.textureScale = new float[] { scale.x, scale.y };
                        }
                        break;
                }

                propData.properties.Add(prop);
            }

            string json = JsonUtility.ToJson(propData, true);
            File.WriteAllText(path, json);
        }

        void SaveLightmapData(string modOutputDir)
        {
            LightmapData[] lightmaps = LightmapSettings.lightmaps;

            if (lightmaps == null || lightmaps.Length == 0)
            {
                Debug.Log("No lightmaps to save.");
                return;
            }

            string lightmapDir = Path.Combine(modOutputDir, "Lightmaps");
            if (!Directory.Exists(lightmapDir))
                Directory.CreateDirectory(lightmapDir);

            for (int i = 0; i < lightmaps.Length; i++)
            {
                if (lightmaps[i].lightmapColor != null)
                {
                    Texture2D readable = MakeTextureReadableAndUncompressed(lightmaps[i].lightmapColor);
                    if (readable != null)
                    {
                        string texPath = Path.Combine(lightmapDir, $"lightmap_{i}_color.png");
                        File.WriteAllBytes(texPath, readable.EncodeToPNG());
                        DestroyImmediate(readable);
                    }
                }

                if (lightmaps[i].lightmapDir != null)
                {
                    Texture2D readable = MakeTextureReadableAndUncompressed(lightmaps[i].lightmapDir);
                    if (readable != null)
                    {
                        string texPath = Path.Combine(lightmapDir, $"lightmap_{i}_dir.png");
                        File.WriteAllBytes(texPath, readable.EncodeToPNG());
                        DestroyImmediate(readable);
                    }
                }
            }

            var scene = EditorSceneManager.GetActiveScene();
            var allRenderers = scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<MeshRenderer>(true))
                .Where(r => r.lightmapIndex >= 0)
                .ToList();

            List<LightmapRendererData> rendererData = new List<LightmapRendererData>();

            foreach (var renderer in allRenderers)
            {
                rendererData.Add(new LightmapRendererData
                {
                    objectPath = GetGameObjectPath(renderer.gameObject),
                    lightmapIndex = renderer.lightmapIndex,
                    lightmapScaleOffset = renderer.lightmapScaleOffset
                });
            }

            string jsonPath = Path.Combine(lightmapDir, "lightmap_data.json");
            File.WriteAllText(jsonPath, JsonUtility.ToJson(new LightmapDataContainer { renderers = rendererData }, true));
        }

        Texture2D MakeTextureReadableAndUncompressed(Texture2D texture)
        {
            if (texture == null) return null;

            RenderTexture tmp = RenderTexture.GetTemporary(
                texture.width,
                texture.height,
                0,
                RenderTextureFormat.Default,
                RenderTextureReadWrite.Linear
            );

            Graphics.Blit(texture, tmp);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = tmp;

            Texture2D readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
            readable.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(tmp);

            return readable;
        }
        string GetGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            Transform parent = obj.transform.parent;

            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }


        public void BuildCurrentSceneBundle()
        {
            string baseOutputDir = $@"C:/{Application.productName}/ModdingSDK/Mods";
            string modOutputDir = Path.Combine(baseOutputDir, mapName);

            EditorUtility.DisplayProgressBar("Compiling map..", "Creating or getting folders..", 0f);

            if (!Directory.Exists(modOutputDir))
                Directory.CreateDirectory(modOutputDir);

            string tempDir = "Assets/Temp";
            if (AssetDatabase.IsValidFolder(tempDir))
                AssetDatabase.DeleteAsset(tempDir);
            AssetDatabase.CreateFolder("Assets", "Temp");

            var scene = EditorSceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();
            int rootCount = Mathf.Max(1, rootObjects.Length);

            print("Found " + rootCount + " gameObjects.");

            for (int i = 0; i < rootObjects.Length; i++)
            {
                var obj = rootObjects[i];
                if (obj.GetComponent<MapDescriptor>())
                    continue;

                GameObject copy = GameObject.Instantiate(obj);

                EditorUtility.DisplayProgressBar(
                    "Compiling map..",
                    $"Bundling {obj.transform.name} into a prefab.",
                    (float)i / rootCount
                );

                foreach (var sc in copy.GetComponentsInChildren<MonoBehaviour>())
                    if (sc.GetType().GetCustomAttributes(typeof(ExcludeCompilationAttribute), true).Length > 0)
                        DestroyImmediate(sc);

                string prefabPath = $"{tempDir}/{copy.name}.prefab";
                PrefabUtility.SaveAsPrefabAsset(copy, prefabPath);
                GameObject.DestroyImmediate(copy);
            }

            AssetDatabase.Refresh();

            var prefabFiles = Directory.GetFiles(tempDir, "*.prefab", SearchOption.TopDirectoryOnly);
            int prefabCount = Mathf.Max(1, prefabFiles.Length);
            for (int i = 0; i < prefabFiles.Length; i++)
            {
                string prefabFile = prefabFiles[i];
                string relativePath = prefabFile.Replace(Application.dataPath, "Assets").Replace("\\", "/");
                AssetImporter.GetAtPath(relativePath).SetAssetBundleNameAndVariant(Path.GetFileNameWithoutExtension(prefabFile) + ".bundle", "");

                EditorUtility.DisplayProgressBar(
                    "Compiling map..",
                    $"Bundling {Path.GetFileNameWithoutExtension(prefabFile)}.",
                    (float)i / prefabCount
                );
            }

            BuildPipeline.BuildAssetBundles(modOutputDir, BuildAssetBundleOptions.None, BuildTarget.Android);

            if (EditorUtility.scriptCompilationFailed)
            {
                EditorUtility.DisplayDialog("Compilation Failed", "Map failed to compile because there were compilation errors. Fix the issues and try again.", "Okay");
                AssetDatabase.DeleteAsset(tempDir);
                AssetDatabase.Refresh();

                Directory.Delete(modOutputDir, true);
                return;
            }

            AssetDatabase.DeleteAsset(tempDir);
            AssetDatabase.Refresh();

            int steps = 7;
            int step = 0;

            EditorUtility.DisplayProgressBar("Compiling map..", "Beginning cleanup.", ++step / (float)steps);

            string zipFilePath = Path.Combine(baseOutputDir, mapName + "." + BundlerInfo.extension);
            string readableFilePath = Path.Combine(baseOutputDir, mapName + "." + BundlerInfo.uncompileExtension);
            string readMeFilePath = Path.Combine(baseOutputDir, "readme.txt");

            if (File.Exists(readableFilePath))
                File.Delete(readableFilePath);
            if (File.Exists(zipFilePath))
                File.Delete(zipFilePath);

            EditorUtility.DisplayProgressBar("Compiling map..", "Saving lightmaps..", ++step / (float)steps);
            SaveLightmapData(modOutputDir);

            EditorUtility.DisplayProgressBar("Compiling map..", "Saving scripts..", ++step / (float)steps);
            SaveScripts(modOutputDir);

            EditorUtility.DisplayProgressBar("Compiling map..", "Saving shaders..", ++step / (float)steps);
            SaveShaders(modOutputDir);

            EditorUtility.DisplayProgressBar("Compiling map..", "Zipping mod..", ++step / (float)steps);
            ZipFile.CreateFromDirectory(modOutputDir, zipFilePath);
            ZipFile.CreateFromDirectory(modOutputDir, readableFilePath);

            Directory.Delete(modOutputDir, true);

            EditorUtility.DisplayProgressBar("Compiling map..", "Finishing up..", ++step / (float)steps);
            ReverseFile(zipFilePath);

            EditorUtility.ClearProgressBar();

            EditorUtility.DisplayDialog("Bundle Complete", $"Bundle finished, exported as {mapName}.{BundlerInfo.extension}", "OK");
        }

        static void ReverseFile(string filePath)
        {
            byte[] data = File.ReadAllBytes(filePath);
            Array.Reverse(data);
            File.WriteAllBytes(filePath, data);
        }

        void CleanupTempFolder()
        {
            string tempDir = "Assets/Temp";
            if (AssetDatabase.IsValidFolder(tempDir))
            {
                AssetDatabase.DeleteAsset(tempDir);
                AssetDatabase.Refresh();
            }
        }
    }
}

#endif

