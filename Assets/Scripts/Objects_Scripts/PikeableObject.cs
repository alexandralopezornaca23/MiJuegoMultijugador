using Unity.Netcode;
using UnityEngine;

public class PikeableObject : NetworkBehaviour
{
    public int allowedTeam = 0;

    public NetworkVariable<bool> isHeld = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> isDelivered = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public bool CanBePickedUp => !isDelivered.Value && !isHeld.Value;
}