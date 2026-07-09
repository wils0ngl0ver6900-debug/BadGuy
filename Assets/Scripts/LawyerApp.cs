using UnityEngine;

public class LawyerApp : MonoBehaviour
{
    [Header("UI App Avocat")]
    [Tooltip("Glisse ici le Panel de l'application Avocat")]
    public GameObject appPanel;

    [Header("Tarifs Progressifs (De 0 à 5 Étoiles)")]
    // L'index 0 = 0 étoile, Index 1 = 1 étoile, etc.
    [Tooltip("Prix en Argent Propre pour 0, 1, 2, 3, 4 et 5 étoiles")]
    public int[] cleanPrices = { 0, 1500, 3000, 8000, 15000, 30000 };

    [Tooltip("Prix en Argent Sale (Plus cher) pour 0, 1, 2, 3, 4 et 5 étoiles")]
    public int[] dirtyPrices = { 0, 3000, 6000, 16000, 30000, 60000 };

    // --- FONCTIONS D'OUVERTURE / FERMETURE DE L'APP ---

    public void OpenApp()
    {
        if (appPanel != null)
        {
            appPanel.SetActive(true);
        }
    }

    public void CloseApp()
    {
        if (appPanel != null)
        {
            appPanel.SetActive(false);
        }
    }

    // --- FONCTIONS DE PAIEMENT ---

    // Fonction à relier au bouton "Payer en Argent Propre"
    public void PayWithCleanMoney()
    {
        if (GameManager.Instance == null) return;

        int stars = GameManager.Instance.wantedLevel;

        if (stars == 0)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Vous n'êtes pas recherché par la police.");
            return;
        }

        int cost = cleanPrices[stars];

        // Vérifie si le joueur a assez d'argent propre
        if (GameManager.Instance.cleanMoney >= cost)
        {
            GameManager.Instance.cleanMoney -= cost;
            ExecuteBribe();
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"Avocat payé avec succès (-{cost}$ Propres).");
        }
        else
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=red>Fonds propres insuffisants.</color>");
        }
    }

    // Fonction à relier au bouton "Payer en Argent Sale"
    public void PayWithDirtyMoney()
    {
        if (GameManager.Instance == null) return;

        int stars = GameManager.Instance.wantedLevel;

        if (stars == 0)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Vous n'êtes pas recherché par la police.");
            return;
        }

        int cost = dirtyPrices[stars];

        // Vérifie si le joueur a assez d'argent sale
        if (GameManager.Instance.dirtyMoney >= cost)
        {
            GameManager.Instance.dirtyMoney -= cost;

            // On synchronise l'inventaire physique de l'argent sale
            GameManager.Instance.SyncDirtyMoneyItem();

            ExecuteBribe();
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"Avocat payé avec succès (-{cost}$ Sales).");
        }
        else
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=red>Argent sale insuffisant.</color>");
        }
    }

    // Le processus de corruption commun aux deux méthodes
    private void ExecuteBribe()
    {
        GameManager.Instance.LoseCops();

        if (UIManager.Instance != null) UIManager.Instance.UpdateHUD();
    }
}