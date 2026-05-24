using UnityEngine;
using UnityEngine.AI; // Indispensable pour utiliser le NavMesh !

[RequireComponent(typeof(NavMeshAgent))]
public class CopAI : MonoBehaviour
{
    private Transform player;
    private NavMeshAgent agent;

    void Start()
    {
        // Cherche le joueur automatiquement grâce à son Tag
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }

        agent = GetComponent<NavMeshAgent>();
        agent.speed = 3.5f; // Vitesse du policier (ajuste selon ton joueur)
    }

    void Update()
    {
        // Poursuit le joueur en temps réel
        if (player != null)
        {
            agent.SetDestination(player.position);
        }
    }

    // Si le policier réussit à toucher le joueur
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            GameManager.Instance.Busted();
        }
    }
}