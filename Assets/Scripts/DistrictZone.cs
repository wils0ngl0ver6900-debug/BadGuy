using UnityEngine;

public class DistrictZone : MonoBehaviour
{
    [Header("Nom exact du quartier (Doit être identique dans le TerritoryManager)")]
    public string districtName;

    private void OnTriggerEnter(Collider other)
    {
        // On vérifie si c'est le joueur à pied OU le joueur en voiture
        if (IsPlayerOrPlayerVehicle(other) && TerritoryManager.Instance != null)
        {
            TerritoryManager.Instance.currentDistrictName = districtName;

            // Récupère les infos du quartier
            var d = TerritoryManager.Instance.cityDistricts.Find(x => x.districtName == districtName);
            if (d != null && UIManager.Instance != null)
            {
                // ALLUME LE NOM DU QUARTIER SUR LE HUD
                UIManager.Instance.ShowDistrictHUD(districtName);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayerOrPlayerVehicle(other) && TerritoryManager.Instance != null)
        {
            // Sécurité : On ne remet à zéro que si le joueur quitte le quartier ACTUEL
            if (TerritoryManager.Instance.currentDistrictName == districtName)
            {
                TerritoryManager.Instance.currentDistrictName = "Inconnu";

                // CACHE LE NOM DU QUARTIER SUR LE HUD
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.HideDistrictHUD();
                }
            }
        }
    }

    // --- LA FONCTION MAGIQUE DE DÉTECTION ---
    private bool IsPlayerOrPlayerVehicle(Collider other)
    {
        // Cas 1 : Le joueur est à pied (il possède le tag "Player")
        if (other.CompareTag("Player"))
        {
            return true;
        }

        // Cas 2 : L'objet est une voiture (peu importe son Layer ou son Tag)
        // On remonte l'arborescence pour trouver le CarController
        CarController car = other.GetComponentInParent<CarController>();

        // Si c'est bien une voiture ET que le joueur est au volant
        if (car != null && car.isDrivenByPlayer)
        {
            return true;
        }

        // Sinon (PNJ, voiture IA, etc.), on ignore !
        return false;
    }
}