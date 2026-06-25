using Unity.Netcode.Components;

// Por defecto Netcode da la autoridad de animacion al servidor, lo que causa retardo visual.
// Esta clase cambia eso: el cliente ejecuta sus animaciones de forma inmediata
// y notifica al servidor para que las replique al resto de jugadores.

public class ClientNetworkAnimator : NetworkAnimator
{
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}