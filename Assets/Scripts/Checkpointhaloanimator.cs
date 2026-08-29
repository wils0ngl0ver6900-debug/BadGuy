using UnityEngine;

// Anime le halo de checkpoint : rotation continue de l'anneau, pulsation d'ÉCHELLE (bien
// plus visible de loin qu'une simple variation de lumière/opacité) et léger flottement
// vertical, pour un rendu vivant même vu à distance depuis une caméra en hauteur. Purement
// cosmétique, aucune dépendance avec le reste du système de course — StreetRaceManager se
// contente de déplacer l'objet qui porte ce script, tout le reste est autonome.
public class CheckpointHaloAnimator : MonoBehaviour
{
    [Header("Rotation de l'anneau")]
    public Transform ringToRotate;
    public float rotationSpeed = 140f; // degrés/seconde

    [Header("Pulsation d'échelle (le repère le plus visible de loin)")]
    [Tooltip("Objet dont l'échelle pulse — mets l'objet qui regroupe TOUT le visuel (anneau + faisceau), pas juste une pièce.")]
    public Transform scalePulseTarget;
    public float minScale = 0.85f;
    public float maxScale = 1.15f;
    public float scalePulseSpeed = 2.5f;

    [Header("Flottement vertical")]
    public Transform floatTarget;
    public float floatHeight = 0.4f;
    public float floatSpeed = 1.5f;

    [Header("Pulsation lumière (optionnel, laisse vide pour ignorer)")]
    public Light glowLight;
    public float minIntensity = 3f;
    public float maxIntensity = 9f;
    public float pulseSpeed = 2f;

    [Header("Pulsation opacité du faisceau (optionnel, laisse vide pour ignorer)")]
    public Renderer beamRenderer;
    [Range(0f, 1f)] public float minAlpha = 0.35f;
    [Range(0f, 1f)] public float maxAlpha = 0.85f;

    private Material beamMaterialInstance;
    private Vector3 scaleBaseScale = Vector3.one;
    private Vector3 floatBasePos;

    private void Start()
    {
        // .material (pas .sharedMaterial) crée une instance propre à cet objet — on ne veut
        // surtout pas modifier l'asset partagé sur le disque à chaque frame.
        if (beamRenderer != null) beamMaterialInstance = beamRenderer.material;
        if (scalePulseTarget != null) scaleBaseScale = scalePulseTarget.localScale;
        if (floatTarget != null) floatBasePos = floatTarget.localPosition;
    }

    private void Update()
    {
        if (ringToRotate != null)
            ringToRotate.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);

        float lightPulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        float scalePulse = (Mathf.Sin(Time.time * scalePulseSpeed) + 1f) * 0.5f;
        float floatPulse = Mathf.Sin(Time.time * floatSpeed);

        if (scalePulseTarget != null)
        {
            float s = Mathf.Lerp(minScale, maxScale, scalePulse);
            scalePulseTarget.localScale = scaleBaseScale * s;
        }

        if (floatTarget != null)
        {
            floatTarget.localPosition = floatBasePos + Vector3.up * (floatPulse * floatHeight);
        }

        if (glowLight != null)
            glowLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, lightPulse);

        if (beamMaterialInstance != null)
        {
            Color c = beamMaterialInstance.color;
            c.a = Mathf.Lerp(minAlpha, maxAlpha, lightPulse);
            beamMaterialInstance.color = c;
        }
    }
}