// Assets/Scripts/TentacleWave.cs
using UnityEngine;

public class TentacleWave : MonoBehaviour
{
    [Header("Bones de la tentacule (root en premier)")]
    public Transform[] bones;

    [Header("Ondulation")]
    public float waveSpeed = 2f;
    public float waveAmplitude = 8f;      // en degrés
    public float phaseOffsetPerBone = 40f; // décalage de phase entre chaque os
    public Vector3 waveAxis = Vector3.forward;

    [Header("Réaction au mouvement")]
    public CharacterController controller;
    public float speedInfluence = 1.5f;

    private Quaternion[] initialLocalRotations;
    private float timeOffset;

    void Start()
    {
        initialLocalRotations = new Quaternion[bones.Length];
        for (int i = 0; i < bones.Length; i++)
            initialLocalRotations[i] = bones[i].localRotation;

        timeOffset = Random.Range(0f, 100f); // désynchronise les tentacules entre elles
    }

    void Update()
    {
        float speedFactor = 1f;
        if (controller != null)
        {
            float horizontalSpeed = new Vector3(controller.velocity.x, 0f, controller.velocity.z).magnitude;
            speedFactor = 1f + horizontalSpeed * speedInfluence;
        }

        for (int i = 0; i < bones.Length; i++)
        {
            float phase = (Time.time + timeOffset) * waveSpeed + i * phaseOffsetPerBone * Mathf.Deg2Rad;
            float angle = Mathf.Sin(phase) * waveAmplitude * speedFactor;
            bones[i].localRotation = initialLocalRotations[i] * Quaternion.AngleAxis(angle, waveAxis);
        }
    }
}