using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class GroceryItem : MonoBehaviour
{
    public bool taken;
    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
    }

    public void PickUp(Transform hand)
    {
        taken = true;
        rb.isKinematic = true;
        transform.SetParent(hand);
        transform.localPosition = Vector3.zero;
    }

    public void Knock(Vector3 force)
    {
        taken = true;
        transform.SetParent(null);
        rb.isKinematic = false;
        rb.AddForce(force, ForceMode.Impulse);
    }
}