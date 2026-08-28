using UnityEngine;
using System;
using System.Collections;
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

    private float pinAngle, lockAngle, targetPinAngle, pinShakeTimer, currentTimer, lockTolerance;
    private bool isLockRotated, isActive, isWinning;
    private Vector2 lockOriginalPos;
    private Action onSuccess;
    private Action onFail;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        if (lockTransform != null) lockOriginalPos = lockTransform.anchoredPosition;
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

        if (lockpickPanel != null) lockpickPanel.SetActive(true);
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
            lockAngle = Mathf.Lerp(lockAngle, maxAllowedLockAngle, Time.deltaTime * 5f);

            if (Mathf.Abs(lockAngle - maxAllowedLockAngle) < 1f && maxAllowedLockAngle < 85f)
            {
                pinShakeTimer += Time.deltaTime * 30f;
                float shakeOffset = Mathf.Sin(pinShakeTimer) * 2f;
                if (pinTransform != null) pinTransform.localRotation = Quaternion.Euler(0, 0, pinAngle + shakeOffset);
                currentTimer -= Time.deltaTime * 1.5f;
            }
            else
            {
                pinShakeTimer = 0f;
            }

            if (lockAngle >= 88f && !isWinning) StartCoroutine(WinRoutine());
        }
        else
        {
            isLockRotated = false;
            pinShakeTimer = 0f;
            lockAngle = Mathf.Lerp(lockAngle, 0f, Time.deltaTime * 10f);
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

        if (lockpickPanel != null) lockpickPanel.SetActive(false);
        Cursor.lockState = CursorLockMode.Confined;

        if (success) onSuccess?.Invoke();
        else onFail?.Invoke();
    }
}