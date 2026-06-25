using Unity.Netcode;
using UnityEngine;

// Componente que se anade a los objetos recogibles del segundo juego.
// Define que equipo puede recogerlo y sincroniza su estado por la red.

public class PikeableObject : NetworkBehaviour
{
    // 0 = cualquier equipo puede recogerlo, 1 = solo Rosa, 2 = solo Azul
    public int allowedTeam = 0;

    // Indica si algun jugador lleva el objeto en la mano en este momento.
    // Solo el servidor puede cambiar su valor para evitar conflictos entre clientes.
    public NetworkVariable<bool> isHeld = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Indica si el objeto ya ha sido entregado en el contenedor y no puede recogerse de nuevo
    public NetworkVariable<bool> isDelivered = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Propiedad de conveniencia para comprobar rapidamente si el objeto puede recogerse
    public bool CanBePickedUp => !isDelivered.Value && !isHeld.Value;
}