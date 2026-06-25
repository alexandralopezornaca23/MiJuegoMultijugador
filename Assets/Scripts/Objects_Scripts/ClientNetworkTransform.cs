using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

// Por defecto Netcode da la autoridad de posicion al servidor, lo que causa retardo en el movimiento.
// Esta clase cambia eso: el cliente tiene autoridad sobre su propia posicion y la envia al servidor,
// eliminando el retardo y haciendo que el personaje responda de forma instantanea.

[DisallowMultipleComponent]
public class ClientNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative() => false;
}