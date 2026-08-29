using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;

// Mini-jeu de crochetage façon Fallout — même mécanique que celle déjà utilisée pour la
// boîte à gants du job casino (ValetJobManager), mais extraite en composant AUTONOME et
// réutilisable ailleurs (ici : CarBreakIn). Pointe les MÊMES objets d'UI que ceux déjà
// configurés sur ValetJobManager si tu veux le look identique — les deux scripts ne
// tournent jamais en même temps (impossible de faire le casino et un vol de voiture
// simultanément), donc aucun conflit à se les partager.
public class LockpickMinigame : MonoBehaviour
{
    public static LockpickMinigame Instance;

    [Header("UI (les mêmes objets que ValetJobManager pour un look identique)")]
    public GameObject lockpickPanel;
    public RectTransform lockTransform;
    public RectTransform pinTransform;
    public TextMeshProUGUI timerText;

    public float lockShakeIntensity = 5f;
    public float pinMoveSpeed = 3f;

    [Header("Retour visuel (couleur) — plus clair que la rotation seule")]
    [Tooltip("Couleur du verrou quand tu es dans la bonne zone en maintenant le clic (ça tourne pour de vrai).")]
    public Color lockGoodColor = new Color(0.4f, 1f, 0.4f);
    [Tooltip("Couleur du verrou quand tu forces au mauvais endroit en maintenant le clic (ça résiste/tremble).")]
    public Color lockBadColor = new Color(1f, 0.35f, 0.35f);
    private Color lockNeutralColor = Color.white;
    private UnityEngine.UI.Image lockImage;
    private Color pinNeutralColor = Color.white;
    private UnityEngine.UI.Image pinImage;

    [Header("Casse si forcé trop longtemps au mauvais endroit")]
    [Tooltip("Si tu maintiens le clic au mauvais endroit (ça tremble) plus longtemps que ça, échec immédiat — l'outil casse net plutôt que de juste perdre du temps.")]
    public float maxForceWrongDuration = 3f;
    private float forceWrongTimer = 0f;

    private float pinAngle, lockAngle, targetPinAngle, pinShakeTimer, currentTimer, lockTolerance;
    private bool isLockRotated, isActive, isWinning;
    private Vector2 lockOriginalPos;
    private Action onSuccess;
    private Action onFail;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        if (lockTransform != null)
        {
            lockOriginalPos = lockTransform.anchoredPosition;
            lockImage = lockTransform.GetComponent<UnityEngine.UI.Image>();
            if (lockImage != null) lockNeutralColor = lockImage.color;
        }
        if (pinTransform != null)
        {
            pinImage = pinTransform.GetComponent<UnityEngine.UI.Image>();
            if (pinImage != null) pinNeutralColor = pinImage.color;
        }
    }

    // tolerance : plus la valeur est BASSE, plus la serrure est difficile (fenêtre de
    // tolérance plus étroite autour de l'angle cible caché).
    public void StartMinigame(float timeToComplete, float tolerance, Action success, Action fail)
    {
        onSuccess = success;
        onFail = fail;
        lockTolerance = tolerance;
        currentTimer = timeToComplete;
        isActive = true;
        isWinning = false;
        isLockRotated = false;
        pinShakeTimer = 0f;
        forceWrongTimer = 0f;
        if (lockImage != null) lockImage.color = lockNeutralColor;

        if (lockpickPanel != null) ActivateWithParents(lockpickPanel);
        Cursor.lockState = CursorLockMode.Locked;

        pinAngle = 0f;
        lockAngle = 0f;
        UpdateTransforms();

        targetPinAngle = UnityEngine.Random.Range(-80f, 80f);
    }

    private void Update()
    {
        if (!isActive || isWinning) return;

        currentTimer -= Time.deltaTime;
        if (timerText != null)
        {
            timerText.text = $"Temps : {currentTimer:F1}s";
            timerText.color = currentTimer < 4f ? Color.red : Color.white;
        }

        if (currentTimer <= 0f)
        {
            EndMinigame(false);
            return;
        }

        HandleInput();
    }

    private void HandleInput()
    {
        if (!isLockRotated)
        {
            float mouseMove = Input.GetAxis("Mouse X");
            pinAngle -= mouseMove * pinMoveSpeed;
            pinAngle = Mathf.Clamp(pinAngle, -90f, 90f);
        }

        float distanceFromTarget = Mathf.Abs(targetPinAngle - pinAngle);
        float maxAllowedLockAngle = 90f;

        if (distanceFromTarget > lockTolerance)
        {
            float difficultyScale = Mathf.Clamp01((distanceFromTarget - lockTolerance) / 90f);
            maxAllowedLockAngle = 90f * (1f - difficultyScale);
        }

        if (Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space))
        {
            isLockRotated = true;

            // Rotation quasi immédiate (au lieu d'un lerp doux) : l'angle atteint doit
            // refléter la distance à la bonne position DE FAÇON LISIBLE — proche = tourne
            // presque à 90°, loin = tourne à peine. Un lerp lent noyait ce signal.
            lockAngle = Mathf.MoveTowards(lockAngle, maxAllowedLockAngle, Time.deltaTime * 220f);

            // Tremblement PROPORTIONNEL à l'écart (pas juste allumé/éteint au plafond) :
            // très loin = tremble fort, un peu loin = tremble léger, pile dessus = stable.
            float wrongness = Mathf.Clamp01((distanceFromTarget - lockTolerance) / 90f);
            if (wrongness > 0.02f)
            {
                pinShakeTimer += Time.deltaTime * (15f + wrongness * 25f);
                float shakeOffset = Mathf.Sin(pinShakeTimer) * (1f + wrongness * 6f);
                if (pinTransform != null) pinTransform.localRotation = Quaternion.Euler(0, 0, pinAngle + shakeOffset);

                // Ne pénalise le temps et ne compte vers la casse que si vraiment très loin
                // (tremblement franc), pas pour un écart minime qui tourne encore pas mal.
                if (wrongness > 0.5f)
                {
                    currentTimer -= Time.deltaTime * 1.5f;
                    forceWrongTimer += Time.deltaTime;
                    if (forceWrongTimer >= maxForceWrongDuration)
                    {
                        EndMinigame(false);
                        return;
                    }
                }
                else
                {
                    forceWrongTimer = 0f;
                }

                if (lockImage != null) lockImage.color = Color.Lerp(lockNeutralColor, lockBadColor, wrongness);
                if (pinImage != null) pinImage.color = Color.Lerp(pinNeutralColor, lockBadColor, wrongness);
            }
            else
            {
                pinShakeTimer = 0f;
                forceWrongTimer = 0f;

                Color goodTint = Color.Lerp(lockNeutralColor, lockGoodColor, lockAngle / 90f);
                if (lockImage != null) lockImage.color = goodTint;
                if (pinImage != null) pinImage.color = Color.Lerp(pinNeutralColor, lockGoodColor, lockAngle / 90f);
            }

            if (lockAngle >= 88f && !isWinning) StartCoroutine(WinRoutine());
        }
        else
        {
            isLockRotated = false;
            pinShakeTimer = 0f;
            forceWrongTimer = 0f;
            lockAngle = Mathf.Lerp(lockAngle, 0f, Time.deltaTime * 10f);

            if (lockImage != null) lockImage.color = lockNeutralColor;
            if (pinImage != null) pinImage.color = pinNeutralColor;
        }

        UpdateTransforms();
    }

    private IEnumerator WinRoutine()
    {
        isWinning = true;
        float startAngle = lockAngle;
        float elapsed = 0f;
        float duration = 0.2f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            lockAngle = Mathf.Lerp(startAngle, 90f, elapsed / duration);
            UpdateTransforms();
            yield return null;
        }

        lockAngle = 90f;
        UpdateTransforms();
        yield return new WaitForSeconds(0.4f);

        EndMinigame(true);
    }

    private void UpdateTransforms()
    {
        if (pinShakeTimer == 0f && pinTransform != null)
            pinTransform.localRotation = Quaternion.Euler(0, 0, pinAngle);

        if (lockTransform != null)
        {
            lockTransform.localRotation = Quaternion.Euler(0, 0, -lockAngle);

            if (isLockRotated && lockAngle > 15f && !isWinning)
            {
                float currentShake = lockShakeIntensity * (lockAngle / 90f);
                lockTransform.anchoredPosition = lockOriginalPos + new Vector2(UnityEngine.Random.Range(-currentShake, currentShake), UnityEngine.Random.Range(-currentShake, currentShake));
            }
            else
            {
                lockTransform.anchoredPosition = lockOriginalPos;
            }
        }
    }

    private void EndMinigame(bool success)
    {
        isActive = false;
        isWinning = false;
        isLockRotated = false;

        if (lockpickPanel != null) DeactivateWithParents(lockpickPanel);
        Cursor.lockState = CursorLockMode.Confined;

        if (success) onSuccess?.Invoke();
        else onFail?.Invoke();
    }

    // Le panel de crochetage réutilise l'UI du job casino (Valet_LockpickPanel), imbriquée
    // sous Valet_MainPanel — activer seulement le panel lui-même ne suffit pas si l'un de
    // ses parents est désactivé (un enfant ne peut jamais s'afficher si un de ses parents
    // ne l'est pas, peu importe son propre état). On remonte donc toute la chaîne, et on
    // note qui était déjà actif pour ne restaurer QUE ce qu'on a nous-mêmes activé.
    private readonly List<GameObject> activatedParents = new List<GameObject>();

    private void ActivateWithParents(GameObject target)
    {
        activatedParents.Clear();
        Transform t = target.transform;
        while (t != null)
        {
            if (!t.gameObject.activeSelf)
            {
                t.gameObject.SetActive(true);
                activatedParents.Add(t.gameObject);
            }
            t = t.parent;
        }
    }

    private void DeactivateWithParents(GameObject target)
    {
        target.SetActive(false);
        foreach (GameObject go in activatedParents)
        {
            if (go != target && go != null) go.SetActive(false);
        }
        activatedParents.Clear();
    }
}