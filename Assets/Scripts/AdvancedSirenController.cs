using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

[RequireComponent(typeof(Light))]
public class AdvancedSirenController : MonoBehaviour
{
    [Header("Composants")]
    public Light sirenLight;
    private HDAdditionalLightData hdLight;

    [Header("Couleurs")]
    [ColorUsage(true, true)] public Color policeRed = new Color(1f, 0f, 0f);
    [ColorUsage(true, true)] public Color policeBlue = new Color(0f, 0.2f, 1f);

    [Header("Réglages Stroboscope")]
    public float maxIntensity = 10000f;
    public float flashSpeed = 45f;

    [Header("Déplacement Organique")]
    public float moveSpeed = 0.8f;
    public float panWidth = 20f;

    [Header("Cycle de Patrouille (Nouveau)")]
    public float patrolDuration = 8f; // Combien de temps la voiture reste visible (en secondes)
    public float timeBetweenPatrols = 60f; // Attente avant le prochain passage (en secondes)

    private float animationTimer;
    private float cycleTimer;
    private bool isActive = true; // Commence allumé (ou mets à false si tu veux que ça commence éteint)

    private float initialX;
    private bool isRed = true;

    void Start()
    {
        if (sirenLight == null) sirenLight = GetComponent<Light>();
        hdLight = GetComponent<HDAdditionalLightData>();

        initialX = transform.position.x;
    }

    void Update()
    {
        cycleTimer += Time.deltaTime;

        if (isActive)
        {
            // La patrouille est terminée, on éteint
            if (cycleTimer >= patrolDuration)
            {
                isActive = false;
                cycleTimer = 0f;
                TurnOffLight();
                return;
            }

            // --- ANIMATION DE LA LUMIÈRE ---
            animationTimer += Time.deltaTime;

            // 1. Déplacement Organique
            float pan = Mathf.Sin(animationTimer * moveSpeed) * Mathf.Cos(animationTimer * moveSpeed * 0.43f) * panWidth;
            transform.position = new Vector3(initialX + pan, transform.position.y, transform.position.z);

            // 2. Logique Stroboscopique
            float cycle = animationTimer % 1.0f;

            if (cycle < 0.5f)
            {
                if (!isRed) { sirenLight.color = policeRed; isRed = true; }
                ApplyStrobe(cycle, 0f, 0.5f);
            }
            else
            {
                if (isRed) { sirenLight.color = policeBlue; isRed = false; }
                ApplyStrobe(cycle, 0.5f, 1.0f);
            }
        }
        else
        {
            // On est dans la phase de pause, on attend la prochaine patrouille
            if (cycleTimer >= timeBetweenPatrols)
            {
                isActive = true;
                cycleTimer = 0f;
                animationTimer = 0f; // Réinitialise l'animation pour qu'elle reparte proprement
            }
        }
    }

    private void ApplyStrobe(float currentCycle, float startTime, float endTime)
    {
        float activePhase = startTime + ((endTime - startTime) * 0.6f);

        if (currentCycle < activePhase)
        {
            float flash = Mathf.Sin(animationTimer * flashSpeed);
            float currentIntensity = flash > 0 ? maxIntensity : 0f;

            if (hdLight != null) hdLight.intensity = currentIntensity;
            else sirenLight.intensity = currentIntensity;
        }
        else
        {
            TurnOffLight();
        }
    }

    private void TurnOffLight()
    {
        if (hdLight != null) hdLight.intensity = 0f;
        else sirenLight.intensity = 0f;
    }
}