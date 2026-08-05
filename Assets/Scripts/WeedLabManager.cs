using UnityEngine;
using System.Collections.Generic;
using TMPro; // NOUVEAU : Requis pour utiliser TextMeshPro
using System.Collections;

public class WeedLabManager : MonoBehaviour
{
    public static WeedLabManager Instance;

    [Header("UI de la Plantation")]
    public GameObject labUIPanel;

    [Header("Images Draggables à verrouiller 🔒")]
    public DraggableItem dragTerre;
    public DraggableItem dragGraine;
    public DraggableItem dragArrosoir;

    [Header("Feedback Visuel 💬")]
    public TextMeshProUGUI textFeedback; // <--- Glisse ton texte UI ici dans l'inspecteur !

    [Header("Ambiance sonore 🔊")]
    [Tooltip("Optionnel : si vide, un AudioSource est ajouté automatiquement sur cet objet au lancement.")]
    public AudioSource ambientAudioSource;
    [Tooltip("Son en boucle joué tant que le labo est ouvert (bourdonnement, gouttes, etc).")]
    public AudioClip ambientSound;
    [Range(0f, 1f)] public float ambientVolume = 0.5f;
    public float ambientFadeDuration = 1f;
    private Coroutine ambientFadeRoutine;

    [HideInInspector] public bool isOpen = false;

    private void Awake() { if (Instance == null) Instance = this; }

    void Start()
    {
        if (labUIPanel != null) labUIPanel.SetActive(false);
        if (textFeedback != null) textFeedback.gameObject.SetActive(false);

        if (ambientAudioSource == null) ambientAudioSource = GetComponent<AudioSource>();
        if (ambientAudioSource == null) ambientAudioSource = gameObject.AddComponent<AudioSource>();
        ambientAudioSource.playOnAwake = false;
        ambientAudioSource.loop = true;
        ambientAudioSource.volume = 0f;
    }

    public void OpenLab()
    {
        isOpen = true;
        if (labUIPanel != null) labUIPanel.SetActive(true);
        if (textFeedback != null) textFeedback.gameObject.SetActive(false); // Cache le texte au début

        RefreshDraggableItems();

        if (UIManager.Instance != null) UIManager.Instance.ToggleHUD(false);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // On empêche le téléphone de sonner tant que le craft est en cours.
        CallApp.RequestCallBlock();

        PlayAmbient();
    }

    public void CloseLab()
    {
        isOpen = false;
        if (labUIPanel != null) labUIPanel.SetActive(false);

        if (UIManager.Instance != null) UIManager.Instance.ToggleHUD(true);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;

        // On relâche le blocage du téléphone (les autres labos peuvent encore le garder actif).
        CallApp.ReleaseCallBlock();

        StopAmbient();
    }

    private void PlayAmbient()
    {
        if (ambientAudioSource == null || ambientSound == null) return;

        ambientAudioSource.clip = ambientSound;
        if (!ambientAudioSource.isPlaying) ambientAudioSource.Play();

        if (ambientFadeRoutine != null) StopCoroutine(ambientFadeRoutine);
        ambientFadeRoutine = StartCoroutine(FadeAmbient(ambientVolume, ambientFadeDuration, stopAtEnd: false));
    }

    private void StopAmbient()
    {
        if (ambientAudioSource == null) return;

        if (ambientFadeRoutine != null) StopCoroutine(ambientFadeRoutine);
        ambientFadeRoutine = StartCoroutine(FadeAmbient(0f, ambientFadeDuration, stopAtEnd: true));
    }

    private IEnumerator FadeAmbient(float targetVolume, float duration, bool stopAtEnd)
    {
        float startVolume = ambientAudioSource.volume;
        float t = 0f;

        if (duration <= 0f)
        {
            ambientAudioSource.volume = targetVolume;
        }
        else
        {
            while (t < duration)
            {
                t += Time.unscaledDeltaTime; // Insensible à une éventuelle pause du jeu pendant le craft
                ambientAudioSource.volume = Mathf.Lerp(startVolume, targetVolume, t / duration);
                yield return null;
            }
            ambientAudioSource.volume = targetVolume;
        }

        if (stopAtEnd) ambientAudioSource.Stop();
    }

    void Update()
    {
        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseLab();
        }
    }

    // ==============================================================
    // NOUVEAU : SYSTÈME DE FEEDBACK TEXTE
    // ==============================================================

    public void ShowFeedback(string message, Color color)
    {
        if (textFeedback != null)
        {
            textFeedback.text = message;
            textFeedback.color = color;
            textFeedback.gameObject.SetActive(true);

            // Stoppe la disparition si on enchaîne les actions vite
            StopAllCoroutines();
            StartCoroutine(HideFeedbackRoutine());
        }
    }

    private IEnumerator HideFeedbackRoutine()
    {
        yield return new WaitForSeconds(3f);
        if (textFeedback != null) textFeedback.gameObject.SetActive(false);
    }

    // ==============================================================
    // LOGIQUE DE VÉRIFICATION ET DE CONSOMMATION INVENTAIRE / COFFRE
    // ==============================================================

    public void RefreshDraggableItems()
    {
        if (dragTerre != null && dragTerre.itemData != null)
            dragTerre.SetInteractable(PlayerOrStashHasItem(dragTerre.itemData.itemName));

        if (dragGraine != null && dragGraine.itemData != null)
            dragGraine.SetInteractable(PlayerOrStashHasItem(dragGraine.itemData.itemName));

        if (dragArrosoir != null && dragArrosoir.itemData != null)
            dragArrosoir.SetInteractable(PlayerOrStashHasItem(dragArrosoir.itemData.itemName));
    }

    private bool PlayerOrStashHasItem(string itemName)
    {
        string target = itemName.Trim().ToLower();

        if (InventoryManager.Instance != null)
        {
            foreach (var slot in InventoryManager.Instance.slots)
            {
                if (slot.item != null && slot.item.itemName.Trim().ToLower() == target) return true;
            }
        }

        if (SafehouseManager.Instance != null)
        {
            foreach (var slot in SafehouseManager.Instance.stashSlots)
            {
                if (slot.item != null && slot.item.itemName.Trim().ToLower() == target) return true;
            }
        }

        return false;
    }

    public void ConsumeItem(ItemData itemToConsume)
    {
        string target = itemToConsume.itemName.Trim().ToLower();

        if (InventoryManager.Instance != null)
        {
            for (int i = 0; i < InventoryManager.Instance.slots.Count; i++)
            {
                var slot = InventoryManager.Instance.slots[i];
                if (slot.item != null && slot.item.itemName.Trim().ToLower() == target)
                {
                    InventoryManager.Instance.RemoveItem(itemToConsume, 1);
                    RefreshDraggableItems();
                    return;
                }
            }
        }

        if (SafehouseManager.Instance != null)
        {
            for (int i = SafehouseManager.Instance.stashSlots.Count - 1; i >= 0; i--)
            {
                var slot = SafehouseManager.Instance.stashSlots[i];
                if (slot.item != null && slot.item.itemName.Trim().ToLower() == target)
                {
                    slot.amount -= 1;
                    if (slot.amount <= 0)
                    {
                        slot.item = null;
                        slot.amount = 0;
                    }
                    FindObjectOfType<StashUI>()?.RefreshUI();
                    RefreshDraggableItems();
                    return;
                }
            }
        }
    }
}