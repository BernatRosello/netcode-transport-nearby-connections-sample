using UnityEditor;
using UnityEngine;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;

public class AARBuilder
{
    private const string OUTPUT_DIR = "Assets/Plugins/Android/";
    private const string AAR_NAME = "NearbyBridge.aar";

    [MenuItem("Nearby/Build NearbyBridge AAR")]
    public static void BuildAAR()
    {
        string jdkPath = GetUnityJDKPath();

        if (string.IsNullOrEmpty(jdkPath))
        {
            EditorUtility.DisplayDialog("Error", "Unity JDK not found!", "OK");
            return;
        }

        string javac = Path.Combine(jdkPath, "bin", "javac.exe");
        string jar = Path.Combine(jdkPath, "bin", "jar.exe");

        if (!File.Exists(javac))
        {
            EditorUtility.DisplayDialog("Error", $"javac not found:\n{javac}", "OK");
            return;
        }
        if (!File.Exists(jar))
        {
            EditorUtility.DisplayDialog("Error", $"jar not found:\n{jar}", "OK");
            return;
        }

        string srcDir = Path.GetFullPath("Packages/com.bernatrosello.nearby-connections-transport/Editor/AARBuilder");
        string javaFile = Path.Combine(srcDir, "NearbyBridge.java");
        string manifestFile = Path.Combine(srcDir, "AndroidManifest.xml");

        if (!File.Exists(javaFile))
        {
            EditorUtility.DisplayDialog("Error", javaFile + " not found (looking for NearbyBridge.java)!", "OK");
            return;
        }

        string tempBuild = Path.Combine(Path.GetTempPath(), "NearbyAARBuild");

        if (Directory.Exists(tempBuild))
            Directory.Delete(tempBuild, true);

        Directory.CreateDirectory(tempBuild);
        Directory.CreateDirectory(Path.Combine(tempBuild, "classes"));

        // --- COMPILE JAVA ---
        ProcessStartInfo javacInfo = new ProcessStartInfo()
        {
            FileName = javac,
            Arguments = $"-source 1.8 -target 1.8 -d \"{Path.Combine(tempBuild, "classes")}\" \"{javaFile}\"",
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        var javacProc = Process.Start(javacInfo);
        javacProc.OutputDataReceived += (s, e) => UnityEngine.Debug.Log(e.Data);
        javacProc.ErrorDataReceived += (s, e) => UnityEngine.Debug.LogError(e.Data);

        javacProc.BeginOutputReadLine();
        javacProc.BeginErrorReadLine();
        javacProc.WaitForExit();

        // --- BUILD classes.jar ---
        string classesJar = Path.Combine(tempBuild, "classes.jar");

        ProcessStartInfo jarInfo = new ProcessStartInfo()
        {
            FileName = jar,
            Arguments = $"cf \"{classesJar}\" -C \"{Path.Combine(tempBuild, "classes")}\" .",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var jarProc = Process.Start(jarInfo);
        javacProc.OutputDataReceived += (s, e) => UnityEngine.Debug.Log(e.Data);
        javacProc.ErrorDataReceived += (s, e) => UnityEngine.Debug.LogError(e.Data);

        javacProc.BeginOutputReadLine();
        javacProc.BeginErrorReadLine();
        javacProc.WaitForExit();

        // --- CREATE AAR STRUCTURE ---
        string aarRoot = Path.Combine(tempBuild, "aar");
        Directory.CreateDirectory(aarRoot);
        Directory.CreateDirectory(Path.Combine(aarRoot, "res"));

        File.Copy(classesJar, Path.Combine(aarRoot, "classes.jar"));
        File.Copy(manifestFile, Path.Combine(aarRoot, "AndroidManifest.xml"));

        // Build AAR
        string finalAAR = Path.Combine(OUTPUT_DIR, AAR_NAME);
        if (File.Exists(finalAAR)) File.Delete(finalAAR);

        ZipFile.CreateFromDirectory(aarRoot, finalAAR);

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("NearbyBridge", "AAR built successfully!", "OK");
    }

    private static string GetUnityJDKPath()
    {
        return Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer/OpenJDK");
    }
}
