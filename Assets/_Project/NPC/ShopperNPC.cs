using Game.Core;
using Game.Gameplay;
using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// NPC shopper (by Farid). Walks to a product, then either takes it or knocks it over and runs around (chaos).
/// Targets:
///   - GroceryItem objects lying around (Farid's test items), and
///   - store Shelves with stock left: the NPC takes a REAL product off the shelf (the shelf stock goes down).
///     Chaos on a shelf = the product is knocked onto the floor, where the player can still grab it.
/// When nothing is left it walks to exitPoint and disappears.
/// Needs a NavMeshAgent and a baked NavMesh (Tools → Store → Add Shopper Bots + NavMesh).
/// </summary>
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

    [Tooltip("How many products this NPC wants before leaving (shelves only). 0 = until everything is gone.")]
    public int wantsItems = 3;

    NavMeshAgent agent;
    GroceryItem target;
    Shelf targetShelf;
    bool busy;
    bool leaving;
    bool ready;
    int itemsTaken;

    bool GameRunning()
    {
        var gm = GameManager.Instance;
        return gm != null && gm.State == GameState.Playing;
    }

    IEnumerator Start()
    {
        yield return new WaitUntil(GameRunning);
        yield return null; // let shelves build and fill themselves first

        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogError("ShopperNPC: NavMeshAgent yoxdur!", this);
            yield break;
        }
        if (!agent.isOnNavMesh)
        {
            Debug.LogError("ShopperNPC: not on a NavMesh — bake one (Tools → Store → Add Shopper Bots + NavMesh).", this);
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

        if (targetShelf != null)
        {
            if (targetShelf.IsEmpty) { ChooseItem(); return; } // someone (the player!) was faster

            if (Flat(transform.position - targetShelf.FrontPoint) < grabDistance)
                StartCoroutine(InteractRoutine());
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

    static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

    IEnumerator InteractRoutine()
    {
        busy = true;
        agent.isStopped = true;

        Vector3 lookAt = targetShelf != null ? targetShelf.transform.position : target.transform.position;
        Vector3 look = lookAt - transform.position;
        look.y = 0;
        if (look != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(look);

        yield return new WaitForSeconds(grabDelay);

        bool chaos = Random.value < chaosChance;

        if (targetShelf != null)
        {
            // Take a real product off the store shelf
            Game.Interaction.Pickup product = targetShelf.TakeOneItem();
            if (product != null)
            {
                itemsTaken++;
                if (chaos)
                {
                    // XAOS: mehsulu yere at, sonra qac
                    Vector3 dir = (transform.forward + Random.insideUnitSphere * 0.5f).normalized;
                    product.KnockByNpc(dir * 3f + Vector3.up * 2f);
                    yield return new WaitForSeconds(0.5f);
                    yield return StartCoroutine(RunAround());
                }
                else
                {
                    if (hand.childCount > 0) Destroy(hand.GetChild(0).gameObject); // one item in hand at a time
                    product.TakeByNpc(hand);
                }
            }
        }
        else if (target != null && !target.taken)
        {
            if (chaos)
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
        agent.isStopped = false;
        target = null;
        targetShelf = null;

        bool wantsMore = wantsItems <= 0 || itemsTaken < wantsItems;

        // Store shelves with something left
        var shelves = new List<Shelf>();
        if (wantsMore)
            foreach (Shelf s in FindObjectsByType<Shelf>(FindObjectsSortMode.None))
                if (!s.IsEmpty) shelves.Add(s);

        // Farid's loose grocery items
        GroceryItem[] items = FindObjectsByType<GroceryItem>(FindObjectsSortMode.None);
        List<GroceryItem> free = new List<GroceryItem>();
        foreach (GroceryItem item in items)
            if (!item.taken) free.Add(item);

        int total = shelves.Count + free.Count;
        if (total > 0)
        {
            int pick = Random.Range(0, total);
            if (pick < shelves.Count)
            {
                targetShelf = shelves[pick];
                Vector3 stand = targetShelf.FrontPoint;
                if (NavMesh.SamplePosition(stand, out NavMeshHit hit, 2f, NavMesh.AllAreas)) stand = hit.position;
                agent.SetDestination(stand);
            }
            else
            {
                target = free[pick - shelves.Count];
                agent.SetDestination(target.transform.position);
            }
        }
        else
        {
            leaving = true;
            if (exitPoint != null) agent.SetDestination(exitPoint.position);
            else Destroy(gameObject);
        }
    }
}
