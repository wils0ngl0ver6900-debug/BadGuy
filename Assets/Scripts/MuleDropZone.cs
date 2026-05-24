using UnityEngine;

public class MuleDropZone : MonoBehaviour
{
    [Header("Configuration")]
    public float detectionRadius = 3f; // Plus petit que les voitures, il faut s'approcher
    public string bagItemName = "Sac de Contrebande"; // Doit correspondre EXACTEMENT au nom de ton ItemData

    private bool isPlayerInZone = false;

    void Update()
    {
        // On s'éteint si le joueur n'a pas la mission Mule
        if (ContractManager.Instance == null || !ContractManager.Instance.hasActiveContract ||
            ContractManager.Instance.currentContract != ContractManager.ContractType.Mule)
        {
            isPlayerInZone = false;
            return;
        }

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, detectionRadius);
        bool foundPlayer = false;

        foreach (var hit in hitColliders)
        {
            // On cherche le joueur à pied
            if (hit.CompareTag("Player"))
            {
                foundPlayer = true;
                if (!isPlayerInZone)
                {
                    isPlayerInZone = true;
                    if (UIManager.Instance != null)
                        UIManager.Instance.ShowNotification("<color=yellow>CLIENT : Tu as la came ? Appuie sur [E] pour livrer.</color>");
                }
                break;
            }
        }

        if (isPlayerInZone && foundPlayer)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                DeliverBag();
            }
        }

        if (!foundPlayer && isPlayerInZone)
        {
            isPlayerInZone = false;
        }
    }

    private void DeliverBag()
    {
        // 1. Le client fouille ton sac à dos pour trouver la marchandise
        ItemData bag = InventoryManager.Instance.items.Find(x => x.itemName.ToLower() == bagItemName.ToLower());

        if (bag != null)
        {
            // Succès : On te prend le sac
            InventoryManager.Instance.RemoveItem(bag);
            InventoryUI ui = FindObjectOfType<InventoryUI>();
            if (ui != null) ui.RefreshUI();

            // On te paie !
            ContractManager.Instance.CompleteContract(ContractManager.ContractType.Mule);
        }
        else
        {
            // Échec : Tu n'as plus le sac !
            if (UIManager.Instance != null)
                UIManager.Instance.ShowNotification("<color=red>T'as perdu la came ?! La mission est annulée !</color>");

            // On reset le contrat sans te payer
            ContractManager.Instance.hasActiveContract = false;
            ContractManager.Instance.currentContract = ContractManager.ContractType.None;
        }

        isPlayerInZone = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}