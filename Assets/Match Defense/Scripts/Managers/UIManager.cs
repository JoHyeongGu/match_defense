using System;
using System.Collections.Generic;
using UnityEngine;

public enum AppLanguage { KO, EN }

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Localization")]
    [SerializeField] private TextAsset csvFile;
    [SerializeField] private AppLanguage currentLanguage;

    public event Action OnLanguageChanged;
    private Dictionary<string, Dictionary<AppLanguage, string>> m_LocalizedText;

    private const string PREFS_LANG_KEY = "App_Language";

    private void Awake()
    {
        if (Instance == null)
        {
            bool sharesObjectWithSceneComponents = GetComponents<MonoBehaviour>().Length > 1
                || GetComponent<UnityEngine.UIElements.PanelRenderer>() != null;

            if (sharesObjectWithSceneComponents)
            {
                GameObject persistentGo = new GameObject("[UIManager_Persistent]");
                DontDestroyOnLoad(persistentGo);
                UIManager persistent = persistentGo.AddComponent<UIManager>();
                persistent.csvFile = this.csvFile;
                persistent.currentLanguage = this.currentLanguage;
                persistent.InitializeSingleton();
                Destroy(this);
                return;
            }

            DontDestroyOnLoad(gameObject);
            InitializeSingleton();
        }
        else if (Instance != this)
        {
            Destroy(this);
        }
    }

    private void InitializeSingleton()
    {
        if (Instance != null && Instance != this) return;
        Instance = this;
        LoadCSV();
        InitializeLanguage();
    }

    private void InitializeLanguage()
    {
        if (PlayerPrefs.HasKey(PREFS_LANG_KEY))
        {
            string savedLang = PlayerPrefs.GetString(PREFS_LANG_KEY);
            if (Enum.TryParse(savedLang, out AppLanguage lang))
            {
                currentLanguage = lang;
            }
        }
        else
        {
            currentLanguage = Application.systemLanguage == SystemLanguage.Korean
                ? AppLanguage.KO
                : AppLanguage.EN;

            PlayerPrefs.SetString(PREFS_LANG_KEY, currentLanguage.ToString());
            PlayerPrefs.Save();
        }
    }

    private void LoadCSV()
    {
        m_LocalizedText = new Dictionary<string, Dictionary<AppLanguage, string>>();
        if (csvFile == null) return;

        string[] lines = csvFile.text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length <= 1) return;

        List<string> headers = ParseCSVLine(lines[0]);

        for (int i = 1; i < lines.Length; i++)
        {
            List<string> row = ParseCSVLine(lines[i]);
            if (row.Count == 0 || string.IsNullOrEmpty(row[0])) continue;

            string key = row[0];
            var langDict = new Dictionary<AppLanguage, string>();

            for (int j = 1; j < row.Count && j < headers.Count; j++)
            {
                if (Enum.TryParse(headers[j], out AppLanguage lang))
                {
                    langDict[lang] = row[j];
                }
            }
            m_LocalizedText[key] = langDict;
        }
    }

    private List<string> ParseCSVLine(string line)
    {
        List<string> result = new List<string>();
        bool inQuotes = false;
        int startIndex = 0;

        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '\"')
            {
                inQuotes = !inQuotes;
            }
            else if (line[i] == ',' && !inQuotes)
            {
                result.Add(CleanCSVValue(line.Substring(startIndex, i - startIndex)));
                startIndex = i + 1;
            }
        }

        result.Add(CleanCSVValue(line.Substring(startIndex)));
        return result;
    }

    private string CleanCSVValue(string value)
    {
        value = value.Trim();
        if (value.StartsWith("\"") && value.EndsWith("\""))
        {
            value = value.Substring(1, value.Length - 2);
            value = value.Replace("\"\"", "\"");
        }
        return value.Replace("\\n", "\n");
    }

    public string GetText(string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;

        if (m_LocalizedText != null && m_LocalizedText.TryGetValue(key, out var langDict))
        {
            if (langDict.TryGetValue(currentLanguage, out string translated))
            {
                return translated;
            }
        }
        return $"#{key}";
    }

    public void ChangeLanguage(AppLanguage newLanguage)
    {
        if (currentLanguage == newLanguage) return;

        currentLanguage = newLanguage;
        PlayerPrefs.SetString(PREFS_LANG_KEY, currentLanguage.ToString());
        PlayerPrefs.Save();
        OnLanguageChanged?.Invoke();
    }

    public void SwitchLanguage()
    {
        AppLanguage newLanguage = currentLanguage == AppLanguage.KO
            ? AppLanguage.EN
            : AppLanguage.KO;
        ChangeLanguage(newLanguage);
    }

#if UNITY_EDITOR
    private AppLanguage _lastLanguage;

    private void OnValidate()
    {
        if (Application.isPlaying && _lastLanguage != currentLanguage)
        {
            _lastLanguage = currentLanguage;
            PlayerPrefs.SetString(PREFS_LANG_KEY, currentLanguage.ToString());
            OnLanguageChanged?.Invoke();
        }
    }
#endif
}
