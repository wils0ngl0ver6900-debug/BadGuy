using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class WanderingNPC : MonoBehaviour
{
    public enum NPCState { Wandering, CallingPolice, Fleeing }
    public NPCState currentState = NPCState.Wandering;

    [Header("Paramètres de Balade")]
    public float wanderRadius = 10f;
    public float minWaitTime = 2f;
    public float maxWaitTime = 5f;

    [Header("Système de Balance (Snitch) 📱")]
    public float callPoliceDuration = 3f;
    private float currentCallTimer = 0f;

    private NavMeshAgent agent;
    private VisionCone vision;
    private Transform playerTarget;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        vision = GetComponent<VisionCone>();
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerTarget = p.transform;
    }

    void Start()
    {
        if (agent != null) agent.speed = 1.5f;
        StartCoroutine(WanderRoutine());
    }

    void Update()
    {
        if (currentState == NPCState.Wandering)
        {
            CheckPlayerIllegalActions();
        }
        else if (currentState == NPCState.CallingPolice)
        {
            currentCallTimer += Time.deltaTime;
            if (currentCallTimer >= callPoliceDuration)
            {
                CallPolice();
            }
        }
    }

    private void CheckPlayerIllegalActions()
    {
        if (vision != null && vision.isSeeingPlayer && GameManager.Instance != null && GameManager.Instance.notoriety > 0)
        {
            currentState = NPCState.CallingPolice;
            if (agent != null) agent.isStopped = true;
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Un civil appelle la police !");
        }
    }

    private void CallPolice()
    {
        if (GameManager.Instance != null) GameManager.Instance.IncreaseNotoriety(15);
        FleeFromPlayer();
    }

    private void FleeFromPlayer()
    {
        currentState = NPCState.Fleeing;
        if (agent != null)
        {
            agent.isStopped = false;
            agent.speed = 4f; // Il court plus vite !
        }

        if (playerTarget != null)
        {
            Vector3 directionAwayFromPlayer = (transform.position - playerTarget.position).normalized;
            Vector3 fleePosition = transform.position + directionAwayFromPlayer * 20f;
            if (agent != null) agent.SetDestination(fleePosition);
        }
    }

    private IEnumerator WanderRoutine()
    {
        while (true)
        {
            if (currentState == NPCState.Wandering)
            {
                if (agent != null)
                {
                    agent.isStopped = false;
                    agent.speed = 1.5f;
                }

                Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
                randomDirection += transform.position;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(randomDirection, out hit, wanderRadius, 1))
                {
                    if (agent != null) agent.SetDestination(hit.position);

                    while (agent != null && (agent.pathPending || agent.remainingDistance > 0.5f))
                    {
                        if (currentState != NPCState.Wandering) break;
                        yield return null;
                    }

                    if (currentState == NPCState.Wandering)
                    {
                        float waitTime = Random.Range(minWaitTime, maxWaitTime);
                        yield return new WaitForSeconds(waitTime);
                    }
                }
            }
            else
            {
                yield return null;
            }
        }
    }

    // --- NOUVEAU : Fonction interne pour gérer les différentes fuites proprement ---
    private void StartFleeing(string notificationMessage)
    {
        currentState = NPCState.Fleeing;

        // Sécurité maximale : si l'agent a bugué, on le force à se réveiller
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.isStopped = false;

        if (UIManager.Instance != null && !string.IsNullOrEmpty(notificationMessage))
        {
            UIManager.Instance.ShowNotification(notificationMessage);
        }

        FleeFromPlayer();
    }

    // --- CARJACKING (Appelé par CarInteraction.cs) ---
    public void ForceFlee()
    {
        StartFleeing("<color=orange>Un conducteur s'enfuit en hurlant !</color>");
    }

    // --- PIÉTONS / PICKPOCKET (Appelé par Interactable.cs) ---
    public void TriggerPanic()
    {
        StartFleeing("<color=red>Au voleur ! Le civil prend la fuite !</color>");
    }

    public void TriggerPanic(bool panicState)
    {
        StartFleeing("<color=red>Au voleur ! Le civil prend la fuite !</color>");
    }

    public void CancelCall()
    {
        currentState = NPCState.Wandering;
        currentCallTimer = 0f;
        if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Appel annulé (Témoin neutralisé).");
    }
}