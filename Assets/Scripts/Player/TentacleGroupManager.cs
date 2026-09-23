// Assets/Scripts/TentacleGroupManager.cs
using UnityEngine;

public class TentacleGroupManager : MonoBehaviour
{
    [Tooltip("Nombre de groupes (2 = marche en tripode alternée)")]
    public int groupCount = 2;

    // Nombre de tentacules en l'air par groupe. Un compteur (et non un HashSet d'ids) :
    // avec un HashSet, la PREMIÈRE tentacule qui atterrissait retirait tout le groupe,
    // alors que ses sœurs étaient encore en l'air -> l'autre groupe pouvait décoller en même temps.
    private int[] airborne;
    private float[] groupStartTime;

    // Tour de rôle : après l'atterrissage d'un groupe, le suivant est prioritaire un court instant.
    private int nextGroup = -1;
    private float lastLandTime = float.NegativeInfinity;

    // Centre du corps (moyenne des racines des tentacules), en espace du corps
    private Vector3 rootSum;
    private int rootCount;

    public void RegisterRoot(Vector3 rootLocal)
    {
        rootSum += rootLocal;
        rootCount++;
    }

    public Vector3 BodyCenterLocal => rootCount > 0 ? rootSum / rootCount : Vector3.zero;

    void Awake()
    {
        airborne = new int[groupCount];
        groupStartTime = new float[groupCount];
    }

    public bool CanStep(int groupId, float turnGrace)
    {
        // Exclusion : jamais deux groupes en l'air en même temps
        for (int g = 0; g < groupCount; g++)
            if (g != groupId && airborne[g] > 0) return false;

        // Le groupe est déjà en l'air : une tentacule en retard peut le suivre
        if (airborne[groupId] > 0) return true;

        // Ce n'est pas notre tour : on laisse l'autre groupe partir d'abord
        if (nextGroup >= 0 && nextGroup != groupId && Time.time - lastLandTime < turnGrace) return false;

        return true;
    }

    // Vrai juste après le décollage d'un groupe : ses tentacules peuvent le rejoindre
    // même sans avoir atteint le seuil, pour que les 3 pattes du tripode bougent ensemble.
    public bool IsGroupOpen(int groupId, float joinWindow)
    {
        return airborne[groupId] > 0 && Time.time - groupStartTime[groupId] <= joinWindow;
    }

    public void BeginStep(int groupId)
    {
        if (airborne[groupId] == 0) groupStartTime[groupId] = Time.time;
        airborne[groupId]++;
    }

    public void EndStep(int groupId)
    {
        airborne[groupId] = Mathf.Max(0, airborne[groupId] - 1);
        if (airborne[groupId] == 0)
        {
            nextGroup = (groupId + 1) % groupCount;
            lastLandTime = Time.time;
        }
    }
}
