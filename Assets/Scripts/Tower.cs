using UnityEngine;

[System.Serializable]
public class TowerUpgradeStages
{
    public float range;
    public float fireRate;
    public Sprite sprite;
    public int price;
    public int damage = 1;
    public HitChannel channel;
    public int burnDamage;
    public float burnDuration;
    public float slowScale = 1f;
    public float slowDuration;
}
public class Tower : MonoBehaviour
{
    public float range = 3f;
    public float fireRate = 1f;
    public GameObject projectilePrefab;
    public Transform firePoint;
    public AudioClip shootSfx;
    public float shootVolume = 1f;

    public int damage = 1;
    public HitChannel channel;
    public int burnDamage;
    public float burnDuration;
    public float slowScale = 1f;
    public float slowDuration;

    public TowerUpgradeStages[] upgradeStages;
    public int upgradeStage = 0;
    private SpriteRenderer sr;
    public GameObject towerUpgradeUIPrefab;
    private GameObject currentUI;

    public GameObject cloudPS;

    public int towerPrice = 1;

    private float fireCooldown = 0f;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        Instantiate(cloudPS, transform.position, Quaternion.identity);
    }
    void Update()
    {
        if (WaveManager.FrameOwned)
            return;
        Tick();
    }

    public void Tick()
    {
        // Barrack has no projectile. Its knights live on Barrack.
        if (projectilePrefab == null)
            return;

        fireCooldown -= Time.deltaTime;

        Enemy target = FindBestTarget();

        if (target != null && fireCooldown <= 0f)
        {
            Shoot(target);
            fireCooldown = 1f / fireRate;
        }
    }

    Enemy FindBestTarget()
    {
        Enemy best = null;
        foreach (Enemy e in WaveManager.alive)
        {
            if (Vector2.Distance(transform.position, e.transform.position) > range)
                continue;
            if (best == null || e.CloserThan(best, transform.position))
                best = e;
        }

        return best;
    }

    void Shoot(Enemy target)
    {
        GameObject p = ObjectPool.Get(projectilePrefab, firePoint.position, Quaternion.identity);
        Projectile pr = p.GetComponent<Projectile>();
        pr.damage = damage;
        pr.channel = channel;
        pr.burnDamage = burnDamage;
        pr.burnDuration = burnDuration;
        pr.slowScale = slowScale;
        pr.slowDuration = slowDuration;
        pr.target = target.transform;
        pr.life = target.life;
        if (shootSfx != null)
            AudioManager.Instance.PlaySFX(shootSfx, shootVolume);
    }

    public bool TryUpgrade()
    {
        if (upgradeStage < 0 || upgradeStage >= upgradeStages.Length) return false;
        TowerUpgradeStages currentUpgradeStage = upgradeStages[upgradeStage];
        if (!CoinManager.instance.TrySpend(currentUpgradeStage.price)) return false;

        range = currentUpgradeStage.range;
        fireRate = currentUpgradeStage.fireRate;
        if (currentUpgradeStage.sprite != null)
            sr.sprite = currentUpgradeStage.sprite;
        damage = currentUpgradeStage.damage;
        channel = currentUpgradeStage.channel;
        burnDamage = currentUpgradeStage.burnDamage;
        burnDuration = currentUpgradeStage.burnDuration;
        slowScale = currentUpgradeStage.slowScale;
        slowDuration = currentUpgradeStage.slowDuration;
        upgradeStage++;
        Instantiate(cloudPS, transform.position, Quaternion.identity);
        return true;
    }

    private void OnMouseDown()
    {
        if (upgradeStage >= upgradeStages.Length) return;
        Canvas hud = LevelCanvasLoader.Hud;
        Debug.Assert(hud != null, "Level HUD missing");
        if (hud == null) return;
        if (currentUI == null)
            currentUI = Instantiate(towerUpgradeUIPrefab, hud.transform);

        TowerUpgradeUI currentUpgradeUI = currentUI.GetComponent<TowerUpgradeUI>();
        currentUpgradeUI.tower = this;

        Camera cam = hud.renderMode == RenderMode.ScreenSpaceOverlay ? null : hud.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            hud.transform as RectTransform, Input.mousePosition, cam, out Vector2 local))
            currentUI.transform.localPosition = new Vector3(local.x + 50f, local.y - 50f, 0f);

        currentUpgradeUI.priceTxt.text = upgradeStages[upgradeStage].price.ToString();
    }
}
