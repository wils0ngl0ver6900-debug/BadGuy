using UnityEngine;
using TMPro;
using System;

// Mini-jeu "tape le code affiché" — même mécanique que le piratage d'alarme du job casino
// (ValetJobManager.StartHackMiniGame), extraite en composant AUTONOME et réutilisable.
// Pointe les mêmes objets d'UI que ValetJobManager si tu veux le même look (les deux ne
// tournent jamais en même temps, aucun risque à les partager), ou construis une copie
// dédiée si tu préfères des visuels différents pour le boîtier électronique.
public class CodeEntryMinigame : MonoBehaviour
{
    public static CodeEntryMinigame Instance;

    [Header("UI")]
    public GameObject codePanel;
    public TextMeshProUGUI codeText;
    public TMP_InputField codeInputField;
    public TextMeshProUGUI timerText;

    [Header("Code")]
    [Tooltip("Nombre de chiffres du code (4 par défaut — volontairement plus court que les 6 du casino, pensé pour une interruption rapide plutôt qu'un vrai piratage).")]
    public int codeDigits = 4;

    private string targetCode = "";
    private bool isActive = false;
    private float currentTimer = 0f;
    private Action onSuccess;
    private Action onFail;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public void StartMinigame(float timeLimit, Action success, Action fail)
    {
        onSuccess = success;
        onFail = fail;
        currentTimer = timeLimit;
        isActive = true;

        int min = (int)Mathf.Pow(10, codeDigits - 1);
        int max = (int)Mathf.Pow(10, codeDigits) - 1;
        targetCode = UnityEngine.Random.Range(min, max).ToString();

        if (codePanel != null) codePanel.SetActive(true);
        if (codeText != null) codeText.text = $"CODE : {targetCode}";

        if (codeInputField != null)
        {
            codeInputField.text = "";
            codeInputField.characterLimit = targetCode.Length;
            codeInputField.ActivateInputField();
            codeInputField.onValueChanged.RemoveAllListeners();
            codeInputField.onValueChanged.AddListener(OnInputChanged);
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void OnInputChanged(string input)
    {
        if (!isActive) return;
        if (input.Trim() == targetCode) EndMinigame(true);
    }

    private void Update()
    {
        if (!isActive) return;

        currentTimer -= Time.deltaTime;
        if (timerText != null)
        {
            timerText.text = $"{currentTimer:F1}s";
            timerText.color = currentTimer < 1.5f ? Color.red : Color.white;
        }

        if (currentTimer <= 0f) EndMinigame(false);
    }

    private void EndMinigame(bool success)
    {
        isActive = false;
        if (codePanel != null) codePanel.SetActive(false);
        if (codeInputField != null) codeInputField.text = "";

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;

        if (success) onSuccess?.Invoke();
        else onFail?.Invoke();
    }
}