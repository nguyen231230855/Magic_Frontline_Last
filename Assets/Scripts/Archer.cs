public class Archer : Tower
{
    protected override void Arm(Projectile pr)
    {
        pr.damage = damage;
        pr.channel = channel;
        pr.burnDamage = burnDamage;
        pr.burnDuration = burnDuration;
        pr.slowScale = 1f;
        pr.slowDuration = 0f;
    }
}
