using UnityEngine;

public class MuleDropZone : MonoBehaviour
{
    [Header("Configuration")]
    public float detectionRadius = 3f;
    public string bagItemName = "Sac de Contrebande";

    private bool isPlayerInZone = false;

    void Update()
    {
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
        InventorySlot bagSlot = InventoryManager.Instance.slots.Find(x => x.item.itemName.ToLower() == bagItemName.ToLower());

        if (bagSlot != null)
        {
            InventoryManager.Instance.RemoveItem(bagSlot.item, 1);
            InventoryUI ui = FindObjectOfType<InventoryUI>();
            if (ui != null) ui.RefreshUI();

            ContractManager.Instance.CompleteContract(ContractManager.ContractType.Mule);
        }
        else
        {
            if (UIManager.Instance != null)
                UIManager.Instance.ShowNotification("<color=red>T'as perdu la came ?! La mission est annulée !</color>");

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