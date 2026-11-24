using UnityEngine;

public class KeyItem : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("encostou");
        PlayerInventory inventory = other.GetComponent<PlayerInventory>();
        if (inventory != null)
        {
            inventory.CollectKey();
            Destroy(gameObject); // Remove a chave do cenário após ser coletada
        }
    }
}