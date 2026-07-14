using UnityEngine;

public class LootItem : MonoBehaviour
{
    public ItemData itemToGive;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (itemToGive != null)
            {
                // CORRECTION : AddItem renvoie maintenant un chiffre !
                int amountAdded = InventoryManager.Instance.AddItem(itemToGive);

                if (amountAdded > 0)
                {
                    UIManager.Instance.ShowNotification($"Ramassé : {itemToGive.itemName}");
                    Destroy(gameObject);
                }
                else
                {
                    UIManager.Instance.ShowNotification("Inventaire plein !");
                }
            }
        }
    }
}