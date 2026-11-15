using System.Collections.Generic;
using System.Globalization;
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
    public EncounterCard card;
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
    float burnPopup;
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

    public virtual bool Targetable => true;

    protected virtual bool Stealthed => false;

    public virtual void Reveal() {}

    public virtual void Hide() {}

    public virtual bool CountsSoldierDamage() => true;

    public virtual void SoldierHurt(int amount) => Hurt(amount);

    public void Hurt(int amount)
    {
        Hurt(amount, HitChannel.Body);
    }

    public void Hurt(int amount, HitChannel lane)
    {
        Take(Incoming(amount, lane, false));
    }

    protected float Taken(HitChannel lane)
    {
        return lane == HitChannel.Spell ? spellTaken : bodyTaken;
    }

    protected bool Slowed => slowLeft > 0f;

    // burn is true for a Burn tick. Subclasses change resist here.
    protected virtual float Incoming(float amount, HitChannel lane, bool burn)
    {
        return amount * Taken(lane);
    }

    void Take(float amount, bool burn = false)
    {
        if (health <= 0 || amount <= 0f) return;
        if (burn)
            ShowBurn(amount);
        else
            ShowDamage(amount);
        Bank(amount);
        if (health > 0) return;
        // A death behavior runs here, before gold is read and before it is spent.
        int gold = reward;
        ObjectPool.Release(gameObject);
        CoinManager.instance.UpdateCoins(gold);
    }

    void Bank(float amount)
    {
        damageLeft += amount;
        int lost = Mathf.FloorToInt(damageLeft + 0.0001f);
        if (lost <= 0) return;
        damageLeft -= lost;
        health -= lost;
    }

    void ShowBurn(float amount)
    {
        burnPopup += amount;
        int whole = Mathf.FloorToInt(burnPopup + 0.0001f);
        burnPopup -= whole;
        for (int i = 0; i < whole; i++)
            ShowDamage(1f);
    }

    void ShowDamage(float amount)
    {
        if (amount <= 0f) return;
        Float("-" + amount.ToString("0.##", CultureInfo.InvariantCulture), new Color(1f, 0.25f, 0.2f));
    }

    void Float(string word, Color ink)
    {
        var go = new GameObject("Damage");
        go.transform.position = transform.position + new Vector3(Random.Range(-0.15f, 0.15f), 0.55f, 0f);
        var label = go.AddComponent<TextMeshPro>();
        label.font = TMP_Settings.defaultFontAsset;
        Debug.Assert(label.font != null, "Damage number missing font");
        label.text = word;
        label.fontSize = 5f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = ink;
        label.sortingOrder = 20;
        label.rectTransform.sizeDelta = new Vector2(3f, 1f);
        go.AddComponent<DamageNumber>();
    }

    public void Burn(int perTick, float duration, HitChannel lane = HitChannel.Body)
    {
        if (health <= 0 || perTick <= 0 || duration <= 0f) return;
        float dealt = Incoming(perTick, lane, true);
        float current = burnLeft > 0f ? Incoming(burnPerTick, burnChannel, true) : 0f;
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
        // A child of scale 5 gets localScale 0.2. That scale fills the SDF quad, so Burn is a gray plate and Slow has no letters.
        go.transform.SetParent(null);
        go.transform.localScale = Vector3.one;
        status = go.AddComponent<TextMeshPro>();
        status.font = TMP_Settings.defaultFontAsset;
        Debug.Assert(status.font != null, "Status missing font");
        // Mobile SDF fills the glyph quad. The letters show, and the rest of the quad is the gray bar.
        status.fontSharedMaterial = LabelInk(status);
        status.fontSize = 3f;
        status.fontStyle = FontStyles.Normal;
        status.alignment = TextAlignmentOptions.Center;
        status.color = new Color(1f, 0.45f, 0.1f);
        status.sortingOrder = 20;
        status.rectTransform.sizeDelta = new Vector2(2f, 1.5f);
        status.text = "";
        PlaceStatus();
    }

    static Material labelInk;

    static Material LabelInk(TextMeshPro label)
    {
        if (labelInk != null) return labelInk;
        labelInk = new Material(label.fontSharedMaterial);
        // Shader.Find misses this shader: nothing else references it, so it is not loaded.
        Material src = Resources.Load<Material>("StatusInk");
        Shader shader = src != null ? src.shader : null;
        Debug.Assert(shader != null, "Status text missing SSD shader");
        if (shader != null)
            labelInk.shader = shader;
        labelInk.EnableKeyword("UNITY_UI_ALPHACLIP");
        return labelInk;
    }

    void LateUpdate()
    {
        PlaceStatus();
    }

    void PlaceStatus()
    {
        if (status == null) return;
        status.transform.position = transform.position + Vector3.up * 0.95f;
    }

    void OnEnable()
    {
        life++;
        damageLeft = 0f;
        burnPopup = 0f;
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
        if (status != null)
        {
            status.gameObject.SetActive(true);
            status.text = "";
        }
        OnSpawn();
        WaveManager.alive.Add(this);
    }

    protected virtual void OnSpawn() {}

    void OnDisable()
    {
        holders.Clear();
        held = 0;
        claimedBy = null;
        if (status != null)
            status.gameObject.SetActive(false);
        WaveManager.alive.Remove(this);
    }

    void OnDestroy()
    {
        if (status == null) return;
        GameObject go = status.gameObject;
        status = null;
        if (Application.isPlaying)
            Destroy(go);
        else
            DestroyImmediate(go);
    }

    protected void RefreshStatus()
    {
        status.text = "";
        if (Stealthed)
            status.text += status.text.Length > 0 ? "\nTàng" : "Tàng";
        if (burnLeft > 0f)
            status.text += status.text.Length > 0 ? "\nBurn" : "Burn";
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
        float amount = Incoming(burnPerTick * step, burnChannel, true);
        burnLeft -= step;
        if (burnLeft <= 0f)
        {
            burnPerTick = 0;
            burnChannel = HitChannel.Body;
            RefreshStatus();
        }
        Take(amount, true);
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
        if (best == null)
            return;
        best.Hurt(attack);
        Animator anim = GetComponent<Animator>();
        if (anim != null)
            anim.SetTrigger("Attack");
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
