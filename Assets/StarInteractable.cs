using UnityEngine;

public class StairInteractable : MonoBehaviour
{
    [Header("Configuração de Destino")]
    [Tooltip("Índice exato do andar para onde esta escada leva (0, 1, 2, 3...)")]
    public int targetFloorIndex = 0;
}