using UnityEngine;

// Etiqueta que se anade a los objetos del escenario para restringir que equipo puede recogerlos.
// 0 = cualquier equipo, 1 = solo Rosa, 2 = solo Azul

public class TeamPickup : MonoBehaviour
{
    [Header("0 = ambos, 1 = solo rosa, 2 = solo azul")]
    public int allowedTeam = 0;
}