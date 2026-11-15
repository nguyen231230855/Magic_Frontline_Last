using UnityEngine;

public class Blood : Enemy
{
    void Reset()
    {
        speed = 2.1f;
        health = 9;
        reward = 5;
        leak = 6;
        attack = 1;
        attackEvery = 1f;
        bodyTaken = 1f;
        spellTaken = 1f;
    }

    public override bool Targetable => false;
}
