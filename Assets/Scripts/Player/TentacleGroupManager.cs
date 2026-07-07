// Assets/Scripts/TentacleGroupManager.cs
using UnityEngine;
using System.Collections.Generic;

public class TentacleGroupManager : MonoBehaviour
{
    private HashSet<int> steppingGroups = new HashSet<int>();

    public bool CanStep(int groupId)
    {
        // Un groupe ne peut step que si aucune tentacule de l'autre groupe n'est en train de step
        foreach (var g in steppingGroups)
            if (g != groupId) return false;
        return true;
    }

    public void BeginStep(int groupId) => steppingGroups.Add(groupId);
    public void EndStep(int groupId) => steppingGroups.Remove(groupId);
}