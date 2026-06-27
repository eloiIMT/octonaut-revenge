// Assets/Editor/SceneExporter.cs
using UnityEngine;
using UnityEditor;
using System.Text;
using System.IO;

public class SceneExporter : MonoBehaviour
{
    [MenuItem("Tools/Export Scene Summary")]
    static void ExportScene()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== SCENE HIERARCHY ===\n");

        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            PrintObject(root, sb, 0);
        }

        string path = Path.Combine(Application.dataPath, "../scene_summary.txt");
        File.WriteAllText(path, sb.ToString());
        Debug.Log("Exported to: " + path);
    }

    static void PrintObject(GameObject go, StringBuilder sb, int depth)
    {
        string indent = new string(' ', depth * 2);
        sb.AppendLine($"{indent}[{go.name}] active={go.activeSelf}");

        foreach (var comp in go.GetComponents<Component>())
        {
            if (comp == null) continue;
            sb.AppendLine($"{indent}  └─ {comp.GetType().Name}");
        }

        for (int i = 0; i < go.transform.childCount; i++)
            PrintObject(go.transform.GetChild(i).gameObject, sb, depth + 1);
    }
}