using UnityEngine;

public interface IDamageMethod
{
    void DamageTick(Enemy Target);
    void Init(float Damage, float Firerate);
}

public class TowerDamageBase : MonoBehaviour, IDamageMethod
{
    protected float Damage;
    protected float Firerate;
    protected float Delay;

    public virtual void Init(float damage, float firerate)
    {
        Damage = damage;
        Firerate = firerate;
        Delay = 1f / firerate;
    }

    public virtual void DamageTick(Enemy target)
    {
        if (!target) return;

        if (Delay > 0)
        {
            Delay -= Time.deltaTime;
            return;
        }

        GameLoopManager.EnqueDamageData(new EnemyDamageData(target, Damage, target.DamageResistance));
        Delay = 1f / Firerate;
    }

    public virtual string GetTowerType()
    {
        return GetType().Name.Replace("Damage", " Tower");
    }
}