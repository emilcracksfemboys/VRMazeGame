using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(NavMeshAgent))]
public class NavMeshMonster : MonoBehaviour
{
    public Transform player;
    private NavMeshAgent agent;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
    }

    void Update()
    {
        if (player)
        {
            agent.SetDestination(player.position);

            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                SceneManager.LoadScene("MenuScene");
                SceneManager.UnloadSceneAsync("Level");
            }
        }
    }
}
