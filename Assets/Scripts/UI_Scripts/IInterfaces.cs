using UnityEngine;

public interface IInterfaces
{
    //int Health { get; set; }
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
