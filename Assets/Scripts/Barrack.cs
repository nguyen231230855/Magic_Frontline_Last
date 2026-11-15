using UnityEngine;
using System.Collections.Generic;

public class Barrack : MonoBehaviour
{
    public int[] damage = { 1, 1, 2 };
    public Sprite[] knightSprites;
    public float attackEvery = 1.15f;
    public float moveSpeed = 2.2f;
    public float melee = 0.45f;
    public AudioClip sword;
    public int[] health = { 4, 6, 8 };
    public float respawn = 10f;

    Tower tower;
    Knight[] men;

    void Awake()
    {
        tower = GetComponent<Tower>();
        Debug.Assert(tower != null, "Barrack needs Tower");
        Debug.Assert(knightSprites != null && knightSprites.Length >= 3, "Barrack needs 3 knight sprites");
        Debug.Assert(damage != null && damage.Length >= 3, "Barrack needs 3 damage steps");
        Debug.Assert(health != null && health.Length >= 3, "Barrack needs 3 health steps");
        Debug.Assert(respawn > 0f, "respawn must be above 0");

        men = new Knight[3];
        float[] x = { -0.42f, 0f, 0.42f };
        for (int i = 0; i < men.Length; i++)
        {
            var go = new GameObject("Knight");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 1;
            var body = go.AddComponent<KnightBody>();
            body.health = health[0];
            men[i] = new Knight(go.transform, sr, body, transform.position + new Vector3(x[i], -0.95f, 0f));
        }
    }

    void Update()
    {
        if (WaveManager.FrameOwned)
            return;
        Tick();
    }

    public void Tick()
    {
        int level = Mathf.Clamp(tower.upgradeStage, 0, knightSprites.Length - 1);
        for (int i = 0; i < men.Length; i++)
            men[i].Tick(this, level, WaveManager.alive);
    }

    class Knight
    {
        readonly Transform transform;
        readonly SpriteRenderer sr;
        readonly Vector3 post;
        readonly KnightBody body;
        Enemy prey;
        int preyLife;
        float cool;
        bool gripping;
        int maxHp;
        float deadLeft;

        public Knight(Transform transform, SpriteRenderer sr, KnightBody body, Vector3 post)
        {
            this.transform = transform;
            this.sr = sr;
            this.body = body;
            this.post = post;
            maxHp = body.health;
            transform.position = post;
        }

        public Enemy Prey => prey;

        public void Tick(Barrack barrack, int level, List<Enemy> enemies)
        {
            Sprite sprite = barrack.knightSprites[level];
            if (sr.sprite != sprite)
                sr.sprite = sprite;

            int nextMax = barrack.health[Mathf.Clamp(level, 0, barrack.health.Length - 1)];
            if (body.health > 0 && nextMax > maxHp)
                body.health += nextMax - maxHp;
            maxHp = nextMax;
            body.FadeHurt(Time.deltaTime);
            if (body.health <= 0)
            {
                Forget();
                if (sr.enabled)
                {
                    sr.enabled = false;
                    deadLeft = barrack.respawn;
                }
                deadLeft -= Time.deltaTime;
                if (deadLeft > 0f)
                    return;
                body.health = maxHp;
                sr.enabled = true;
                sr.color = Color.white;
                transform.position = post;
            }

            if (prey != null && (prey.life != preyLife || prey.health <= 0 || !prey.gameObject.activeInHierarchy || !Sees(barrack, prey)))
                Forget();
            if (prey == null)
            {
                prey = Pick(barrack, enemies);
                if (prey != null)
                {
                    preyLife = prey.life;
                    prey.Claim(body);
                }
            }

            Vector3 goal = prey != null ? prey.transform.position : post;
            goal.z = transform.position.z;
            float dist = Vector2.Distance(transform.position, goal);
            if (prey != null && !gripping && dist <= barrack.melee)
                Grab();
            if (gripping)
            {
                cool -= Time.deltaTime;
                if (cool > 0f)
                    return;
                int hit = barrack.damage[Mathf.Clamp(level, 0, barrack.damage.Length - 1)];
                if (hit > 0 && prey.health > 0 && barrack.sword != null && AudioManager.Instance != null)
                    AudioManager.Instance.PlaySFX(barrack.sword);
                prey.Hurt(hit);
                cool = barrack.attackEvery;
                if (prey.life != preyLife || prey.health <= 0 || !prey.gameObject.activeInHierarchy || !Sees(barrack, prey))
                    Forget();
                return;
            }

            if (dist <= 0.02f)
                return;
            transform.position = Vector3.MoveTowards(transform.position, goal, barrack.moveSpeed * Time.deltaTime);
            sr.flipX = goal.x < transform.position.x;
        }

        void Grab()
        {
            if (gripping || prey == null)
                return;
            prey.Hold(body, true);
            gripping = true;
        }

        void Drop()
        {
            if (!gripping)
                return;
            if (prey != null && prey.gameObject.activeInHierarchy && prey.life == preyLife)
                prey.Hold(body, false);
            gripping = false;
        }

        void Forget()
        {
            Drop();
            if (prey != null && prey.gameObject.activeInHierarchy && prey.life == preyLife)
                prey.ReleaseClaim(body);
            prey = null;
        }

        bool Sees(Barrack barrack, Enemy enemy)
        {
            return Vector2.Distance(barrack.transform.position, enemy.transform.position) <= barrack.tower.range;
        }

        Enemy Pick(Barrack barrack, List<Enemy> enemies)
        {
            Enemy free = null;
            foreach (Enemy enemy in enemies)
            {
                if (!enemy.gameObject.activeInHierarchy || enemy.health <= 0 || enemy.Claimed)
                    continue;
                float dist = Vector2.Distance(barrack.transform.position, enemy.transform.position);
                if (dist > barrack.tower.range)
                    continue;
                if (free == null || enemy.CloserThan(free, barrack.transform.position))
                    free = enemy;
            }
            return free;
        }
    }
}

public class KnightBody : MonoBehaviour
{
    public int health;
    float hurtLeft;

    public void Hurt(int amount)
    {
        if (amount <= 0 || health <= 0)
            return;
        health -= amount;
        hurtLeft = 0.15f;
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.color = new Color(1f, 0.45f, 0.45f);
    }

    public void FadeHurt(float dt)
    {
        if (health <= 0 || hurtLeft <= 0f)
            return;
        hurtLeft -= dt;
        if (hurtLeft > 0f)
            return;
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.color = Color.white;
    }
}
