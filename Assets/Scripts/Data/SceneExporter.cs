// Assets/Scripts/Data/SceneExporter.cs
using UnityEngine;
using UnityEditor;
using System.Text;
using System.IO;
using UnityEngine.Animations.Rigging;

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

    static string SafeName(Object obj)
    {
        if (obj == null) return "NULL";
        try { return obj.name; }
        catch { return "NULL"; }
    }

    static void PrintObject(GameObject go, StringBuilder sb, int depth)
    {
        string indent = new string(' ', depth * 2);
        sb.AppendLine($"{indent}[{go.name}] active={go.activeSelf} pos={go.transform.position} rot={go.transform.eulerAngles}");

        foreach (var comp in go.GetComponents<Component>())
        {
            if (comp == null) continue;
            string compName;
            try { compName = comp.GetType().Name; }
            catch { sb.AppendLine($"{indent}  └─ [MISSING COMPONENT]"); continue; }

            sb.AppendLine($"{indent}  └─ {compName}");

            try
            {
                if (comp is TwoBoneIKConstraint ik)
                {
                    sb.AppendLine($"{indent}      weight={ik.weight}");
                    sb.AppendLine($"{indent}      root={SafeName(ik.data.root)}");
                    sb.AppendLine($"{indent}      mid={SafeName(ik.data.mid)}");
                    sb.AppendLine($"{indent}      tip={SafeName(ik.data.tip)}");
                    sb.AppendLine($"{indent}      target={SafeName(ik.data.target)}");
                    sb.AppendLine($"{indent}      hint={SafeName(ik.data.hint)}");
                    sb.AppendLine($"{indent}      hintWeight={ik.data.hintWeight}");
                    sb.AppendLine($"{indent}      targetPositionWeight={ik.data.targetPositionWeight}");
                    sb.AppendLine($"{indent}      targetRotationWeight={ik.data.targetRotationWeight}");
                }

                if (comp is MultiAimConstraint aim)
                {
                    sb.AppendLine($"{indent}      weight={aim.weight}");
                    sb.AppendLine($"{indent}      constrainedObject={SafeName(aim.data.constrainedObject)}");
                    var sources = aim.data.sourceObjects;
                    for (int i = 0; i < sources.Count; i++)
                        sb.AppendLine($"{indent}      source[{i}]={SafeName(sources[i].transform)} w={sources[i].weight}");
                }

                if (comp is Rig rig)
                    sb.AppendLine($"{indent}      weight={rig.weight}");

                if (comp is RigBuilder rb)
                {
                    foreach (var layer in rb.layers)
                        sb.AppendLine($"{indent}      layer={SafeName(layer.rig)} active={layer.active}");
                }
            }
            catch (System.Exception e)
            {
                sb.AppendLine($"{indent}      [ERROR reading component: {e.Message}]");
            }
        }

        for (int i = 0; i < go.transform.childCount; i++)
            PrintObject(go.transform.GetChild(i).gameObject, sb, depth + 1);
    }
}