using UnityEngine;
using UnityEngine.UI;

public class SpeedUpManager : MonoBehaviour
{
    public static int pauseCount;
    static SpeedUpManager current;

    public bool speedUp;
    public Image btnImage;
    public Color normalColor;
    public Color speedUpColor;

    void OnEnable()
    {
        current = this;
    }

    void OnDisable()
    {
        if (current == this)
            current = null;
    }

    public static void PushPause()
    {
        pauseCount++;
        Apply();
    }

    public static void PopPause()
    {
        if (pauseCount > 0)
            pauseCount--;
        Apply();
    }

    public static void ClearPause()
    {
        pauseCount = 0;
        if (OptionsHost.Instance != null)
            OptionsHost.Instance.ReleaseHold();
        Apply();
    }

    // Result screens freeze on their own hold. Fast is only the scale once nothing is held.
    public static void Apply()
    {
        var home = Object.FindFirstObjectByType<LevelHome>();
        bool fast = current != null && current.speedUp;
        Time.timeScale = pauseCount > 0 || (home != null && home.ResultOpen) ? 0f : (fast ? 2f : 1f);
    }

    public void ResetSpeed()
    {
        current = this;
        speedUp = false;
        btnImage.color = normalColor;
        Apply();
    }

    public void ToggleSpeedUp()
    {
        current = this;
        if (pauseCount > 0) return;
        var home = FindFirstObjectByType<LevelHome>();
        if (home != null && home.ResultOpen) return;

        speedUp = !speedUp;
        btnImage.color = speedUp ? speedUpColor : normalColor;
        Apply();
    }
}
