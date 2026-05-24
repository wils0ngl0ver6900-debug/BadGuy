using UnityEngine;

public class LootItem : MonoBehaviour
{
    public ItemData itemToGive; // L'objet que ça va ajouter à l'inventaire

    // Quand un objet avec un Collider (le joueur) rentre dans la zone
    private void OnTriggerEnter(Collider other)
    {
        // On vérifie que c'est bien le joueur qui marche dessus
        if (other.CompareTag("Player"))
        {
            if (itemToGive != null)
            {
                // On essaie d'ajouter l'objet au sac
                bool pickedUp = InventoryManager.Instance.AddItem(itemToGive);

                if (pickedUp)
                {
                    UIManager.Instance.ShowNotification($"Ramassé : {itemToGive.itemName}");
                    Destroy(gameObject); // L'objet par terre disparaît
                }
                else
                {
                    UIManager.Instance.ShowNotification("Inventaire plein !");
                }
            }
        }
    }
}