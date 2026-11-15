using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

[System.Serializable]
public class WaveGroup
{
    public GameObject prefab;
    public int count;
}

[System.Serializable]
public class WaveData
{
    public float duration = 10f;
    public WaveGroup[] groups;
}

[DefaultExecutionOrder(-100)]
public class WaveManager : MonoBehaviour
{
    public static readonly List<Enemy> alive = new();

    public WaveData[] waves;

    public Transform[] wayPoints;

    int currentWaveIndex;
    bool waveRunning;
    WaveLaneHud hud;
    static bool won;
    static int ownedFrame = -1;

    public static bool FrameOwned => Application.isPlaying && ownedFrame == Time.frameCount;
    Grid grid;
    bool pathSnapped;
    float[] exitDist;

    static WaveManager()
    {
        SceneManager.sceneUnloaded += _ => alive.Clear();
    }

    void Start()
    {
        grid = FindFirstObjectByType<Grid>();
        Debug.Assert(grid != null, "WaveManager missing Grid");
        Debug.Assert(waves != null && waves.Length > 0, "WaveManager has no waves");
        won = false;
    }

    public void Bind(WaveLaneHud board)
    {
        hud = board;
    }

    public bool Ready => !waveRunning && waves != null && currentWaveIndex < waves.Length;
    public bool FinalTurn => waves != null && waves.Length > 0 && currentWaveIndex == waves.Length - 1;
    public int ShownWave => currentWaveIndex + 1;

    public void Begin()
    {
        if (!Ready) return;
        hud?.CancelAuto(this);
        StartCoroutine(RunWave());
        hud?.Refresh();
    }

    IEnumerator RunWave()
    {
        waveRunning = true;

        int slot = hud != null ? hud.Order(this) : 0;
        if (slot > 0)
            yield return new WaitForSeconds(slot * WaveLaneHud.Stagger);

        WaveData wave = waves[currentWaveIndex];
        float gap = SpawnGap(wave);
        if (wave.groups != null)
        {
            for (int g = 0; g < wave.groups.Length; g++)
            {
                WaveGroup group = wave.groups[g];
                if (group == null || group.count <= 0)
                    continue;
                Debug.Assert(group.prefab != null, "Wave group has no prefab");
                for (int i = 0; i < group.count; i++)
                {
                    SpawnEnemy(group.prefab);
                    yield return new WaitForSeconds(gap);
                }
            }
        }

        // Last body leaves the gate before this lane reports ready.
        yield return new WaitForSeconds(1f);
        waveRunning = false;
        currentWaveIndex++;
        if (currentWaveIndex >= waves.Length)
            hud?.ReportDone(this);
        else
            hud?.ReportReady(this);
    }

    public static float SpawnGap(WaveData wave)
    {
        int bodies = 0;
        if (wave != null && wave.groups != null)
        {
            for (int i = 0; i < wave.groups.Length; i++)
            {
                WaveGroup group = wave.groups[i];
                if (group != null && group.count > 0)
                    bodies += group.count;
            }
        }
        if (bodies == 0)
            return 0f;
        return wave.duration / bodies;
    }

    bool Cleared => currentWaveIndex >= waves.Length && !waveRunning;

    void Update()
    {
        Battle();
        if (won || !Cleared) return;
        if (HealthManager.Instance == null || HealthManager.Instance.health <= 0) return;
        foreach (var manager in FindObjectsByType<WaveManager>(FindObjectsSortMode.None))
            if (!manager.Cleared) return;
        if (WaveManager.alive.Count > 0) return;
        won = true;
        hud?.Lock();
        var home = FindFirstObjectByType<LevelHome>();
        Debug.Assert(home != null, "LevelHome missing");
        home.ShowWin(HealthManager.Instance.health);
    }

    // Defense wins a tie. Leak stays here. Enemy.Move only marks the exit.
    static void Battle()
    {
        if (Application.isPlaying && ownedFrame == Time.frameCount)
            return;
        if (Application.isPlaying)
            ownedFrame = Time.frameCount;

        // Land shots already in the air before a tower fires another at the corpse.
        Projectile[] shots = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None);
        for (int i = 0; i < shots.Length; i++)
            shots[i].Advance(Time.deltaTime);

        Tower[] towers = Object.FindObjectsByType<Tower>(FindObjectsSortMode.None);
        for (int i = 0; i < towers.Length; i++)
            towers[i].Tick();

        Enemy[] bodies = alive.ToArray();
        for (int i = 0; i < bodies.Length; i++)
            bodies[i].TickStatus(Time.deltaTime);

        Barrack[] barracks = Object.FindObjectsByType<Barrack>(FindObjectsSortMode.None);
        for (int i = 0; i < barracks.Length; i++)
            barracks[i].Tick();

        bodies = alive.ToArray();
        for (int i = 0; i < bodies.Length; i++)
            bodies[i].TickMove(Time.deltaTime);

        for (int i = alive.Count - 1; i >= 0; i--)
            alive[i].LeakIfExit();
    }

    void SpawnEnemy(GameObject prefab)
    {
        if (!pathSnapped && Enemy.UsablePath(wayPoints))
        {
            for (int i = 0; i < wayPoints.Length; i++)
            {
                if (wayPoints[i] == null)
                    continue;
                Vector3 center = grid.GetCellCenterWorld(grid.WorldToCell(wayPoints[i].position));
                center.z = 0f;
                wayPoints[i].position = center;
            }
            pathSnapped = true;
            exitDist = ExitDistances(wayPoints);
        }

        Vector3 at = Vector3.zero;
        if (wayPoints != null)
        {
            for (int i = 0; i < wayPoints.Length; i++)
            {
                if (wayPoints[i] == null)
                    continue;
                at = wayPoints[i].position;
                break;
            }
        }

        GameObject e = ObjectPool.Get(prefab, at, Quaternion.identity);
        Enemy enemy = e.GetComponent<Enemy>();
        enemy.waypoints = wayPoints;
        enemy.exitDist = exitDist;
        // A lane with no usable point is not a leak.
        if (!Enemy.UsablePath(wayPoints))
        {
            ObjectPool.Release(e);
            return;
        }
        SpriteRenderer view = e.GetComponent<SpriteRenderer>();
        EncounterBook.Discover(enemy.card, view != null ? view.sprite : null);
    }

    public static float[] ExitDistances(Transform[] points)
    {
        var exit = new float[points.Length];
        float remain = 0f;
        Vector3 next = default;
        bool hasNext = false;
        for (int i = points.Length - 1; i >= 0; i--)
        {
            if (points[i] == null)
            {
                exit[i] = remain;
                continue;
            }
            Vector3 at = points[i].position;
            if (hasNext)
            {
                float span = Vector2.Distance(at, next);
                if (span > 0.0001f)
                    remain += span;
            }
            exit[i] = remain;
            next = at;
            hasNext = true;
        }
        return exit;
    }
}

public class WaveLaneHud : MonoBehaviour
{
    public const float Stagger = 2.5f;
    const float autoDelay = 5f;

    Button button;
    TextMeshProUGUI label;
    readonly List<WaveManager> lanes = new();
    readonly Dictionary<WaveManager, Coroutine> autos = new();

    public static void Load(GameObject host, Button button, TextMeshProUGUI label, List<WaveManager> found)
    {
        var hud = host.GetComponent<WaveLaneHud>();
        if (hud == null)
            hud = host.AddComponent<WaveLaneHud>();
        hud.Setup(button, label, found);
    }

    // Spawn point, not object name. Higher on the map starts first.
    public static int CompareSpawn(WaveManager a, WaveManager b)
    {
        Vector3 pa = Spawn(a);
        Vector3 pb = Spawn(b);
        int y = pb.y.CompareTo(pa.y);
        if (y != 0) return y;
        int x = pa.x.CompareTo(pb.x);
        if (x != 0) return x;
        return a.GetInstanceID().CompareTo(b.GetInstanceID());
    }

    public static bool AnyWaiting(List<WaveManager> lanes)
    {
        foreach (var lane in lanes)
        {
            if (lane != null && lane.Ready && !lane.FinalTurn)
                return true;
        }
        return false;
    }

    static Vector3 Spawn(WaveManager lane)
    {
        if (lane.wayPoints != null && lane.wayPoints.Length > 0 && lane.wayPoints[0] != null)
            return lane.wayPoints[0].position;
        return lane.transform.position;
    }

    void Setup(Button nextButton, TextMeshProUGUI nextLabel, List<WaveManager> found)
    {
        StopAllCoroutines();
        autos.Clear();
        if (button != null)
            button.onClick.RemoveListener(StartWaiting);
        button = nextButton;
        label = nextLabel;
        lanes.Clear();
        lanes.AddRange(found);
        lanes.Sort(CompareSpawn);
        foreach (var lane in lanes)
            lane.Bind(this);
        button.onClick.AddListener(StartWaiting);
        Refresh();
    }

    public int Order(WaveManager lane)
    {
        int slot = lanes.IndexOf(lane);
        return slot < 0 ? 0 : slot;
    }

    public void CancelAuto(WaveManager lane)
    {
        if (!autos.TryGetValue(lane, out Coroutine running))
            return;
        autos.Remove(lane);
        if (running != null)
            StopCoroutine(running);
    }

    public void ReportReady(WaveManager lane)
    {
        CancelAuto(lane);
        autos[lane] = StartCoroutine(Auto(lane));
        Refresh();
    }

    public void ReportDone(WaveManager lane)
    {
        CancelAuto(lane);
        Refresh();
    }

    public void Refresh()
    {
        int lowest = 1;
        bool any = false;
        foreach (var lane in lanes)
        {
            if (lane == null) continue;
            if (!any || lane.ShownWave < lowest)
                lowest = lane.ShownWave;
            any = true;
        }
        if (label != null)
            label.text = lowest.ToString();
        if (button == null) return;
        bool show = AnyWaiting(lanes);
        button.gameObject.SetActive(show);
        button.interactable = show;
    }

    public void Lock()
    {
        if (button != null)
            button.interactable = false;
    }

    void StartWaiting()
    {
        for (int i = 0; i < lanes.Count; i++)
        {
            WaveManager lane = lanes[i];
            if (lane != null && lane.Ready)
                lane.Begin();
        }
    }

    IEnumerator Auto(WaveManager lane)
    {
        yield return new WaitForSeconds(autoDelay);
        autos.Remove(lane);
        if (lane != null)
            lane.Begin();
    }
}
