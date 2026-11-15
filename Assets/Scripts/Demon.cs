using UnityEngine;

public class Demon : Enemy
{
    const float SlowHurt = 1.5f;

    void Reset()
    {
        speed = 2f;
        health = 17;
        reward = 8;
        leak = 8;
        attack = 3;
        attackEvery = 1f;
        bodyTaken = 0.75f;
        spellTaken = 1f;
    }

    protected override float Incoming(float amount, HitChannel lane, bool burn)
    {
        if (burn)
            return 0f;
        amount *= Taken(lane);
        if (Slowed)
            amount *= SlowHurt;
        return amount;
    }
}
