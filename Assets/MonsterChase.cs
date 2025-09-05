// MonsterChase.cs
using UnityEngine;

public class MonsterChase : MonoBehaviour
{
    public Transform player;
    public float moveSpeed = 3f;
    public float stoppingDistance = 1.5f;

    void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
    }

    void Update()
    {
        if (!player) return;

        Vector3 target = new Vector3(player.position.x, transform.position.y, player.position.z);
        float dist = Vector3.Distance(transform.position, target);

        if (dist > stoppingDistance)
        {
            Vector3 dir = (target - transform.position).normalized;
            transform.position += dir * moveSpeed * Time.deltaTime;
            transform.LookAt(target);
        }
    }
}
