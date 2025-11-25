using UnityEditor;
using UnityEditor.Android;
using UnityEngine;
using System.IO;

public class NearbyGradlePostProcessor : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 100;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        var libGradle = Path.Combine(path, "unityLibrary", "build.gradle");
        if (!File.Exists(libGradle))
        {
            Debug.LogWarning("Could not find unityLibrary/build.gradle to inject Nearby.");
            return;
        }

        string text = File.ReadAllText(libGradle);
        string marker = "// NearbyInjected";
        string dependencyLine = "implementation 'com.google.android.gms:play-services-nearby:19.3.0'";

        if (text.Contains(dependencyLine))
        {
            Debug.Log("Nearby dependency already present, skipping injection.");
            return;
        }

        // Find the dependencies block
        int idx = text.IndexOf("dependencies {");
        if (idx == -1)
        {
            Debug.LogWarning("Could not find dependencies block in build.gradle");
            return;
        }

        int insertPos = text.IndexOf('\n', idx) + 1;
        string newText = text.Insert(insertPos,
            $"    {marker}\n    {dependencyLine}\n");

        File.WriteAllText(libGradle, newText);
        Debug.Log("Injected Nearby dependency into unityLibrary/build.gradle");
    }
}