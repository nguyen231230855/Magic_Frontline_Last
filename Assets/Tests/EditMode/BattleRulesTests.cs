using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

public class BattleRulesTests
{
    [TearDown]
    public void ClearPool()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
        var pools = (Dictionary<GameObject, Stack<GameObject>>)typeof(ObjectPool)
            .GetField("pools", flags).GetValue(null);
        var prefabs = (Dictionary<GameObject, GameObject>)typeof(ObjectPool)
            .GetField("prefabs", flags).GetValue(null);
        pools.Clear();
        prefabs.Clear();
        WaveManager.alive.Clear();
    }

    [Test]
    public void TrySpend_MoreThanCoins_ReturnsFalse()
    {
        var root = new GameObject("coins");
        root.SetActive(false);
        var coins = root.AddComponent<CoinManager>();
        coins.coins = 3;
        try
        {
            Assert.IsFalse(coins.TrySpend(4));
            Assert.AreEqual(3, coins.coins);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void Release_Twice_PushesOnce()
    {
        var prefab = new GameObject("pool-prefab");
        GameObject first = ObjectPool.Get(prefab, Vector3.zero, Quaternion.identity);
        ObjectPool.Release(first);
        ObjectPool.Release(first);
        GameObject again = ObjectPool.Get(prefab, Vector3.zero, Quaternion.identity);
        GameObject extra = ObjectPool.Get(prefab, Vector3.zero, Quaternion.identity);
        try
        {
            Assert.AreSame(first, again);
            Assert.AreNotSame(first, extra);
        }
        finally
        {
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(extra);
            Object.DestroyImmediate(prefab);
        }
    }

    [Test]
    public void Burn_WhileSlowed_KeepsBurn()
    {
        var root = new GameObject("enemy");
        root.SetActive(false);
        var enemy = root.AddComponent<Enemy>();
        enemy.SendMessage("Awake");
        enemy.Slow(0.5f, 2f);
        enemy.Burn(1, 2f);
        float burnLeft = (float)typeof(Enemy)
            .GetField("burnLeft", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(enemy);
        try
        {
            Assert.Greater(burnLeft, 0f);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void HitStatus_Both_KeepsEachAndScalesBurn()
    {
        var root = new GameObject("enemy");
        root.SetActive(false);
        var enemy = root.AddComponent<Enemy>();
        enemy.health = 10;
        enemy.bodyTaken = 2f;
        enemy.speed = 4f;
        enemy.SendMessage("Awake");
        enemy.HitStatus(3, 5f, 0.5f, 4f);
        float burnLeft = (float)typeof(Enemy).GetField("burnLeft", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(enemy);
        float slowLeft = (float)typeof(Enemy).GetField("slowLeft", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(enemy);
        try
        {
            Assert.Greater(burnLeft, 0f);
            Assert.Greater(slowLeft, 0f);
            Assert.AreEqual(2f, enemy.speed, 0.0001f);
            StepBurn(enemy, 1f);
            Assert.AreEqual(4, enemy.health);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void HitStatus_FireThenIce_KeepsStrongerLonger()
    {
        var root = new GameObject("enemy");
        root.SetActive(false);
        var enemy = root.AddComponent<Enemy>();
        enemy.health = 10;
        enemy.bodyTaken = 2f;
        enemy.speed = 4f;
        enemy.SendMessage("Awake");
        try
        {
            enemy.HitStatus(0, 0f, 0.5f, 2f);
            enemy.HitStatus(2, 3f, 1f, 0f);
            Assert.AreEqual(3f, Left(enemy, "burnLeft"), 0.0001f);
            Assert.AreEqual(2f, Left(enemy, "slowLeft"), 0.0001f);
            Assert.AreEqual(2, Tick(enemy));
            Assert.AreEqual(HitChannel.Body, Lane(enemy));
            Assert.AreEqual(2f, enemy.speed, 0.0001f);

            enemy.HitStatus(1, 5f, 0.8f, 4f);
            Assert.AreEqual(3f, Left(enemy, "burnLeft"), 0.0001f);
            Assert.AreEqual(2, Tick(enemy));
            Assert.AreEqual(HitChannel.Body, Lane(enemy));
            Assert.AreEqual(2f, Left(enemy, "slowLeft"), 0.0001f);
            Assert.AreEqual(0.5f, Scale(enemy), 0.0001f);
            Assert.AreEqual(2f, enemy.speed, 0.0001f);

            enemy.HitStatus(2, 4f, 0.5f, 3f);
            Assert.AreEqual(4f, Left(enemy, "burnLeft"), 0.0001f);
            Assert.AreEqual(2, Tick(enemy));
            Assert.AreEqual(3f, Left(enemy, "slowLeft"), 0.0001f);
            Assert.AreEqual(0.5f, Scale(enemy), 0.0001f);

            enemy.HitStatus(4, 1f, 0.25f, 1f);
            Assert.AreEqual(4, Tick(enemy));
            Assert.AreEqual(4f, Left(enemy, "burnLeft"), 0.0001f);
            Assert.AreEqual(0.25f, Scale(enemy), 0.0001f);
            Assert.AreEqual(3f, Left(enemy, "slowLeft"), 0.0001f);
            Assert.AreEqual(1f, enemy.speed, 0.0001f);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void BurnStep_PartialSecond_BanksThenWhole()
    {
        var root = new GameObject("enemy");
        root.SetActive(false);
        var enemy = root.AddComponent<Enemy>();
        enemy.health = 5;
        enemy.SendMessage("Awake");
        try
        {
            enemy.Burn(1, 2f);
            StepBurn(enemy, 2f);
            Assert.AreEqual(3, enemy.health);
            Assert.AreEqual(0, Tick(enemy));
            Assert.AreEqual(0f, Left(enemy, "burnLeft"), 0.0001f);

            enemy.Burn(1, 0.5f);
            StepBurn(enemy, 0.5f);
            Assert.AreEqual(3, enemy.health);
            Assert.AreEqual(0.5f, Banked(enemy), 0.0001f);
            Assert.AreEqual(0, Tick(enemy));

            enemy.Burn(1, 0.5f);
            StepBurn(enemy, 0.5f);
            Assert.AreEqual(2, enemy.health);
            Assert.AreEqual(0f, Banked(enemy), 0.0001f);

            enemy.Burn(1, 1f);
            StepBurn(enemy, 0.25f);
            enemy.Burn(1, 1f);
            StepBurn(enemy, 0.25f);
            Assert.AreEqual(2, enemy.health);
            Assert.AreEqual(0.5f, Banked(enemy), 0.0001f);
            Assert.AreEqual(0.75f, Left(enemy, "burnLeft"), 0.0001f);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void Orc_BodyBanksQuiet_SpellPaysAndKeepsLane()
    {
        var root = new GameObject("orc");
        root.SetActive(false);
        var enemy = root.AddComponent<Enemy>();
        enemy.health = 5;
        enemy.bodyTaken = 0.75f;
        enemy.spellTaken = 1f;
        enemy.SendMessage("Awake");
        int pops = Popups();
        try
        {
            enemy.Hurt(1, HitChannel.Body);
            Assert.AreEqual(5, enemy.health);
            Assert.AreEqual(0.75f, Banked(enemy), 0.0001f);
            Assert.AreEqual(pops, Popups());

            enemy.Hurt(1, HitChannel.Spell);
            Assert.AreEqual(4, enemy.health);
            Assert.AreEqual(0.75f, Banked(enemy), 0.0001f);
            Assert.AreEqual(pops + 1, Popups());

            enemy.Burn(1, 2f, HitChannel.Spell);
            enemy.Burn(1, 3f, HitChannel.Body);
            Assert.AreEqual(HitChannel.Spell, Lane(enemy));
            Assert.AreEqual(2f, Left(enemy, "burnLeft"), 0.0001f);
            StepBurn(enemy, 1f);
            Assert.AreEqual(3, enemy.health);
            Assert.AreEqual(HitChannel.Spell, Lane(enemy));
        }
        finally
        {
            foreach (var popup in Object.FindObjectsByType<DamageNumber>(FindObjectsSortMode.None))
                Object.DestroyImmediate(popup.gameObject);
            Object.DestroyImmediate(root);
        }
    }

    static int Popups()
    {
        return Object.FindObjectsByType<DamageNumber>(FindObjectsSortMode.None).Length;
    }

    static void StepBurn(Enemy enemy, float dt)
    {
        typeof(Enemy).GetMethod("BurnStep", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(enemy, new object[] { dt });
    }

    static float Banked(Enemy enemy)
    {
        return (float)typeof(Enemy).GetField("damageLeft", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(enemy);
    }

    static float Left(Enemy enemy, string field)
    {
        return (float)typeof(Enemy).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(enemy);
    }

    static int Tick(Enemy enemy)
    {
        return (int)typeof(Enemy).GetField("burnPerTick", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(enemy);
    }

    static HitChannel Lane(Enemy enemy)
    {
        return (HitChannel)typeof(Enemy).GetField("burnChannel", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(enemy);
    }

    static float Scale(Enemy enemy)
    {
        return (float)typeof(Enemy).GetField("slowScale", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(enemy);
    }

    [Test]
    public void Move_StepPastPoint_ContinuesOnPlane()
    {
        var root = new GameObject("enemy");
        root.SetActive(false);
        var enemy = root.AddComponent<Enemy>();
        var a = new GameObject("a");
        var b = new GameObject("b");
        a.transform.position = new Vector3(1f, 0f, 4f);
        b.transform.position = new Vector3(1f, 1f, 4f);
        enemy.waypoints = new[] { a.transform, b.transform };
        enemy.transform.position = new Vector3(0f, 0f, 1f);
        try
        {
            bool done = (bool)typeof(Enemy)
                .GetMethod("Move", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(enemy, new object[] { 1.5f });
            Assert.IsFalse(done);
            Assert.AreEqual(1, enemy.currentWayPoint);
            Vector3 at = enemy.transform.position;
            Assert.AreEqual(1f, at.x, 0.0001f);
            Assert.AreEqual(0.5f, at.y, 0.0001f);
            Assert.AreEqual(1f, at.z, 0.0001f);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
        }
    }

    [Test]
    public void SameLife_AfterReuse_RejectsOldShot()
    {
        var root = new GameObject("enemy");
        root.SetActive(false);
        var enemy = root.AddComponent<Enemy>();
        var shot = new GameObject("shot");
        shot.SetActive(false);
        var pr = shot.AddComponent<Projectile>();
        pr.target = enemy.transform;
        root.SetActive(true);
        pr.life = enemy.life;
        root.SetActive(false);
        root.SetActive(true);
        try
        {
            bool hit = (bool)typeof(Projectile)
                .GetMethod("SameLife", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(pr, new object[] { enemy });
            Assert.AreEqual(pr.life + 1, enemy.life);
            Assert.IsFalse(hit);
        }
        finally
        {
            Object.DestroyImmediate(shot);
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void DeadTarget_GlidesToLastPoint_WithoutDamage()
    {
        var enemyGo = new GameObject("enemy");
        enemyGo.SetActive(false);
        var enemy = enemyGo.AddComponent<Enemy>();
        enemy.health = 5;
        var prefab = new GameObject("shot-prefab");
        prefab.SetActive(false);
        prefab.AddComponent<Projectile>();
        var fx = new GameObject("fx");
        GameObject live = ObjectPool.Get(prefab, Vector3.zero, Quaternion.identity);
        Projectile shot = live.GetComponent<Projectile>();
        shot.hitPS = fx;
        shot.hitSFX = null;
        shot.damage = 4;
        enemyGo.transform.position = new Vector3(1f, 0f, 0f);
        enemyGo.SetActive(true);
        shot.target = enemy.transform;
        shot.life = enemy.life;
        enemyGo.SetActive(false);
        enemyGo.SetActive(true);
        var audio = AudioManager.Instance;
        AudioManager.Instance = null;
        try
        {
            live.SendMessage("Update");
            Assert.IsTrue(live.activeSelf);
            Assert.AreEqual(5, enemy.health);
            Assert.IsNull(GameObject.Find("fx(Clone)"));

            enemyGo.transform.position = new Vector3(10f, 0f, 0f);
            live.transform.position = new Vector3(1f, 0f, 0f);
            live.SendMessage("Update");
            Assert.IsFalse(live.activeSelf);
            Assert.AreEqual(5, enemy.health);
            Assert.IsNotNull(GameObject.Find("fx(Clone)"));
        }
        finally
        {
            AudioManager.Instance = audio;
            var clone = GameObject.Find("fx(Clone)");
            if (clone != null)
                Object.DestroyImmediate(clone);
            Object.DestroyImmediate(live);
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(fx);
            Object.DestroyImmediate(enemyGo);
        }
    }

    [Test]
    public void FastStep_SegmentHit_SnapsAndHurts()
    {
        var enemyGo = new GameObject("enemy");
        enemyGo.SetActive(false);
        var enemy = enemyGo.AddComponent<Enemy>();
        enemy.health = 5;
        enemyGo.transform.position = new Vector3(1f, 0f, 0f);
        var prefab = new GameObject("shot-prefab");
        prefab.SetActive(false);
        prefab.AddComponent<Projectile>();
        GameObject live = ObjectPool.Get(prefab, Vector3.zero, Quaternion.identity);
        Projectile shot = live.GetComponent<Projectile>();
        shot.speed = 8f;
        shot.damage = 1;
        shot.hitPS = null;
        shot.hitSFX = null;
        enemyGo.SetActive(true);
        shot.target = enemy.transform;
        shot.life = enemy.life;
        var audio = AudioManager.Instance;
        AudioManager.Instance = null;
        try
        {
            typeof(Projectile).GetMethod("Advance", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(shot, new object[] { 1f });
            Assert.IsFalse(live.activeSelf);
            Assert.AreEqual(4, enemy.health);
            Assert.AreEqual(1f, live.transform.position.x, 0.0001f);
            Assert.AreEqual(0f, live.transform.position.y, 0.0001f);
        }
        finally
        {
            AudioManager.Instance = audio;
            Object.DestroyImmediate(live);
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(enemyGo);
        }
    }

    [Test]
    public void Hud_WaitingLane_KeepsButtonAndIgnoresName()
    {
        var highGo = new GameObject("WaveManager (2)");
        highGo.SetActive(false);
        var lowGo = new GameObject("WaveManager");
        lowGo.SetActive(false);
        var high = highGo.AddComponent<WaveManager>();
        var low = lowGo.AddComponent<WaveManager>();
        var highSpawn = new GameObject("high");
        var lowSpawn = new GameObject("low");
        highSpawn.transform.position = new Vector3(1f, 3f, 0f);
        lowSpawn.transform.position = new Vector3(0f, 0f, 0f);
        high.wayPoints = new[] { highSpawn.transform };
        low.wayPoints = new[] { lowSpawn.transform };
        high.waves = new WaveData[4];
        low.waves = new WaveData[4];
        var lanes = new List<WaveManager> { low, high };
        lanes.Sort(WaveLaneHud.CompareSpawn);
        SetWave(high, 3);
        try
        {
            Assert.AreSame(high, lanes[0]);
            Assert.IsTrue(WaveLaneHud.AnyWaiting(lanes));
            SetWave(low, 3);
            Assert.IsFalse(WaveLaneHud.AnyWaiting(lanes));
        }
        finally
        {
            Object.DestroyImmediate(highGo);
            Object.DestroyImmediate(lowGo);
            Object.DestroyImmediate(highSpawn);
            Object.DestroyImmediate(lowSpawn);
        }
    }

    static void SetWave(WaveManager lane, int index)
    {
        typeof(WaveManager).GetField("currentWaveIndex", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(lane, index);
    }

    [Test]
    public void PauseCount_NestedClose_KeepsFastFlag()
    {
        var go = new GameObject("speed");
        var speed = go.AddComponent<SpeedUpManager>();
        speed.speedUp = true;
        SpeedUpManager.ClearPause();
        try
        {
            SpeedUpManager.PushPause();
            SpeedUpManager.PushPause();
            speed.ToggleSpeedUp();
            Assert.IsTrue(speed.speedUp);
            Assert.AreEqual(0f, Time.timeScale);
            SpeedUpManager.PopPause();
            Assert.AreEqual(0f, Time.timeScale);
            SpeedUpManager.PopPause();
            Assert.AreEqual(2f, Time.timeScale);
        }
        finally
        {
            speed.speedUp = false;
            SpeedUpManager.ClearPause();
            Time.timeScale = 1f;
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void Swing_MissOrHit_RearmsAndSkipsStrangers()
    {
        var enemyGo = new GameObject("enemy");
        enemyGo.SetActive(false);
        var enemy = enemyGo.AddComponent<Enemy>();
        enemy.attack = 1;
        enemy.attackEvery = 1.5f;
        enemy.health = 3;
        var heldGo = new GameObject("held");
        var heldSr = heldGo.AddComponent<SpriteRenderer>();
        var held = heldGo.AddComponent<KnightBody>();
        held.health = 4;
        heldGo.transform.position = new Vector3(0.2f, 0f, 0f);
        var farGo = new GameObject("far");
        var far = farGo.AddComponent<KnightBody>();
        far.health = 4;
        farGo.transform.position = new Vector3(5f, 0f, 0f);
        var post = new GameObject("post");
        post.transform.position = new Vector3(0f, 3f, 0f);
        enemy.waypoints = new[] { post.transform };
        enemyGo.SetActive(true);
        enemy.Hold(held, true);
        SetCool(enemy, 0f);
        try
        {
            enemyGo.SendMessage("Update");
            Assert.AreEqual(3, held.health);
            Assert.AreEqual(4, far.health);
            Assert.AreEqual(1.5f, Cool(enemy));
            Assert.AreNotEqual(Color.white, heldSr.color);
            held.FadeHurt(1f);
            Assert.AreEqual(Color.white, heldSr.color);
            enemy.Hold(held, false);
            SetCool(enemy, 0f);
            enemyGo.SendMessage("Update");
            Assert.AreEqual(3, held.health);
            Assert.AreEqual(1.5f, Cool(enemy));
        }
        finally
        {
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(heldGo);
            Object.DestroyImmediate(farGo);
            Object.DestroyImmediate(post);
        }
    }

    static void SetCool(Enemy enemy, float value)
    {
        typeof(Enemy).GetField("attackCool", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(enemy, value);
    }

    static float Cool(Enemy enemy)
    {
        return (float)typeof(Enemy).GetField("attackCool", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(enemy);
    }

    [Test]
    public void Kill_PoolsBeforeCoins()
    {
        var coinsGo = new GameObject("coins");
        coinsGo.SetActive(false);
        CoinManager.instance = coinsGo.AddComponent<CoinManager>();
        var prefab = new GameObject("enemy-prefab");
        prefab.SetActive(false);
        var defined = prefab.AddComponent<Enemy>();
        defined.health = 1;
        defined.reward = 5;
        defined.leak = 2;
        GameObject live = ObjectPool.Get(prefab, Vector3.zero, Quaternion.identity);
        Enemy enemy = live.GetComponent<Enemy>();
        try
        {
            Assert.Throws<System.NullReferenceException>(() => enemy.Hurt(1));
            Assert.IsFalse(live.activeSelf);
            Assert.IsFalse(WaveManager.alive.Contains(enemy));
            Assert.AreEqual(2, enemy.leak);
            Assert.AreEqual(5, enemy.reward);
        }
        finally
        {
            CoinManager.instance = null;
            Object.DestroyImmediate(live);
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(coinsGo);
        }
    }

    [Test]
    public void CloseOnClick_WithoutEventSystem_StaysOpen()
    {
        bool close = (bool)typeof(TowerUpgradeUI)
            .GetMethod("CloseOnClick", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { null });
        Assert.IsFalse(close);
    }

    [Test]
    public void Hit_NullFx_ReturnsToPool()
    {
        var prefab = new GameObject("shot-prefab");
        prefab.SetActive(false);
        prefab.AddComponent<Projectile>();
        GameObject live = ObjectPool.Get(prefab, Vector3.zero, Quaternion.identity);
        Projectile shot = live.GetComponent<Projectile>();
        shot.hitPS = null;
        shot.hitSFX = null;
        var enemyGo = new GameObject("enemy");
        enemyGo.SetActive(false);
        var enemy = enemyGo.AddComponent<Enemy>();
        enemy.health = 5;
        var audio = AudioManager.Instance;
        AudioManager.Instance = null;
        enemyGo.SetActive(true);
        shot.target = enemy.transform;
        shot.life = enemy.life;
        shot.damage = 1;
        shot.transform.position = enemy.transform.position;
        try
        {
            live.SendMessage("Update");
            Assert.IsFalse(live.activeSelf);
            Assert.AreEqual(4, enemy.health);
        }
        finally
        {
            AudioManager.Instance = audio;
            Object.DestroyImmediate(live);
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(enemyGo);
        }
    }

    [Test]
    public void Boost_DifferentGain_DoesNotReuseCopy()
    {
        var go = new GameObject("audio");
        go.SetActive(false);
        var audio = go.AddComponent<AudioManager>();
        var clip = AudioClip.Create("hit", 8, 1, 44100, false);
        var samples = new float[8];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = 0.25f;
        clip.SetData(samples, 0);
        var boost = typeof(AudioManager).GetMethod("Boost", BindingFlags.NonPublic | BindingFlags.Instance);
        try
        {
            var low = (AudioClip)boost.Invoke(audio, new object[] { clip, 1.5f });
            var lowAgain = (AudioClip)boost.Invoke(audio, new object[] { clip, 1.5f });
            var high = (AudioClip)boost.Invoke(audio, new object[] { clip, 2f });
            Assert.AreSame(low, lowAgain);
            Assert.AreNotSame(low, high);
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void PathLeft_ShortRoad_BeatsLongIndex()
    {
        var towerGo = new GameObject("tower");
        towerGo.SetActive(false);
        var tower = towerGo.AddComponent<Tower>();
        tower.range = 100f;
        var nearGo = new GameObject("near");
        nearGo.SetActive(false);
        var near = nearGo.AddComponent<Enemy>();
        var farGo = new GameObject("far");
        farGo.SetActive(false);
        var far = farGo.AddComponent<Enemy>();
        var shortPath = Road(2);
        shortPath[1].position = new Vector3(4f, 0f, 0f);
        var longPath = Road(11);
        longPath[10].position = new Vector3(59f, 0f, 0f);
        near.waypoints = shortPath;
        near.exitDist = WaveManager.ExitDistances(shortPath);
        near.currentWayPoint = 0;
        nearGo.transform.position = shortPath[0].position;
        far.waypoints = longPath;
        far.exitDist = WaveManager.ExitDistances(longPath);
        far.currentWayPoint = 10;
        farGo.transform.position = longPath[9].position;
        WaveManager.alive.Add(far);
        WaveManager.alive.Add(near);
        var folded = Road(3);
        folded[1].position = folded[0].position;
        folded[2].position = new Vector3(5f, 0f, 0f);
        try
        {
            float[] foldedExit = WaveManager.ExitDistances(folded);
            Assert.AreEqual(5f, foldedExit[0], 0.0001f);
            Assert.AreEqual(5f, foldedExit[1], 0.0001f);
            Assert.AreEqual(0f, foldedExit[2], 0.0001f);
            Assert.AreEqual(4f, near.PathLeft(), 0.0001f);
            Assert.AreEqual(50f, far.PathLeft(), 0.0001f);
            var best = (Enemy)typeof(Tower)
                .GetMethod("FindBestTarget", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(tower, null);
            Assert.AreSame(near, best);
        }
        finally
        {
            WaveManager.alive.Clear();
            Object.DestroyImmediate(towerGo);
            Object.DestroyImmediate(nearGo);
            Object.DestroyImmediate(farGo);
            foreach (var point in shortPath)
                Object.DestroyImmediate(point.gameObject);
            foreach (var point in longPath)
                Object.DestroyImmediate(point.gameObject);
            foreach (var point in folded)
                Object.DestroyImmediate(point.gameObject);
        }
    }

    [Test]
    public void First_Tie_UsesDistanceThenId()
    {
        var towerGo = new GameObject("tower");
        towerGo.SetActive(false);
        var tower = towerGo.AddComponent<Tower>();
        tower.range = 20f;
        var path = Road(2);
        path[1].position = new Vector3(4f, 0f, 0f);
        var exit = WaveManager.ExitDistances(path);
        var leftGo = new GameObject("left");
        leftGo.SetActive(false);
        var left = leftGo.AddComponent<Enemy>();
        var rightGo = new GameObject("right");
        rightGo.SetActive(false);
        var right = rightGo.AddComponent<Enemy>();
        left.waypoints = path;
        right.waypoints = path;
        left.exitDist = exit;
        right.exitDist = exit;
        leftGo.transform.position = new Vector3(-2f, 0f, 0f);
        rightGo.transform.position = new Vector3(2f, 0f, 0f);
        towerGo.transform.position = new Vector3(5f, 0f, 0f);
        WaveManager.alive.Add(left);
        WaveManager.alive.Add(right);
        try
        {
            Assert.AreEqual(left.PathLeft(), right.PathLeft(), 0.0001f);
            Assert.AreSame(right, Best(tower));

            rightGo.transform.position = leftGo.transform.position;
            Enemy low = left.GetInstanceID() < right.GetInstanceID() ? left : right;
            Enemy high = low == left ? right : left;
            WaveManager.alive.Clear();
            WaveManager.alive.Add(high);
            WaveManager.alive.Add(low);
            Assert.AreSame(low, Best(tower));
        }
        finally
        {
            WaveManager.alive.Clear();
            Object.DestroyImmediate(towerGo);
            Object.DestroyImmediate(leftGo);
            Object.DestroyImmediate(rightGo);
            foreach (var point in path)
                Object.DestroyImmediate(point.gameObject);
        }
    }

    static Enemy Best(Tower tower)
    {
        return (Enemy)typeof(Tower)
            .GetMethod("FindBestTarget", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(tower, null);
    }

    static Transform[] Road(int count)
    {
        var path = new Transform[count];
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("p" + i);
            go.transform.position = new Vector3(i, 0f, 0f);
            path[i] = go.transform;
        }
        return path;
    }

    [Test]
    public void Select_HoldsPrice_RefundsOnCancel_DropsOnReset()
    {
        var coinsGo = new GameObject("coins");
        coinsGo.SetActive(false);
        var coins = coinsGo.AddComponent<CoinManager>();
        coins.coinTxt = coinsGo.AddComponent<TextMeshProUGUI>();
        coins.coins = 10;
        CoinManager.instance = coins;
        var uiGo = new GameObject("select");
        var ui = uiGo.AddComponent<TowerSelectionUI>();
        var prefab = new GameObject("tower");
        prefab.SetActive(false);
        prefab.AddComponent<Tower>().towerPrice = 4;
        try
        {
            ui.SelectTower(prefab);
            Assert.AreEqual(6, coins.coins);
            Assert.AreSame(prefab, TowerSelectionUI.SelectedTowerPrefab);
            Assert.AreEqual(4, TowerSelectionUI.HeldPrice);
            ui.SelectTower(prefab);
            Assert.AreEqual(10, coins.coins);
            Assert.IsNull(TowerSelectionUI.SelectedTowerPrefab);
            ui.SelectTower(prefab);
            coins.ResetCoins();
            Assert.IsNull(TowerSelectionUI.SelectedTowerPrefab);
            Assert.AreEqual(0, coins.coins);
            Assert.AreEqual(0, TowerSelectionUI.HeldPrice);
        }
        finally
        {
            TowerSelectionUI.Clear(false);
            CoinManager.instance = null;
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(uiGo);
            Object.DestroyImmediate(coinsGo);
        }
    }

    [Test]
    public void Upgrade_StampsShot_KeepsPrefabSpeed()
    {
        var coinsGo = new GameObject("coins");
        coinsGo.SetActive(false);
        var coins = coinsGo.AddComponent<CoinManager>();
        coins.coinTxt = coinsGo.AddComponent<TextMeshProUGUI>();
        coins.coins = 5;
        CoinManager.instance = coins;
        var towerGo = new GameObject("tower");
        towerGo.SetActive(false);
        towerGo.AddComponent<SpriteRenderer>();
        var tower = towerGo.AddComponent<Tower>();
        var cloud = new GameObject("cloud");
        tower.cloudPS = cloud;
        var point = new GameObject("point");
        tower.firePoint = point.transform;
        var bullet = new GameObject("bullet");
        bullet.SetActive(false);
        var defined = bullet.AddComponent<Projectile>();
        defined.speed = 6f;
        defined.damage = 9;
        defined.burnDamage = 9;
        defined.burnDuration = 9f;
        defined.slowDuration = 9f;
        tower.projectilePrefab = bullet;
        tower.damage = 1;
        tower.upgradeStages = new[]
        {
            new TowerUpgradeStages
            {
                range = 4f,
                fireRate = 2f,
                price = 1,
                damage = 3,
                channel = HitChannel.Spell,
                burnDamage = 2,
                burnDuration = 1.5f,
                slowScale = 0.5f,
                slowDuration = 0f
            }
        };
        var enemyGo = new GameObject("enemy");
        enemyGo.SetActive(false);
        var enemy = enemyGo.AddComponent<Enemy>();
        enemyGo.SetActive(true);
        towerGo.SendMessage("Awake");
        GameObject live = null;
        try
        {
            Assert.IsTrue(tower.TryUpgrade());
            Assert.AreEqual(3, tower.damage);
            Assert.AreEqual(HitChannel.Spell, tower.channel);
            Assert.AreEqual(1.5f, tower.burnDuration, 0.0001f);
            Assert.AreEqual(0f, tower.slowDuration, 0.0001f);
            typeof(Tower).GetMethod("Shoot", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(tower, new object[] { enemy });
            live = GameObject.Find("bullet(Clone)");
            Projectile shot = live.GetComponent<Projectile>();
            Assert.AreEqual(6f, shot.speed, 0.0001f);
            Assert.AreEqual(3, shot.damage);
            Assert.AreEqual(HitChannel.Spell, shot.channel);
            Assert.AreEqual(2, shot.burnDamage);
            Assert.AreEqual(1.5f, shot.burnDuration, 0.0001f);
            Assert.AreEqual(0.5f, shot.slowScale, 0.0001f);
            Assert.AreEqual(0f, shot.slowDuration, 0.0001f);
            Assert.AreEqual(enemy.life, shot.life);
            Assert.AreSame(enemy.transform, shot.target);
        }
        finally
        {
            CoinManager.instance = null;
            GameObject extra;
            while ((extra = GameObject.Find("cloud(Clone)")) != null)
                Object.DestroyImmediate(extra);
            if (live != null)
                Object.DestroyImmediate(live);
            Object.DestroyImmediate(bullet);
            Object.DestroyImmediate(point);
            Object.DestroyImmediate(cloud);
            Object.DestroyImmediate(towerGo);
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(coinsGo);
        }
    }

    [Test]
    public void Exit_MarksWithoutLeak_DeadSkipsLeak()
    {
        var healthGo = new GameObject("health");
        healthGo.SetActive(false);
        var hm = healthGo.AddComponent<HealthManager>();
        hm.healthTxt = healthGo.AddComponent<TextMeshProUGUI>();
        hm.health = 20;
        HealthManager.Instance = hm;
        var coinsGo = new GameObject("coins");
        coinsGo.SetActive(false);
        var coins = coinsGo.AddComponent<CoinManager>();
        coins.coinTxt = coinsGo.AddComponent<TextMeshProUGUI>();
        CoinManager.instance = coins;
        var path = Road(2);
        path[1].position = new Vector3(1f, 0f, 0f);
        var prefab = new GameObject("enemy-prefab");
        prefab.SetActive(false);
        var defined = prefab.AddComponent<Enemy>();
        defined.health = 3;
        defined.leak = 4;
        defined.speed = 1f;
        defined.reward = 1;
        GameObject liveGo = ObjectPool.Get(prefab, Vector3.zero, Quaternion.identity);
        GameObject deadGo = ObjectPool.Get(prefab, Vector3.zero, Quaternion.identity);
        Enemy live = liveGo.GetComponent<Enemy>();
        Enemy dead = deadGo.GetComponent<Enemy>();
        live.waypoints = path;
        dead.waypoints = path;
        try
        {
            live.TickMove(2f);
            Assert.IsTrue(liveGo.activeSelf);
            Assert.AreEqual(20, hm.health);
            live.LeakIfExit();
            Assert.IsFalse(liveGo.activeSelf);
            Assert.AreEqual(16, hm.health);

            dead.TickMove(2f);
            dead.Hurt(3);
            Assert.IsFalse(deadGo.activeSelf);
            Assert.AreEqual(16, hm.health);
            dead.LeakIfExit();
            Assert.AreEqual(16, hm.health);
        }
        finally
        {
            HealthManager.Instance = null;
            CoinManager.instance = null;
            WaveManager.alive.Clear();
            Object.DestroyImmediate(liveGo);
            Object.DestroyImmediate(deadGo);
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(healthGo);
            Object.DestroyImmediate(coinsGo);
            foreach (var point in path)
                Object.DestroyImmediate(point.gameObject);
        }
    }

    [Test]
    public void BadPath_DropsBody_NullPointExits()
    {
        var healthGo = new GameObject("health");
        healthGo.SetActive(false);
        var hm = healthGo.AddComponent<HealthManager>();
        hm.healthTxt = healthGo.AddComponent<TextMeshProUGUI>();
        hm.health = 20;
        HealthManager.Instance = hm;
        var laneGo = new GameObject("lane");
        laneGo.SetActive(false);
        var lane = laneGo.AddComponent<WaveManager>();
        var prefab = new GameObject("enemy-prefab");
        prefab.SetActive(false);
        var defined = prefab.AddComponent<Enemy>();
        defined.health = 3;
        defined.leak = 4;
        defined.speed = 1f;
        var spawn = typeof(WaveManager).GetMethod("SpawnEnemy", BindingFlags.NonPublic | BindingFlags.Instance);
        var point = new GameObject("point");
        point.transform.position = new Vector3(1f, 0f, 0f);
        try
        {
            lane.wayPoints = null;
            spawn.Invoke(lane, new object[] { prefab });
            Assert.AreEqual(0, WaveManager.alive.Count);
            Assert.AreEqual(20, hm.health);

            lane.wayPoints = new Transform[0];
            spawn.Invoke(lane, new object[] { prefab });
            Assert.AreEqual(0, WaveManager.alive.Count);
            Assert.AreEqual(20, hm.health);

            lane.wayPoints = new Transform[] { null };
            spawn.Invoke(lane, new object[] { prefab });
            Assert.AreEqual(0, WaveManager.alive.Count);
            Assert.AreEqual(20, hm.health);

            GameObject liveGo = ObjectPool.Get(prefab, Vector3.zero, Quaternion.identity);
            Enemy live = liveGo.GetComponent<Enemy>();
            live.waypoints = new[] { point.transform, null };
            live.currentWayPoint = 1;
            live.TickMove(1f);
            live.LeakIfExit();
            Assert.IsFalse(liveGo.activeSelf);
            Assert.AreEqual(16, hm.health);
        }
        finally
        {
            HealthManager.Instance = null;
            WaveManager.alive.Clear();
            foreach (var enemy in Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (enemy.gameObject != prefab)
                    Object.DestroyImmediate(enemy.gameObject);
            }
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(laneGo);
            Object.DestroyImmediate(healthGo);
            Object.DestroyImmediate(point);
        }
    }

    [Test]
    public void Knight_ChaseDoesNotHold_MeleeDoes()
    {
        var root = new GameObject("barrack");
        root.SetActive(false);
        var tower = root.AddComponent<Tower>();
        tower.range = 1.6f;
        var bar = root.AddComponent<Barrack>();
        bar.knightSprites = new Sprite[3];
        bar.melee = 0.45f;
        var enemyGo = new GameObject("enemy");
        enemyGo.SetActive(false);
        var enemy = enemyGo.AddComponent<Enemy>();
        enemy.health = 10;
        enemyGo.transform.position = new Vector3(1f, 0f, 0f);
        root.SetActive(true);
        enemyGo.SetActive(true);
        try
        {
            bar.Tick();
            Assert.AreEqual(0, Held(enemy));

            foreach (Transform child in root.transform)
                child.position = enemyGo.transform.position;
            bar.Tick();
            Assert.AreEqual(1, Held(enemy));

            enemyGo.transform.position = new Vector3(5f, 0f, 0f);
            bar.Tick();
            Assert.AreEqual(0, Held(enemy));

            enemyGo.transform.position = new Vector3(1f, 0f, 0f);
            foreach (Transform child in root.transform)
                child.position = enemyGo.transform.position;
            bar.Tick();
            Assert.AreEqual(1, Held(enemy));
            enemy.life++;
            foreach (Transform child in root.transform)
                child.position = root.transform.position;
            bar.Tick();
            Assert.AreEqual(0, Held(enemy));
        }
        finally
        {
            WaveManager.alive.Clear();
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void Reborn_ClearsHolders_KnightSkipsOldHold()
    {
        var root = new GameObject("barrack");
        root.SetActive(false);
        var tower = root.AddComponent<Tower>();
        tower.range = 1.6f;
        var bar = root.AddComponent<Barrack>();
        bar.knightSprites = new Sprite[3];
        bar.melee = 0.45f;
        bar.attackEvery = 0f;
        var prefab = new GameObject("enemy-prefab");
        prefab.SetActive(false);
        var defined = prefab.AddComponent<Enemy>();
        defined.health = 10;
        root.SetActive(true);
        GameObject live = ObjectPool.Get(prefab, new Vector3(1f, 0f, 0f), Quaternion.identity);
        Enemy enemy = live.GetComponent<Enemy>();
        int born = enemy.life;
        try
        {
            foreach (Transform child in root.transform)
                child.position = live.transform.position;
            bar.Tick();
            Assert.AreEqual(1, Held(enemy));
            Assert.AreEqual(9, enemy.health);

            ObjectPool.Release(live);
            Assert.AreEqual(0, Held(enemy));
            live = ObjectPool.Get(prefab, new Vector3(1f, 0f, 0f), Quaternion.identity);
            enemy = live.GetComponent<Enemy>();
            Assert.AreNotEqual(born, enemy.life);
            Assert.AreEqual(0, Held(enemy));
            Assert.AreEqual(10, enemy.health);

            KnightBody planted = root.GetComponentInChildren<KnightBody>();
            enemy.Hold(planted, true);
            foreach (Transform child in root.transform)
                child.position = new Vector3(0f, -0.95f, 0f);
            bar.Tick();
            Assert.AreEqual(1, Held(enemy));
            Assert.AreEqual(10, enemy.health);
        }
        finally
        {
            WaveManager.alive.Clear();
            if (live != null)
                Object.DestroyImmediate(live);
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void TwoBarracks_ClaimOneEach_SpareWaits()
    {
        var first = BarrackAt("a", Vector3.zero);
        var second = BarrackAt("b", Vector3.zero);
        var nearGo = new GameObject("near");
        nearGo.SetActive(false);
        var near = nearGo.AddComponent<Enemy>();
        near.health = 5;
        nearGo.transform.position = new Vector3(0f, 1f, 0f);
        var farGo = new GameObject("far");
        farGo.SetActive(false);
        var far = farGo.AddComponent<Enemy>();
        far.health = 5;
        farGo.transform.position = new Vector3(3f, 1f, 0f);
        nearGo.SetActive(true);
        farGo.SetActive(true);
        try
        {
            foreach (Transform child in first.transform)
                child.position = nearGo.transform.position;
            foreach (Transform child in second.transform)
                child.position = nearGo.transform.position;
            first.GetComponent<Barrack>().Tick();
            second.GetComponent<Barrack>().Tick();
            Assert.IsTrue(near.Claimed);
            Assert.IsTrue(far.Claimed);
            Assert.AreEqual(1, Held(near));
            Assert.AreEqual(0, Held(far));
        }
        finally
        {
            WaveManager.alive.Clear();
            Object.DestroyImmediate(nearGo);
            Object.DestroyImmediate(farGo);
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
        }
    }

    [Test]
    public void SpawnGap_SplitsWindowAcrossBodies_SkipsEmpty()
    {
        var wave = new WaveData
        {
            duration = 15f,
            groups = new[]
            {
                new WaveGroup { count = 3 },
                new WaveGroup { count = 0 },
                new WaveGroup { count = 4 },
                new WaveGroup { count = 2 },
                new WaveGroup { count = 1 },
                new WaveGroup { count = 2 },
            }
        };
        Assert.AreEqual(15f / 12f, WaveManager.SpawnGap(wave), 0.0001f);
        wave.groups = new WaveGroup[0];
        Assert.AreEqual(0f, WaveManager.SpawnGap(wave));
    }

    [Test]
    public void Hold_ShowsDung_BesideBurn()
    {
        var go = new GameObject("enemy");
        go.SetActive(false);
        var enemy = go.AddComponent<Enemy>();
        enemy.health = 5;
        var knightGo = new GameObject("knight");
        var knight = knightGo.AddComponent<KnightBody>();
        go.SetActive(true);
        try
        {
            enemy.Hold(knight, true);
            Assert.AreEqual("Dừng", StatusOf(enemy));
            enemy.Burn(1, 2f);
            Assert.AreEqual("Burn\nDừng", StatusOf(enemy));
            enemy.Hold(knight, false);
            Assert.AreEqual("Burn", StatusOf(enemy));
        }
        finally
        {
            WaveManager.alive.Clear();
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(knightGo);
        }
    }

    static string StatusOf(Enemy enemy)
    {
        return enemy.GetComponentInChildren<TextMeshPro>().text;
    }

    static GameObject BarrackAt(string name, Vector3 at)
    {
        var root = new GameObject(name);
        root.SetActive(false);
        root.transform.position = at;
        var tower = root.AddComponent<Tower>();
        tower.range = 5f;
        var bar = root.AddComponent<Barrack>();
        bar.knightSprites = new Sprite[3];
        bar.melee = 0.45f;
        root.SetActive(true);
        return root;
    }

    static int Held(Enemy enemy)
    {
        return (int)typeof(Enemy).GetField("held", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(enemy);
    }
}
