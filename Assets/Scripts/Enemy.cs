using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum HitChannel
{
    Body,
    Spell
}

public class Enemy : MonoBehaviour
{
    public float speed = 2f;
    public int health = 1;
    public int reward = 1;
    public int leak = 1;
    public Transform[] waypoints;
    public float[] exitDist;

    public int currentWayPoint = 0;
    public int life; // Increments on enable so a pooled body is a new target.

    // Stored tail is the walked distance to the exit. Index weight made a dense road look shorter.
    public float PathLeft()
    {
        if (waypoints == null || waypoints.Length == 0)
            return float.MaxValue;
        if (currentWayPoint >= waypoints.Length)
            return 0f;
        int i = currentWayPoint;
        float tail = exitDist != null && i < exitDist.Length ? exitDist[i] : 0f;
        Transform next = waypoints[i];
        if (next == null)
            return tail;
        return Vector2.Distance(transform.position, next.position) + tail;
    }

    public bool CloserThan(Enemy other, Vector3 from)
    {
        int byLeft = PathLeft().CompareTo(other.PathLeft());
        if (byLeft != 0)
            return byLeft < 0;
        int byDist = Vector2.Distance(from, transform.position)
            .CompareTo(Vector2.Distance(from, other.transform.position));
        if (byDist != 0)
            return byDist < 0;
        return GetInstanceID() < other.GetInstanceID();
    }
    public float bodyTaken = 1f;
    public float spellTaken = 1f;
    public int attack = 1;
    public float attackEvery = 1f;
    float damageLeft;
    int burnPerTick;
    HitChannel burnChannel; // a later shot cannot relabel this burn
    float burnLeft;
    float baseSpeed;
    int baseHealth;
    float slowLeft;
    float slowScale = 1f;
    bool atExit;
    int held;
    KnightBody claimedBy;
    float attackCool;
    readonly List<KnightBody> holders = new();
    TextMeshPro status;

    public void Hold(KnightBody knight, bool on)
    {
        // Several knights can grab one enemy. Each grab stacks.
        if (knight == null)
            return;
        if (on)
        {
            if (!holders.Contains(knight))
                holders.Add(knight);
        }
        else
            holders.Remove(knight);
        held = holders.Count;
        RefreshMove();
    }

    public bool Claimed => claimedBy != null;

    public void Claim(KnightBody knight)
    {
        if (knight == null || claimedBy != null)
            return;
        claimedBy = knight;
    }

    public void ReleaseClaim(KnightBody knight)
    {
        if (knight != null && claimedBy == knight)
            claimedBy = null;
    }

    public void Hurt(int amount)
    {
        Hurt(amount, HitChannel.Body);
    }

    public void Hurt(int amount, HitChannel lane)
    {
        Take(amount * Taken(lane));
    }

    float Taken(HitChannel lane)
    {
        return lane == HitChannel.Spell ? spellTaken : bodyTaken;
    }

    public void HurtRaw(int amount)
    {
        Take(amount);
    }

    void Take(float amount)
    {
        if (health <= 0 || amount <= 0f) return;
        int lost = Bank(amount);
        if (lost > 0)
            ShowDamage(lost);
        if (health > 0) return;
        // A death behavior runs here, before gold is read and before it is spent.
        int gold = reward;
        ObjectPool.Release(gameObject);
        CoinManager.instance.UpdateCoins(gold);
    }

    int Bank(float amount)
    {
        damageLeft += amount;
        int lost = Mathf.FloorToInt(damageLeft + 0.0001f);
        if (lost > 0)
        {
            damageLeft -= lost;
            health -= lost;
        }
        return lost;
    }

    void ShowDamage(int amount)
    {
        if (amount <= 0) return;
        var go = new GameObject("Damage");
        go.transform.position = transform.position + new Vector3(Random.Range(-0.15f, 0.15f), 0.55f, 0f);
        go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        go.transform.localScale = new Vector3(-1f, 1f, 1f);
        var text = go.AddComponent<TextMeshPro>();
        text.font = TMP_Settings.defaultFontAsset;
        Debug.Assert(text.font != null, "Damage number missing font");
        text.text = "-" + amount.ToString();
        text.fontSize = 5f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 0.25f, 0.2f);
        text.sortingOrder = 20;
        text.rectTransform.sizeDelta = new Vector2(3f, 1f);
        go.AddComponent<DamageNumber>();
    }

    public void Burn(int perTick, float duration, HitChannel lane = HitChannel.Body)
    {
        if (health <= 0 || perTick <= 0 || duration <= 0f) return;
        float dealt = perTick * Taken(lane);
        float current = burnLeft > 0f ? burnPerTick * Taken(burnChannel) : 0f;
        bool stronger = dealt > current;
        if (stronger)
        {
            burnPerTick = perTick;
            burnChannel = lane;
        }
        if ((stronger || dealt == current) && duration > burnLeft)
            burnLeft = duration;
        RefreshStatus();
    }

    public void HitStatus(int perTick, float burnDuration, float scale, float slowDuration, HitChannel lane = HitChannel.Body)
    {
        bool burning = perTick > 0 && burnDuration > 0f;
        bool slowing = slowDuration > 0f && scale > 0f && scale < 1f;
        if (health <= 0 || (!burning && !slowing))
            return;
        if (burning)
            Burn(perTick, burnDuration, lane);
        if (slowing)
            Slow(scale, slowDuration);
        RefreshMove();
    }

    public void Slow(float scale, float duration)
    {
        if (health <= 0 || duration <= 0f || scale <= 0f || scale >= 1f) return;
        bool open = slowLeft <= 0f;
        bool stronger = scale < slowScale;
        if (open || stronger)
            slowScale = scale;
        if ((open || stronger || scale == slowScale) && duration > slowLeft)
            slowLeft = duration;
        RefreshStatus();
    }

    void RefreshMove()
    {
        speed = baseSpeed * (slowLeft > 0f ? slowScale : 1f);
        RefreshStatus();
    }

    void Awake()
    {
        Debug.Assert(bodyTaken > 0f, "bodyTaken must be above 0");
        Debug.Assert(spellTaken > 0f, "spellTaken must be above 0");
        Debug.Assert(attackEvery > 0f, "attackEvery must be above 0");
        baseSpeed = speed;
        baseHealth = health;
        var go = new GameObject("Status");
        go.transform.SetParent(transform, false);
        go.transform.position = transform.position + Vector3.up * 0.95f;
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        Vector3 parentScale = transform.lossyScale;
        go.transform.localScale = new Vector3(-1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);
        status = go.AddComponent<TextMeshPro>();
        status.font = TMP_Settings.defaultFontAsset;
        Debug.Assert(status.font != null, "Status missing font");
        status.fontSize = 3f;
        status.fontStyle = FontStyles.Bold;
        status.alignment = TextAlignmentOptions.Center;
        status.color = new Color(1f, 0.45f, 0.1f);
        status.sortingOrder = 20;
        status.rectTransform.sizeDelta = new Vector2(2f, 1.5f);
        status.text = "";
    }

    void OnEnable()
    {
        life++;
        damageLeft = 0f;
        health = baseHealth;
        burnPerTick = 0;
        burnChannel = HitChannel.Body;
        burnLeft = 0f;
        slowLeft = 0f;
        slowScale = 1f;
        atExit = false;
        held = 0;
        holders.Clear();
        claimedBy = null;
        attackCool = attackEvery;
        speed = baseSpeed > 0f ? baseSpeed : speed;
        currentWayPoint = 0;
        if (status != null) status.text = "";
        WaveManager.alive.Add(this);
    }

    void OnDisable()
    {
        holders.Clear();
        held = 0;
        claimedBy = null;
        WaveManager.alive.Remove(this);
    }

    void RefreshStatus()
    {
        status.text = "";
        if (burnLeft > 0f)
            status.text = "Burn";
        if (slowLeft > 0f)
            status.text += status.text.Length > 0 ? "\nSlow" : "Slow";
        if (held > 0)
            status.text += status.text.Length > 0 ? "\nDừng" : "Dừng";
    }

    void Update()
    {
        if (WaveManager.FrameOwned)
            return;
        float dt = Time.deltaTime;
        TickStatus(dt);
        if (health <= 0) return;
        TickMove(dt);
        LeakIfExit();
    }

    public void TickStatus(float dt)
    {
        bool ticking = burnLeft > 0f || slowLeft > 0f;
        BurnStep(dt);
        if (health <= 0) return;

        if (slowLeft > 0f)
        {
            slowLeft -= dt;
            if (slowLeft <= 0f)
                RefreshStatus();
        }

        if (ticking)
            RefreshMove();
    }

    public void TickMove(float dt)
    {
        if (health <= 0 || !gameObject.activeInHierarchy) return;

        attackCool -= dt;
        if (attackCool <= 0f)
        {
            attackCool = attackEvery;
            HitKnight();
        }

        if (!Enemy.UsablePath(waypoints))
        {
            atExit = true;
            return;
        }
        if (held > 0) return;

        if (Move(speed * dt))
            atExit = true;
    }

    public void LeakIfExit()
    {
        if (!atExit || health <= 0) return;
        atExit = false;
        HealthManager.Instance.UpdateHealth(-leak);
        ObjectPool.Release(gameObject);
    }

    void BurnStep(float dt)
    {
        if (burnLeft <= 0f || dt <= 0f) return;
        float step = dt < burnLeft ? dt : burnLeft;
        float amount = burnPerTick * Taken(burnChannel) * step;
        burnLeft -= step;
        if (burnLeft <= 0f)
        {
            burnPerTick = 0;
            burnChannel = HitChannel.Body;
            RefreshStatus();
        }
        Take(amount);
    }

    // Flat Z matches the move. Snap when this frame's step covers the gap, then spend the rest on the next point.
    bool Move(float budget)
    {
        while (budget > 0f && currentWayPoint < waypoints.Length)
        {
            Transform mark = waypoints[currentWayPoint];
            if (mark == null)
            {
                currentWayPoint++;
                if (currentWayPoint >= waypoints.Length)
                    return true;
                continue;
            }
            Vector3 current = transform.position;
            Vector3 dest = mark.position;
            dest.z = current.z;
            Vector3 offset = dest - current;
            float remain = offset.magnitude;
            if (remain <= budget)
            {
                transform.position = dest;
                budget -= remain;
                currentWayPoint++;
                if (currentWayPoint >= waypoints.Length)
                    return true;
                continue;
            }

            transform.position = current + offset * (budget / remain);
            return false;
        }

        return false;
    }

    public static bool UsablePath(Transform[] points)
    {
        if (points == null || points.Length == 0)
            return false;
        for (int i = 0; i < points.Length; i++)
            if (points[i] != null)
                return true;
        return false;
    }

    void HitKnight()
    {
        if (attack <= 0 || health <= 0)
            return;
        KnightBody best = null;
        float bestDist = 0.5f;
        for (int i = 0; i < holders.Count; i++)
        {
            KnightBody knight = holders[i];
            if (knight == null || knight.health <= 0)
                continue;
            float dist = Vector2.Distance(transform.position, knight.transform.position);
            if (dist > bestDist)
                continue;
            bestDist = dist;
            best = knight;
        }
        if (best != null)
            best.Hurt(attack);
    }
}

public class DamageNumber : MonoBehaviour
{
    void Start()
    {
        Destroy(gameObject, 0.6f);
    }

    void Update()
    {
        transform.position += Vector3.up * Time.deltaTime;
    }
}
