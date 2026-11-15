public class Wizard : Tower
{
    protected override void Arm(Projectile pr)
    {
        pr.damage = damage;
        pr.channel = channel;
        pr.burnDamage = 0;
        pr.burnDuration = 0f;
        pr.slowScale = slowScale;
        pr.slowDuration = slowDuration;
    }
}
