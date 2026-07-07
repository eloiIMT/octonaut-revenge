// Assets/Scripts/TentacleIKSettings.cs
using UnityEngine;

[CreateAssetMenu(fileName = "TentacleIKSettings", menuName = "Octonaut/Tentacle IK Settings")]
public class TentacleIKSettings : ScriptableObject
{
    [Header("Chain IK partagé")]
    [Range(0f, 1f)] public float chainRotationWeight = 1f;
    [Range(0f, 1f)] public float tipRotationWeight = 0.5f;
    public int maxIterations = 10;
    public float tolerance = 0.01f;

    [Header("Comportement du pas partagé")]
    public float stepThreshold = 0.5f;
    public float stepDuration = 0.25f;
    public float stepHeight = 0.3f;
    public float forwardStepBias = 0.3f;

    [Header("Raycast sol partagé")]
    public float raycastHeight = 1f;
    public float raycastDistance = 2f;
}