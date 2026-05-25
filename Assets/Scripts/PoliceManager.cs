using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PoliceManager : MonoBehaviour
{
    public static PoliceManager Instance;

    [Header("Ressources (Prefabs) 🚓")]
    public GameObject copCarPrefab; // Remplace ton ancien copPrefab par la voiture !

    [Header("Paramètres de Fuite 🚔")]
    public float escapeTimer = 0f;
    public float baseTimeToEscape = 15f; // 15s par étoile pour être oublié
    public bool isPlayerSpotted = false;
    public Vector3 lastKnownPosition;

    [Header("Gestion des Renforts 🚨")]
    public int maxCopsAllowed = 0;
    public List<GameObject> activeCops = new List<GameObject>();
    public float spawnCooldown = 5f;
    private float nextSpawnTime = 0f;

    [Header("Apparition Organique (Hors-Champ) 🗺️")]
    public float minSpawnDist = 60f;
    public float maxSpawnDist = 150f;

    private Transform player;
    private Camera mainCam;
    private TrafficNode[] allNodes;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        mainCam = Camera.main;
        allNodes = FindObjectsOfType<TrafficNode>();
    }

    void Update()
    {
        // ON UTILISE TON GAMEMANAGER ! Si pas d'étoiles, on ne fait rien.
        if (GameManager.Instance == null || GameManager.Instance.wantedLevel == 0 || player == null) return;

        ManageEscape();
        UpdateMaxCops();
        ManageReinforcements();
    }

    // Appelée par l'IA des policiers (NPCBrain) quand ils ont un visuel direct sur le joueur
    public void ReportPlayerSight(Vector3 pos)
    {
        isPlayerSpotted = true;
        lastKnownPosition = pos;

        // On convertit grossièrement ton wantedLevel en étoiles pour le calcul du temps
        int level = GameManager.Instance.wantedLevel;
        int stars = level >= 50 ? (level / 50) : level;
        if (stars == 0) stars = 1;

        escapeTimer = baseTimeToEscape * stars; // Relance le chrono de recherche
    }

    private void ManageEscape()
    {
        if (!isPlayerSpotted)
        {
            escapeTimer -= Time.deltaTime;
            if (escapeTimer <= 0)
            {
                GameManager.Instance.wantedLevel = 0; // Le joueur a réussi à s'enfuir ! On remet à zéro.
                if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=cyan>Avis de recherche expiré. Vous êtes libre.</color>");
            }
        }

        // On remet le visuel à faux. Si un flic voit encore le joueur, il rappellera la fonction ReportPlayerSight() à la prochaine frame
        isPlayerSpotted = false;
    }

    private void UpdateMaxCops()
    {
        int level = GameManager.Instance.wantedLevel;
        // Adaptation à ton système (si c'est 1,2,3,4,5 étoiles, ou des points)
        int stars = level >= 50 ? (level / 50) : level;

        switch (stars)
        {
            case 1: maxCopsAllowed = 1; break;
            case 2: maxCopsAllowed = 3; break;
            case 3: maxCopsAllowed = 6; break;
            case 4: maxCopsAllowed = 10; break;
            case 5: maxCopsAllowed = 15; break;
            default: maxCopsAllowed = stars > 5 ? 15 : 1; break;
        }
    }

    private void ManageReinforcements()
    {
        // Nettoie la liste des voitures explosées
        activeCops.RemoveAll(item => item == null);

        if (activeCops.Count < maxCopsAllowed && Time.time >= nextSpawnTime)
        {
            SpawnCopOrganically();
            nextSpawnTime = Time.time + spawnCooldown;
        }
    }

    private void SpawnCopOrganically()
    {
        if (allNodes.Length == 0 || copCarPrefab == null) return;

        List<TrafficNode> validNodes = new List<TrafficNode>();

        foreach (TrafficNode node in allNodes)
        {
            float dist = Vector3.Distance(player.position, node.transform.position);

            // Est-ce qu'on est à la bonne distance ?
            if (dist >= minSpawnDist && dist <= maxSpawnDist)
            {
                // LA MAGIE : Est-ce que cette rue est dans le dos du joueur ?
                Vector3 viewPos = mainCam.WorldToViewportPoint(node.transform.position);
                bool isOffScreen = viewPos.x < -0.1f || viewPos.x > 1.1f || viewPos.y < -0.1f || viewPos.y > 1.1f || viewPos.z < 0;

                if (isOffScreen)
                {
                    validNodes.Add(node);
                }
            }
        }

        if (validNodes.Count > 0)
        {
            // On tire une rue au hasard parmi celles qu'on ne regarde pas
            TrafficNode spawnNode = validNodes[Random.Range(0, validNodes.Count)];

            GameObject cop = Instantiate(copCarPrefab, spawnNode.transform.position, spawnNode.transform.rotation);
            CarAI ai = cop.GetComponent<CarAI>();

            if (ai != null)
            {
                // Le flic se dirige vers l'endroit du dernier appel radio, pas bêtement sur le joueur !
                ai.currentNode = GetClosestNodeToPosition(lastKnownPosition);
            }

            activeCops.Add(cop);
        }
    }

    private TrafficNode GetClosestNodeToPosition(Vector3 pos)
    {
        TrafficNode bestNode = null;
        float minDist = Mathf.Infinity;

        foreach (TrafficNode node in allNodes)
        {
            float dist = Vector3.Distance(pos, node.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                bestNode = node;
            }
        }
        return bestNode;
    }
}