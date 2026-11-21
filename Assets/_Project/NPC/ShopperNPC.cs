using Game.Core;
using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class ShopperNPC : MonoBehaviour
{
    public Transform exitPoint;
    public Transform hand;   // bos qoysan NPC-nin qabagında avtomatik yaranır

    public float grabDistance = 1.5f;
    public float walkSpeed = 2f;
    public float runSpeed = 5f;
    public float grabDelay = 0.6f;
    public float runTime = 3f;
    [Range(0f, 1f)] public float chaosChance = 0.5f;

    NavMeshAgent agent;
    GroceryItem target;
    bool busy;
    bool leaving;
    bool ready;

    bool GameRunning()
    {
        var gm = GameManager.Instance;
        return gm != null && gm.State == GameState.Playing;
    }

    IEnumerator Start()
    {
        yield return new WaitUntil(GameRunning);

        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogError("ShopperNPC: NavMeshAgent yoxdur!", this);
            yield break;
        }
        agent.speed = walkSpeed;

        if (hand == null)
        {
            GameObject h = new GameObject("Hand");
            h.transform.SetParent(transform);
            h.transform.localPosition = new Vector3(0, 1f, 0.5f);
            hand = h.transform;
        }

        ready = true;
        ChooseItem();
    }

    void Update()
    {
        if (!ready || busy) return;

        if (leaving)
        {
            if (!agent.pathPending && agent.remainingDistance < 1f)
                Destroy(gameObject);
            return;
        }

        if (target == null || target.taken)
        {
            ChooseItem();
            return;
        }

        float dist = Vector3.Distance(transform.position, target.transform.position);
        if (dist < grabDistance)
            StartCoroutine(InteractRoutine());
    }

    IEnumerator InteractRoutine()
    {
        busy = true;
        agent.isStopped = true;

        Vector3 look = target.transform.position - transform.position;
        look.y = 0;
        if (look != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(look);

        yield return new WaitForSeconds(grabDelay);

        if (target != null && !target.taken)
        {
            if (Random.value < chaosChance)
            {
                // XAOS: erzagi yere at, sonra qac
                Vector3 dir = (transform.forward + Random.insideUnitSphere * 0.5f).normalized;
                target.Knock(dir * 3f + Vector3.up * 2f);
                yield return new WaitForSeconds(0.5f);
                yield return StartCoroutine(RunAround());
            }
            else
            {
                target.PickUp(hand);
            }
        }

        yield return new WaitForSeconds(0.5f);
        agent.isStopped = false;
        busy = false;
        ChooseItem();
    }

    IEnumerator RunAround()
    {
        agent.isStopped = false;
        agent.speed = runSpeed;
        float end = Time.time + runTime;

        while (Time.time < end)
        {
            Vector3 p = transform.position + Random.insideUnitSphere * 8f;
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
                float t = Time.time + 1f;
                while (Time.time < t && Time.time < end)
                    yield return null;
            }
            else
            {
                yield return null;
            }
        }

        agent.speed = walkSpeed;
    }

    void ChooseItem()
    {
        GroceryItem[] items = FindObjectsByType<GroceryItem>(FindObjectsSortMode.None);
        List<GroceryItem> free = new List<GroceryItem>();

        foreach (GroceryItem item in items)
            if (!item.taken) free.Add(item);

        agent.isStopped = false;

        if (free.Count > 0)
        {
            target = free[Random.Range(0, free.Count)];
            agent.SetDestination(target.transform.position);
        }
        else
        {
            target = null;
            leaving = true;
            if (exitPoint != null) agent.SetDestination(exitPoint.position);
            else Destroy(gameObject);
        }
    }
}