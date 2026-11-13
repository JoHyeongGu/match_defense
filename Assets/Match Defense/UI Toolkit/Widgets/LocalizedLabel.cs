using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement]
public partial class LocalizedLabel : Label
{
    [UxmlAttribute("localization-key")]
    public string LocalizationKey
    {
        get => m_LocalizationKey;
        set { m_LocalizationKey = value; UpdateText(); }
    }
    private string m_LocalizationKey;

    [UxmlAttribute]
    public float FontSize
    {
        get => m_FontSize;
        set { m_FontSize = value; UpdateResponsiveStyles(); }
    }
    private float m_FontSize = 5f;

    [UxmlAttribute]
    public float OutlineWidthVW
    {
        get => m_OutlineWidthVW;
        set { m_OutlineWidthVW = value; UpdateResponsiveStyles(); }
    }
    private float m_OutlineWidthVW = 0f;

    [UxmlAttribute]
    public Vector2 ShadowOffsetVW
    {
        get => m_ShadowOffsetVW;
        set { m_ShadowOffsetVW = value; UpdateResponsiveStyles(); }
    }
    private Vector2 m_ShadowOffsetVW = Vector2.zero;

    [UxmlAttribute]
    public float ShadowBlurVW
    {
        get => m_ShadowBlurVW;
        set { m_ShadowBlurVW = value; UpdateResponsiveStyles(); }
    }
    private float m_ShadowBlurVW = 0f;

    [UxmlAttribute]
    public Color ShadowColor
    {
        get => m_ShadowColor;
        set { m_ShadowColor = value; UpdateResponsiveStyles(); }
    }
    private Color m_ShadowColor = Color.clear;


    public LocalizedLabel()
    {
        RegisterCallback<AttachToPanelEvent>(OnAttach);
        RegisterCallback<DetachFromPanelEvent>(OnDetach);

        RegisterCallback<GeometryChangedEvent>(evt => UpdateResponsiveStyles());
    }

    private void OnAttach(AttachToPanelEvent evt)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.OnLanguageChanged += UpdateText;
            UpdateText();
        }
    }

    private void OnDetach(DetachFromPanelEvent evt)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.OnLanguageChanged -= UpdateText;
        }
    }

    private void UpdateText()
    {
        if (UIManager.Instance != null && !string.IsNullOrEmpty(m_LocalizationKey))
        {
            this.text = UIManager.Instance.GetText(m_LocalizationKey);
        }
    }

    private void UpdateResponsiveStyles()
    {
        float currentWidth = resolvedStyle.width;
        if (currentWidth > 0)
        {
            float vw = currentWidth / 100f;

            if (m_FontSize > 0)
                this.style.fontSize = vw * m_FontSize;

            if (m_OutlineWidthVW > 0)
                this.style.unityTextOutlineWidth = vw * m_OutlineWidthVW;

            if (m_ShadowColor.a > 0)
            {
                this.style.textShadow = new StyleTextShadow(new TextShadow
                {
                    offset = new Vector2(vw * m_ShadowOffsetVW.x, vw * m_ShadowOffsetVW.y),
                    blurRadius = vw * m_ShadowBlurVW,
                    color = m_ShadowColor
                });
            }
            else
            {
                this.style.textShadow = new StyleTextShadow(StyleKeyword.None);
            }
        }
    }
}