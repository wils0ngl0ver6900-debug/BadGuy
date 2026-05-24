using UnityEngine;
using UnityEngine.UI; // Indispensable pour l'Image noire
using System.Collections;

public class SafehouseBed : Interactable
{
    [Header("Paramètres du dodo")]
    public float fadeDuration = 1.0f; // Temps pour fermer/ouvrir les yeux (fondu)
    public float sleepScreenDuration = 2.0f; // Temps passé dans le noir total

    public override void Interact()
    {
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            StartCoroutine(SleepRoutine(player));
        }
    }

    private IEnumerator SleepRoutine(PlayerController player)
    {
        // 1. On bloque les mouvements du joueur
        player.isDoingQTE = true;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowNotification("Vous allez vous coucher...");
        }

        // --- CRÉATION DE L'ÉCRAN NOIR DYNAMIQUE ---
        GameObject fadeCanvasObj = new GameObject("FadeCanvasTemporaire");
        Canvas canvas = fadeCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; // 999 = S'affiche par-dessus absolument tout (inventaire, etc.)

        GameObject fadeImgObj = new GameObject("ImageNoire");
        fadeImgObj.transform.SetParent(fadeCanvasObj.transform, false);
        Image fadeImage = fadeImgObj.AddComponent<Image>();
        fadeImage.color = new Color(0, 0, 0, 0); // Noir mais 100% transparent au début

        // On étire l'image pour qu'elle prenne tout l'écran
        RectTransform rect = fadeImage.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // --- ETAPE 1 : FONDU VERS LE NOIR (On s'endort) ---
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            fadeImage.color = new Color(0, 0, 0, Mathf.Lerp(0f, 1f, elapsed / fadeDuration));
            yield return null; // Attend la frame suivante
        }
        fadeImage.color = new Color(0, 0, 0, 1f); // Sécurité : on s'assure que c'est noir total

        // --- ETAPE 2 : LE SOMMEIL (L'écran est noir) ---
        yield return new WaitForSeconds(sleepScreenDuration);

        // Pendant qu'on dort (et que l'écran est noir), on applique les effets magiques :
        player.Heal(player.maxHealth);
        player.currentShield = player.maxShield;

        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.RecoverMarket();
        }
        if (ChopShop.Instance != null)
        {
            ChopShop.Instance.ResetDailyLimits();
        }
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateHealthDisplay(player.currentHealth, player.maxHealth);
            UIManager.Instance.ShowNotification("Un nouveau jour se lève !");
        }

        // --- ETAPE 3 : FONDU VERS LA LUMIÈRE (On se réveille) ---
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            fadeImage.color = new Color(0, 0, 0, Mathf.Lerp(1f, 0f, elapsed / fadeDuration));
            yield return null;
        }

        // On détruit notre toile noire temporaire pour libérer la mémoire
        Destroy(fadeCanvasObj);

        // 4. REVEIL (On redonne le contrôle des mouvements)
        player.isDoingQTE = false;
    }
}