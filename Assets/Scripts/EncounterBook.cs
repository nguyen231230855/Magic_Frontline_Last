using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class EncounterBook
{
    const string Key = "discoveredIds";

    static readonly HashSet<string> known = new();
    static readonly Queue<Page> waiting = new();
    static bool loaded;
    static Page showing;
    static bool active;

    public struct Page
    {
        public EncounterCard card;
        public Sprite icon;
    }

    public static event System.Action<Page> Shown;
    public static event System.Action Closed;

    public static Page Showing => showing;

    static EncounterBook()
    {
        SceneManager.sceneUnloaded += _ => Seal();
    }

    public static void Discover(EncounterCard card, Sprite icon)
    {
        if (card == null || string.IsNullOrEmpty(card.id))
            return;
        Load();
        if (known.Contains(card.id))
            return;
        foreach (Page page in waiting)
            if (page.card.id == card.id)
                return;
        waiting.Enqueue(new Page { card = card, icon = card.icon != null ? card.icon : icon });
        if (active)
            return;
        ShowNext();
    }

    public static void Dismiss()
    {
        if (!active)
            return;
        Remember(showing.card);
        if (waiting.Count > 0)
            waiting.Dequeue();
        active = false;
        if (waiting.Count == 0)
        {
            showing = default;
            Closed?.Invoke();
            return;
        }
        ShowNext();
    }

    public static void Seal()
    {
        if (active)
            Remember(showing.card);
        waiting.Clear();
        showing = default;
        active = false;
        Closed?.Invoke();
    }

    public static void Clear()
    {
        known.Clear();
        waiting.Clear();
        showing = default;
        active = false;
        loaded = true;
        PlayerPrefs.DeleteKey(Key);
        Closed?.Invoke();
    }

    static void ShowNext()
    {
        showing = waiting.Peek();
        active = true;
        Shown?.Invoke(showing);
    }

    static void Remember(EncounterCard card)
    {
        if (card == null || !known.Add(card.id))
            return;
        PlayerPrefs.SetString(Key, string.Join("|", known));
        PlayerPrefs.Save();
    }

    public static void Reload()
    {
        loaded = false;
        known.Clear();
        Load();
    }

    static void Load()
    {
        if (loaded)
            return;
        loaded = true;
        known.Clear();
        string raw = PlayerPrefs.GetString(Key, "");
        if (raw.Length == 0)
            return;
        string[] ids = raw.Split('|');
        for (int i = 0; i < ids.Length; i++)
            if (ids[i].Length > 0)
                known.Add(ids[i]);
    }
}
