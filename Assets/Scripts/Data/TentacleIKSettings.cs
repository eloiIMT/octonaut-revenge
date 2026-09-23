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

    [Header("Déclenchement du pas")]
    [Tooltip("Distance horizontale (m) entre le pied posé et son point de repos au-delà de laquelle la tentacule fait un pas.")]
    public float stepThreshold = 0.6f;
    [Tooltip("Distance (m) à laquelle le pied atterrit EN AVANCE du point de repos, dans le sens du mouvement. " +
             "Amplitude totale d'une foulée ≈ stepThreshold + stepOvershoot.")]
    public float stepOvershoot = 0.45f;
    [Tooltip("Étirement maximal, en fraction de la longueur de la tentacule (distance racine -> pied). Au-delà, " +
             "la tentacule fait un pas même si ce n'est pas le tour de son groupe (démarrage brusque, demi-tour).")]
    [Range(0.8f, 2f)] public float maxReachRatio = 1.5f;
    [Tooltip("Vitesse (m/s) à partir de laquelle l'overshoot est appliqué en entier (plus lent = pas plus courts).")]
    public float fullStrideSpeed = 2f;
    [Tooltip("Lissage de la vitesse mesurée de l'ancre (plus grand = plus réactif).")]
    public float velocitySmoothing = 10f;

    [Header("Retour au repos à l'arrêt")]
    [Tooltip("Temps (s) d'immobilité avant que les pieds se replacent sous le corps.")]
    public float settleDelay = 0.3f;
    [Tooltip("Écart minimal (m) pour qu'un pied se replace à l'arrêt.")]
    public float settleThreshold = 0.1f;

    [Header("Trajectoire du pas")]
    public float stepDuration = 0.2f;
    [Tooltip("Hauteur (m) de l'arc. Garder nettement inférieur à stepThreshold, sinon le pas paraît vertical.")]
    public float stepHeight = 0.25f;

    [Header("Forme de la tentacule (TentacleCurveIK)")]
    [Tooltip("Vrai : la tentacule entière forme une arche racine -> pied. Faux : Chain IK d'origine (seul le bout plie).")]
    public bool useCurveSolver = true;
    [Tooltip("0 = la tentacule part dans sa direction naturelle, 1 = elle part droit vers le pied.")]
    [Range(0f, 1f)] public float rootAim = 0.5f;
    [Tooltip("Avancée horizontale de l'arche côté racine, en fraction de la distance racine -> pied.")]
    [Range(0f, 0.6f)] public float rootReach = 0.4f;
    [Tooltip("Recul horizontal de l'arche côté pied (0 = le bout arrive à la verticale).")]
    [Range(0f, 0.6f)] public float footReach = 0.15f;
    [Tooltip("Bombé minimal vers l'extérieur (fraction de la longueur) quand le pied est presque sous la racine.")]
    [Range(0f, 0.6f)] public float minBulge = 0.3f;
    [Tooltip("Hauteur de l'arche côté pied, relative au côté racine.")]
    public float endHandleRatio = 0.7f;
    [Tooltip("Pendant un pas, le bout traîne derrière le mouvement (effet fouet).")]
    public float stepTipDrag = 0.8f;
    [Tooltip("Inertie de la forme : plus petit = plus mou / plus de retard. 0 = pas de lissage.")]
    public float shapeSmoothing = 12f;
    public int fabrikIterations = 3;

    [Header("Ondulation des tentacules")]
    [Tooltip("Amplitude (m) de l'ondulation au repos.")]
    public float waveAmplitude = 0.02f;
    [Tooltip("Amplitude (m) ajoutée pendant un pas.")]
    public float waveStepAmplitude = 0.06f;
    [Tooltip("Nombre d'ondulations le long de la tentacule.")]
    public float waveFrequency = 1f;
    [Tooltip("Vitesse de propagation de l'onde (rad/s).")]
    public float waveSpeed = 4f;

    [Header("Alternance des groupes")]
    [Tooltip("Quand un groupe décolle, les autres tentacules du même groupe le rejoignent si elles ont dépassé cette fraction de stepThreshold.")]
    [Range(0f, 1f)] public float groupJoinFraction = 0.35f;
    [Tooltip("Durée (s) après le décollage d'un groupe pendant laquelle ses tentacules peuvent encore le rejoindre.")]
    public float groupJoinWindow = 0.08f;
    [Tooltip("Délai (s) pendant lequel l'autre groupe est prioritaire après l'atterrissage d'un groupe.")]
    public float turnGrace = 0.15f;

    [Header("Raycast sol partagé")]
    public float raycastHeight = 1f;
    public float raycastDistance = 3f;
    [Tooltip("Vitesse à laquelle un pied sans sol sous lui (vide) revient pendre sous son ancre.")]
    public float danglingFollowSpeed = 8f;
}
