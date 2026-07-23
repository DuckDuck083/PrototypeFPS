using UnityEngine;

public sealed class GameSettingsManager : MonoBehaviour
{
    public const string VolumeKey = "PrototypeFPS.Settings.Volume";
    public const string SensitivityKey = "PrototypeFPS.Settings.Sensitivity";
    public const string FieldOfViewKey = "PrototypeFPS.Settings.FOV";
    public const string QualityKey = "PrototypeFPS.Settings.Quality";
    public const string FullscreenKey = "PrototypeFPS.Settings.Fullscreen";

    public float MasterVolume { get; private set; }
    public float MouseSensitivity { get; private set; }
    public float FieldOfView { get; private set; }
    public int QualityLevel { get; private set; }
    public bool Fullscreen { get; private set; }

    private void Awake()
    {
        MasterVolume = PlayerPrefs.GetFloat(VolumeKey, 0.85f);
        MouseSensitivity = PlayerPrefs.GetFloat(SensitivityKey, 0.12f);
        FieldOfView = PlayerPrefs.GetFloat(FieldOfViewKey, 75f);
        QualityLevel = Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1);
        Fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
    }

    private void Start() => ApplyAll();

    public void SetVolume(float value)
    {
        MasterVolume = Mathf.Clamp01(value);
        AudioListener.volume = MasterVolume;
        PlayerPrefs.SetFloat(VolumeKey, MasterVolume);
    }

    public void SetSensitivity(float value)
    {
        MouseSensitivity = Mathf.Clamp(value, 0.03f, 0.4f);
        FirstPersonController controller = FindAnyObjectByType<FirstPersonController>();
        if (controller != null) controller.MouseSensitivity = MouseSensitivity;
        PlayerPrefs.SetFloat(SensitivityKey, MouseSensitivity);
    }

    public void SetFieldOfView(float value)
    {
        FieldOfView = Mathf.Clamp(value, 60f, 110f);
        SimpleRifle rifle = FindAnyObjectByType<SimpleRifle>();
        if (rifle != null) rifle.SetBaseFieldOfView(FieldOfView);
        else if (Camera.main != null) Camera.main.fieldOfView = FieldOfView;
        PlayerPrefs.SetFloat(FieldOfViewKey, FieldOfView);
    }

    public void SetQuality(int value)
    {
        QualityLevel = Mathf.Clamp(value, 0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(QualityLevel, true);
        PlayerPrefs.SetInt(QualityKey, QualityLevel);
    }

    public void SetFullscreen(bool value)
    {
        Fullscreen = value;
        Screen.fullScreen = value;
        PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
    }

    public void Save()
    {
        PlayerPrefs.Save();
        ApplyAll();
    }

    private void ApplyAll()
    {
        AudioListener.volume = MasterVolume;
        SetSensitivity(MouseSensitivity);
        SetFieldOfView(FieldOfView);
        QualitySettings.SetQualityLevel(QualityLevel, true);
        Screen.fullScreen = Fullscreen;
    }
}
