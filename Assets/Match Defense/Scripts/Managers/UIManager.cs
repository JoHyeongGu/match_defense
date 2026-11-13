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
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadCSV();
            InitializeLanguage();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeLanguage()
    {
        // 1. 기존에 저장된 언어 설정이 있는지 확인
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
            // 2. 저장된 설정이 없다면 사용자 기기의 OS 언어를 감지
            if (Application.systemLanguage == SystemLanguage.Korean)
            {
                currentLanguage = AppLanguage.KO;
            }
            else // 한국어가 아니면 기본 언어인 EN으로 설정 (일본어, 중국어 등 포함)
            {
                currentLanguage = AppLanguage.EN;
            }

            // 첫 감지된 언어를 저장
            PlayerPrefs.SetString(PREFS_LANG_KEY, currentLanguage.ToString());
            PlayerPrefs.Save();
        }
    }

    private void LoadCSV()
    {
        m_LocalizedText = new Dictionary<string, Dictionary<AppLanguage, string>>();
        if (csvFile == null) return;

        // CSV 줄 단위 파싱
        string[] lines = csvFile.text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length <= 1) return;

        // 첫 번째 줄(헤더) 파싱
        List<string> headers = ParseCSVLine(lines[0]);

        // 두 번째 줄부터 데이터 파싱
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
                    langDict[lang] = row[j]; // 헬퍼 메서드에서 이미 정제(Trim, 따옴표 제거)되어 나옵니다.
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
                inQuotes = !inQuotes; // 따옴표 안팎 상태 반전
            }
            else if (line[i] == ',' && !inQuotes)
            {
                // 따옴표 밖의 쉼표(진짜 구분자)일 때만 자르기
                result.Add(CleanCSVValue(line.Substring(startIndex, i - startIndex)));
                startIndex = i + 1;
            }
        }

        // 마지막 열 데이터 추가
        result.Add(CleanCSVValue(line.Substring(startIndex)));

        return result;
    }

    // [새로 추가] 양끝 큰따옴표 및 이스케이프 문자(\n, "")를 정리하는 메서드
    private string CleanCSVValue(string value)
    {
        value = value.Trim();
        if (value.StartsWith("\"") && value.EndsWith("\""))
        {
            value = value.Substring(1, value.Length - 2); // 양끝 껍데기 따옴표 제거
            value = value.Replace("\"\"", "\"");          // 텍스트 내부의 "" 를 " 로 복원
        }
        return value.Replace("\\n", "\n");
    }

    // 텍스트를 요청받는 메서드
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
        return $"#{key}"; // 번역 데이터가 없으면 키값을 그대로 노출 (디버깅 용이)
    }

    // 외부(설정 창 등)에서 언어를 변경할 때 호출하는 메서드
    public void ChangeLanguage(AppLanguage newLanguage)
    {
        if (currentLanguage == newLanguage) return;

        currentLanguage = newLanguage;

        // 1. 변경된 설정을 저장 (WebGL, Android 모두 정상 작동)
        PlayerPrefs.SetString(PREFS_LANG_KEY, currentLanguage.ToString());
        PlayerPrefs.Save(); // WebGL 환경에서는 즉시 저장을 위해 .Save() 호출이 중요합니다.

        // 2. 현재 씬에 띄워진 모든 LocalizedLabel 위젯의 텍스트를 즉시 갱신
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

    // 인스펙터에서 값이 변경될 때마다 에디터가 자동으로 호출하는 유니티 내장 함수
    private void OnValidate()
    {
        // 플레이 모드 중에 인스펙터에서 언어를 바꿨을 때만 실행
        if (Application.isPlaying && _lastLanguage != currentLanguage)
        {
            _lastLanguage = currentLanguage;

            // 변수는 이미 인스펙터에서 바뀌었으니, 값 저장과 텍스트 갱신 이벤트만 강제로 호출해줍니다.
            PlayerPrefs.SetString(PREFS_LANG_KEY, currentLanguage.ToString());
            OnLanguageChanged?.Invoke();
        }
    }
#endif
}