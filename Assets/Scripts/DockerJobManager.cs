using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DockerJobManager : MonoBehaviour
{
    public static DockerJobManager Instance;

    [Header("État du Job")]
    public bool isJobActive = false;
    public int totalCratesToDeliver = 5;
    private int cratesDelivered = 0;
    private int cashEarned = 0;

    [Header("Économie")]
    public int cleanReward = 30;
    public int dirtyReward = 150;

    [Header("Physique")]
    public float carrySpeed = 2f;
    private float savedPlayerSpeed;

    [Header("Références Visuelles")]
    [Tooltip("Glisse ici la caisse qui est enfant du bras de ton joueur")]
    public GameObject carriedCrateModel;

    [Header("Mini-Jeu Équilibre ⚖️")]
    public GameObject balanceUIPanel; // Le Panel qui contient la jauge
    public Slider balanceSlider; // Le Slider Unity
    public float balanceDifficulty = 0.3f; // Force avec laquelle la caisse penche
    public float playerCorrectionSpeed = 0.8f; // Vitesse de redressement (Q/D)

    [HideInInspector] public bool isCarryingCrate = false;
    private bool isCurrentCrateIllegal = false;
    private PlayerController playerController;

    private float currentBalance = 0.5f; // 0.5 = Centre parfait
    private float currentDrift = 0f;
    private float driftChangeTimer = 0f;

    private void Awake() { if (Instance == null) Instance = this; }

    private void Start()
    {
        playerController = FindObjectOfType<PlayerController>();
        if (carriedCrateModel != null) carriedCrateModel.SetActive(false);
        if (balanceUIPanel != null) balanceUIPanel.SetActive(false);
    }

    public void StartJob()
    {
        isJobActive = true;
        cratesDelivered = 0;
        cashEarned = 0;

        if (UIManager.Instance != null)
            UIManager.Instance.ShowNotification("Service commencé ! Prenez une caisse et chargez le camion.");
    }

    public void EndJob()
    {
        isJobActive = false;

        // Virement du salaire total
        if (cashEarned > 0)
        {
            if (GameManager.Instance != null) GameManager.Instance.cleanMoney += cashEarned;
            if (BankApp.Instance != null) BankApp.Instance.RecordTransaction(cashEarned, "Salaire : Manutention Portuaire");
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"<color=#00FF41>Service terminé ! Salaire total : {cashEarned}$</color>");
        }
        else
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Service terminé. Tu n'as rien gagné aujourd'hui.");
        }
    }

    public void PickupCrate(bool illegalCrate)
    {
        if (isCarryingCrate || !isJobActive) return;

        isCarryingCrate = true;
        isCurrentCrateIllegal = illegalCrate;

        // Ralentissement
        if (playerController != null)
        {
            savedPlayerSpeed = playerController.moveSpeed;
            playerController.moveSpeed = carrySpeed;
        }

        // On affiche la caisse dans les mains
        if (carriedCrateModel != null) carriedCrateModel.SetActive(true);

        // On lance le mini-jeu d'équilibre
        currentBalance = 0.5f;
        currentDrift = 0f;
        if (balanceUIPanel != null) balanceUIPanel.SetActive(true);
        if (balanceSlider != null) balanceSlider.value = currentBalance;
    }

    private void Update()
    {
        if (isCarryingCrate)
        {
            ManageBalanceMiniGame();
        }
    }

    private void ManageBalanceMiniGame()
    {
        // 1. La caisse penche aléatoirement d'un côté
        driftChangeTimer -= Time.deltaTime;
        if (driftChangeTimer <= 0f)
        {
            // Choisit une direction aléatoire (négatif = gauche, positif = droite)
            currentDrift = Random.Range(-balanceDifficulty, balanceDifficulty);
            driftChangeTimer = Random.Range(0.5f, 2.0f); // Change de sens toutes les 0.5 à 2 secondes
        }

        currentBalance += currentDrift * Time.deltaTime;

        // 2. Le joueur corrige avec Q et D (ou les flèches)
        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftArrow))
        {
            currentBalance -= playerCorrectionSpeed * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            currentBalance += playerCorrectionSpeed * Time.deltaTime;
        }

        // 3. Mise à jour de la jauge UI
        if (balanceSlider != null) balanceSlider.value = currentBalance;

        // 4. Échec si la caisse penche trop
        if (currentBalance <= 0f || currentBalance >= 1f)
        {
            FailCrate();
        }
    }

    private void FailCrate()
    {
        ResetCarryState();
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=red>Vous avez fait tomber la caisse ! Le contremaître n'est pas content.</color>");
    }

    public void DeliverCrate(bool deliveredToIllegalZone)
    {
        ResetCarryState();

        if (deliveredToIllegalZone)
        {
            if (isCurrentCrateIllegal)
            {
                if (GameManager.Instance != null) GameManager.Instance.AddDirtyMoney(dirtyReward);
                if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"<color=red>Caisse rivale détournée ! +{dirtyReward}$ (Sale)</color>");
            }
            else
            {
                if (GameManager.Instance != null) GameManager.Instance.AddDirtyMoney(10);
                if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=orange>Caisse standard volée... Ça ne vaut pas grand chose.</color>");
            }
        }
        else
        {
            cashEarned += cleanReward;
            cratesDelivered++;
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification($"<color=green>Caisse {cratesDelivered}/{totalCratesToDeliver} livrée !</color>");

            if (cratesDelivered >= totalCratesToDeliver) EndJob();
        }
    }

    private void ResetCarryState()
    {
        isCarryingCrate = false;

        if (playerController != null)
        {
            playerController.moveSpeed = savedPlayerSpeed;
        }

        if (carriedCrateModel != null) carriedCrateModel.SetActive(false);
        if (balanceUIPanel != null) balanceUIPanel.SetActive(false);
    }
}