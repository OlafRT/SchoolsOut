using UnityEngine;
using UnityEngine.UI;

public class GameSettings : MonoBehaviour
{
    [Header("UI References")]
    public Dropdown qualityDropdown;
    public Dropdown screenModeDropdown;

    private const string QUALITY_KEY = "QualityLevel";
    private const string SCREENMODE_KEY = "ScreenMode";

    void Start()
    {
        SetupQualityDropdown();
        SetupScreenModeDropdown();
        LoadSettings();
    }

    // -------------------------
    // QUALITY SETTINGS
    // -------------------------
    void SetupQualityDropdown()
    {
        qualityDropdown.ClearOptions();
        qualityDropdown.AddOptions(new System.Collections.Generic.List<string>(QualitySettings.names));

        qualityDropdown.onValueChanged.AddListener(SetQuality);
    }

    public void SetQuality(int index)
    {
        QualitySettings.SetQualityLevel(index, true);
        PlayerPrefs.SetInt(QUALITY_KEY, index);
    }

    // -------------------------
    // SCREEN MODE SETTINGS
    // -------------------------
    void SetupScreenModeDropdown()
    {
        screenModeDropdown.ClearOptions();

        var options = new System.Collections.Generic.List<string>
        {
            "Fullscreen",
            "Borderless",
            "Windowed"
        };

        screenModeDropdown.AddOptions(options);
        screenModeDropdown.onValueChanged.AddListener(SetScreenMode);
    }

    public void SetScreenMode(int index)
    {
        FullScreenMode mode = FullScreenMode.FullScreenWindow;

        switch (index)
        {
            case 0:
                mode = FullScreenMode.ExclusiveFullScreen;
                break;
            case 1:
                mode = FullScreenMode.FullScreenWindow;
                break;
            case 2:
                mode = FullScreenMode.Windowed;
                break;
        }

        Screen.fullScreenMode = mode;
        PlayerPrefs.SetInt(SCREENMODE_KEY, index);
    }

    // -------------------------
    // LOAD SETTINGS
    // -------------------------
    void LoadSettings()
    {
        int quality = PlayerPrefs.GetInt(QUALITY_KEY, QualitySettings.GetQualityLevel());
        int screenMode = PlayerPrefs.GetInt(SCREENMODE_KEY, 1);

        qualityDropdown.value = quality;
        screenModeDropdown.value = screenMode;

        SetQuality(quality);
        SetScreenMode(screenMode);
    }
}
