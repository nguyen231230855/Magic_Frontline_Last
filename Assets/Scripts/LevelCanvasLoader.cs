using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelCanvasLoader : MonoBehaviour
{
    public static Canvas Hud { get; private set; }
    static GameObject spawned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        var go = new GameObject(nameof(LevelCanvasLoader));
        DontDestroyOnLoad(go);
        go.AddComponent<LevelCanvasLoader>();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Main Menu")
        {
            Hud = null;
            if (spawned != null)
                Destroy(spawned);
            return;
        }

        var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var canvas in canvases)
        {
            if (canvas.gameObject.scene == scene)
                Destroy(canvas.gameObject);
        }

        if (spawned == null)
        {
            var prefab = Resources.Load<GameObject>("LevelCanvas");
            Debug.Assert(prefab != null, "LevelCanvas prefab missing");
            spawned = Instantiate(prefab);
            DontDestroyOnLoad(spawned);
        }

        Hud = spawned.GetComponent<Canvas>();
        Debug.Assert(Hud != null, "Level HUD missing");

        Button goButton = null;
        TextMeshProUGUI waveTxt = null;
        foreach (var button in spawned.GetComponentsInChildren<Button>(true))
        {
            if (button.name == "GoBTN")
                goButton = button;
        }
        foreach (var label in spawned.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (label.name == "WaveTxt")
                waveTxt = label;
        }
        Debug.Assert(goButton != null && waveTxt != null, "Level canvas missing GoBTN or WaveTxt");

        var placer = FindFirstObjectByType<TowerPlacer>();
        var coins = spawned.GetComponentInChildren<CoinManager>(true);
        Debug.Assert(placer != null && coins != null, "Level missing coins");
        if (placer != null && coins != null)
            coins.SetStart(placer.startCoins);

        EnsureHome(spawned);

        var lanes = new List<WaveManager>();
        foreach (var wave in FindObjectsByType<WaveManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (wave.gameObject.scene == scene)
                lanes.Add(wave);
        }
        WaveLaneHud.Load(spawned, goButton, waveTxt, lanes);
    }

    static void EnsureHome(GameObject canvas)
    {
        var home = canvas.GetComponent<LevelHome>();
        if (home == null)
            home = canvas.AddComponent<LevelHome>();
        else
            home.ResetLevel();
    }
}

public class LevelHome : MonoBehaviour
{
    GameObject panel;
    GameObject dim;
    Transform winPanel;
    Transform lostPanel;
    bool lost;

    void Awake()
    {
        panel = transform.Find("HomeBackPanel").gameObject;
        var existing = transform.Find("DimBackground");
        dim = existing != null ? existing.gameObject : CreateDim();
        dim.SetActive(false);
        panel.SetActive(false);
        FindButton("HomeBTN").onClick.AddListener(Open);
        FindButton("YesBTN").onClick.AddListener(Confirm);
        FindButton("NoBTN").onClick.AddListener(Hide);
        var optionsButton = FindButton("SettingBTN");
        if (optionsButton != null)
            optionsButton.onClick.AddListener(OpenOptions);
        foreach (var button in GetComponentsInChildren<Button>(true))
            button.onClick.AddListener(PlayClick);
        Debug.Assert(MenuManager.ClickSfx != null, "Click sfx missing");
        winPanel = transform.Find("WinPanel");
        if (winPanel != null)
        {
            winPanel.gameObject.SetActive(false);
            Wire(winPanel, "RePlayBTN", Replay);
            Wire(winPanel, "NextBTN", Next);
        }
        lostPanel = transform.Find("LostPanel");
        if (lostPanel != null)
        {
            lostPanel.gameObject.SetActive(false);
            Wire(lostPanel, "RePlayBTN", Replay);
            Wire(lostPanel, "BackBTN", Back);
        }
    }

    void Wire(Transform root, string name, UnityEngine.Events.UnityAction action)
    {
        foreach (var button in root.GetComponentsInChildren<Button>(true))
        {
            if (button.name != name)
                continue;
            button.onClick.AddListener(action);
            return;
        }
        Debug.Assert(false, name + " missing");
    }

    public void ResetLevel()
    {
        lost = false;
        var speed = GetComponentInChildren<SpeedUpManager>(true);
        TowerSelectionUI.Clear(false);
        if (winPanel != null)
            winPanel.gameObject.SetActive(false);
        if (lostPanel != null)
            lostPanel.gameObject.SetActive(false);
        if (panel != null)
            panel.SetActive(false);
        if (dim != null)
            dim.SetActive(false);
        var health = GetComponentInChildren<HealthManager>(true);
        if (health != null)
            health.ResetHealth();
        var coins = GetComponentInChildren<CoinManager>(true);
        if (coins != null)
            coins.ResetCoins();
        if (speed != null)
            speed.ResetSpeed();
        SpeedUpManager.ClearPause();
        foreach (var label in GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (label.name == "WaveTxt")
                label.text = "1";
        }
        foreach (var button in GetComponentsInChildren<Button>(true))
        {
            if (button.name == "GoBTN")
            {
                button.gameObject.SetActive(true);
                button.interactable = true;
            }
        }
        SetBattleButtons(true);
        foreach (var upgrade in GetComponentsInChildren<TowerUpgradeUI>(true))
            Destroy(upgrade.gameObject);
    }

    public void ShowLost()
    {
        if (lost)
            return;
        lost = true;
        SpeedUpManager.PushPause();
        Debug.Assert(lostPanel != null, "LostPanel missing");
        if (lostPanel == null)
            return;
        lostPanel.gameObject.SetActive(true);
        lostPanel.SetAsLastSibling();
        SetBattleButtons(false);
    }

    void Back()
    {
        SpeedUpManager.ClearPause();
        Time.timeScale = 1f;
        MenuManager.opened = true;
        SceneManager.LoadScene("Main Menu");
    }

    void Replay()
    {
        ResetLevel();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void Next()
    {
        SpeedUpManager.ClearPause();
        Time.timeScale = 1f;
        MenuManager.opened = true;
        MenuManager.showPlay = true;
        SceneManager.LoadScene("Main Menu");
    }

    public void ShowWin(int health)
    {
        Debug.Assert(winPanel != null, "WinPanel missing");
        SpeedUpManager.PushPause();
        if (winPanel == null)
            return;
        MenuManager.ClearLevel(SceneManager.GetActiveScene().name);
        int max = HealthManager.Instance.MaxHealth;
        int stars = health * 3 > max * 2 ? 3 : health * 3 > max ? 2 : 1;
        string key = SceneManager.GetActiveScene().name + "Stars";
        if (stars > PlayerPrefs.GetInt(key, 0))
        {
            PlayerPrefs.SetInt(key, stars);
            PlayerPrefs.Save();
        }
        winPanel.gameObject.SetActive(true);
        winPanel.SetAsLastSibling();
        for (int i = 1; i <= 3; i++)
            winPanel.Find("Star_" + i).gameObject.SetActive(i <= stars);
        SetBattleButtons(false);
    }

    public bool ResultOpen =>
        winPanel != null && winPanel.gameObject.activeSelf ||
        lostPanel != null && lostPanel.gameObject.activeSelf;

    void SetBattleButtons(bool on)
    {
        if (!on)
            TowerSelectionUI.Clear(true);
        foreach (var button in GetComponentsInChildren<Button>(true))
        {
            if (button.name == "2XBTN" || button.name.StartsWith("TowerBTN"))
                button.interactable = on;
        }
    }

    Button FindButton(string name)
    {
        foreach (var button in GetComponentsInChildren<Button>(true))
        {
            if (button.name == name)
                return button;
        }
        Debug.Assert(false, name + " missing");
        return null;
    }

    GameObject CreateDim()
    {
        var go = new GameObject("DimBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.5882353f);
        return go;
    }

    void PlayClick()
    {
        if (AudioManager.Instance == null || MenuManager.ClickSfx == null)
            return;
        AudioManager.Instance.PlaySFX(MenuManager.ClickSfx);
    }

    void OpenOptions()
    {
        Debug.Assert(OptionsHost.Instance != null, "OptionsHost missing");
        OptionsHost.Instance.OpenPaused();
    }

    void Open()
    {
        if (!panel.activeSelf)
            SpeedUpManager.PushPause();
        dim.SetActive(true);
        panel.SetActive(true);
        dim.transform.SetAsLastSibling();
        panel.transform.SetAsLastSibling();
    }

    public void Hide()
    {
        bool wasOpen = panel != null && panel.activeSelf;
        if (panel != null)
            panel.SetActive(false);
        if (dim != null)
            dim.SetActive(false);
        if (wasOpen)
            SpeedUpManager.PopPause();
    }

    void Confirm()
    {
        SpeedUpManager.ClearPause();
        Time.timeScale = 1f;
        MenuManager.opened = true;
        SceneManager.LoadScene("Main Menu");
    }
}
