using UnityEngine;

// Interfaces preparadas para una posible expansion del proyecto con sistemas de dano y curacion.
// Actualmente no se usan en el prototipo pero estan definidas para futuras implementaciones.

public interface IInterfaces
{
    void Interface(int interfaz);
}

public interface IDamageable
{
    void Damage(int damage);
}

public interface IHeleable
{
    void Heal(int healAmont);
}