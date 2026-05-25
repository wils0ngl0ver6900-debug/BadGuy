using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TerritoryManager : MonoBehaviour
{
    public static TerritoryManager Instance;

    public enum Faction { None, Skulls, Vipers, Mafia }

    [System.Serializable]
    public class District
    {
        public string districtName;
        public Faction rivalGang;
        public bool rivalGangDefeated = false;

        [Header("Guerre de Territoire ⚔️")]
        public int sabotagesDone = 0;
        public int lieutenantsKilled = 0;

        [Header("Progression du Contrôle 📈")]
        [Range(0, 100)] public int playerControlPercentage = 0;

        [Header("Équipe d'employés débloqués")]
        public bool thievesUnlocked = false;
        public bool dealersUnlocked = false;
        public bool robbersUnlocked = false;
    }

    [Header("Carte des Territoires 🗺️")]
    public List<District> cityDistricts = new List<District>();
    public string currentDistrictName = "Inconnu";

    [Header("Revenus (Argent Sale)")]
    public int thiefIncome = 30;
    public int dealerIncome = 75;
    public int robberIncome = 150;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        StartCoroutine(EmployeeIncomeRoutine());
    }

    public void IncreasePlayerControl(string districtName, int amount)
    {
        District d = cityDistricts.Find(x => x.districtName == districtName);
        if (d != null && d.playerControlPercentage < 100)
        {
            int maxControl = (d.rivalGang != Faction.None && !d.rivalGangDefeated) ? 25 : 100;

            if (d.playerControlPercentage >= maxControl)
            {
                if (maxControl == 25 && UIManager.Instance != null)
                {
                    UIManager.Instance.ShowNotification($"<color=red>Emprise bloquée (25%) ! Déclarez la guerre aux {d.rivalGang}.</color>");
                }
                return;
            }

            d.playerControlPercentage = Mathf.Clamp(d.playerControlPercentage + amount, 0, maxControl);

            if (UIManager.Instance != null)
                UIManager.Instance.ShowNotification($"Emprise sur {d.districtName} : {d.playerControlPercentage}%");

            CheckEmployeeUnlocks(d);
        }
    }

    public void RegisterObjectiveCompletion(string district, GangObjective.ObjectiveType type)
    {
        District d = cityDistricts.Find(x => x.districtName == district);
        if (d == null || d.rivalGang == Faction.None || d.rivalGangDefeated) return;

        if (type == GangObjective.ObjectiveType.Sabotage)
        {
            if (d.sabotagesDone < 3)
            {
                d.sabotagesDone++;
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowNotification($"<color=orange>Sabotage réussi ({d.sabotagesDone}/3)</color>");

                if (d.sabotagesDone == 3 && UIManager.Instance != null)
                    UIManager.Instance.ShowNotification($"<color=red>Les opérations des {d.rivalGang} sont ruinées ! Leurs lieutenants sortent de leur cachette.</color>");
            }
        }
        else if (type == GangObjective.ObjectiveType.Lieutenant)
        {
            if (d.sabotagesDone < 3)
            {
                if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=yellow>Détruisez d'abord les opérations (Sabotages) du gang !</color>");
                return;
            }

            if (d.lieutenantsKilled < 3)
            {
                d.lieutenantsKilled++;
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowNotification($"<color=orange>Lieutenant éliminé ({d.lieutenantsKilled}/3)</color>");

                if (d.lieutenantsKilled == 3 && UIManager.Instance != null)
                    UIManager.Instance.ShowNotification($"<color=red>LE BOSS DES {d.rivalGang.ToString().ToUpper()} EST À DÉCOUVERT ! ÉLIMINEZ-LE !</color>");
            }
        }
        else if (type == GangObjective.ObjectiveType.Boss)
        {
            if (d.lieutenantsKilled < 3)
            {
                if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=yellow>Le Boss est intouchable ! Tuez ses lieutenants d'abord.</color>");
                return;
            }

            DefeatGangInDistrict(district);
        }
    }

    public void DefeatGangInDistrict(string districtName)
    {
        District d = cityDistricts.Find(x => x.districtName == districtName);
        if (d != null && d.rivalGang != Faction.None && !d.rivalGangDefeated)
        {
            d.rivalGangDefeated = true;
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowNotification($"<color=#00FF22>LE BOSS EST MORT ! Les {d.rivalGang} ont été chassés de {d.districtName} !</color>");
                UIManager.Instance.ShowNotification("<color=cyan>Le plafond de 25% est levé ! Prenez le contrôle total.</color>");
            }
        }
    }

    private void CheckEmployeeUnlocks(District d)
    {
        if (d.playerControlPercentage >= 25 && !d.thievesUnlocked)
        {
            d.thievesUnlocked = true;
            TriggerUnlockMessage(d.districtName, "<color=cyan>VOLEURS EN PLACE</color>", "Ils détroussent les passants pour vous.");
        }
        if (d.playerControlPercentage >= 55 && !d.dealersUnlocked)
        {
            d.dealersUnlocked = true;
            TriggerUnlockMessage(d.districtName, "<color=orange>DEALERS DÉPLOYÉS</color>", "Le trafic de rue est actif dans la zone.");
        }
        if (d.playerControlPercentage >= 85 && !d.robbersUnlocked)
        {
            d.robbersUnlocked = true;
            TriggerUnlockMessage(d.districtName, "<color=red>ÉQUIPE DE BRAQUEURS</color>", "Les commerces locaux sont sous votre coupe !");
        }
    }

    private void TriggerUnlockMessage(string district, string title, string desc)
    {
        if (UIManager.Instance != null)
            UIManager.Instance.ShowNotification($"[Quartier: {district}] \n{title} ! {desc}");
    }

    private IEnumerator EmployeeIncomeRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(60f);

            int totalIncome = 0;
            int activeWorkersCount = 0;

            foreach (District d in cityDistricts)
            {
                if (d.thievesUnlocked) { totalIncome += thiefIncome; activeWorkersCount++; }
                if (d.dealersUnlocked) { totalIncome += dealerIncome; activeWorkersCount++; }
                if (d.robbersUnlocked) { totalIncome += robberIncome; activeWorkersCount++; }
            }

            if (totalIncome > 0 && GameManager.Instance != null)
            {
                GameManager.Instance.AddDirtyMoney(totalIncome);
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowNotification($"<color=yellow>Réseau criminel ({activeWorkersCount} équipes) : +{totalIncome}$ (Argent sale)</color>");
            }
        }
    }

    // NOUVEAU : Fonction qui dit au reste du jeu qui contrôle le quartier actuel !
    public Faction GetDominantFactionInCurrentDistrict()
    {
        District d = cityDistricts.Find(x => x.districtName == currentDistrictName);
        if (d != null && !d.rivalGangDefeated)
        {
            return d.rivalGang; // Renvoie le gang ennemi si le quartier n'est pas encore conquis
        }
        return Faction.None; // Renvoie "None" si c'est au joueur ou vide
    }
}