using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement]
public partial class StageSelect : VisualElement
{
    #region UI Elements & Layout Attributes

    [UxmlAttribute]
    public string LocalizationKey
    {
        get => m_LocalizationKey;
        set
        {
            m_LocalizationKey = value;
            if (m_Label != null) m_Label.LocalizationKey = value;
        }
    }
    private string m_LocalizationKey;

    [UxmlAttribute]
    public Sprite EmptyStar
    {
        get => m_EmptyStar;
        set { m_EmptyStar = value; UpdateStars(); }
    }
    private Sprite m_EmptyStar;

    [UxmlAttribute]
    public Sprite FilledStar
    {
        get => m_FilledStar;
        set { m_FilledStar = value; UpdateStars(); }
    }
    private Sprite m_FilledStar;

    [UxmlAttribute]
    public int FilledCount
    {
        get => m_FilledCount;
        set { m_FilledCount = Mathf.Clamp(value, 0, 5); UpdateStars(); }
    }
    private int m_FilledCount = 0;

    [UxmlAttribute]
    public Color FilledColor
    {
        get => m_FilledColor;
        set { m_FilledColor = value; UpdateStars(); }
    }
    private Color m_FilledColor = Color.white;

    #endregion


    #region Text Style Attributes

    [UxmlAttribute]
    public Font CustomFont
    {
        get => m_CustomFont;
        set
        {
            m_CustomFont = value;
            if (m_Label != null)
            {
                m_Label.style.unityFont = value;
                m_Label.style.unityFontDefinition = new StyleFontDefinition(StyleKeyword.None);
            }
        }
    }
    private Font m_CustomFont;

    [UxmlAttribute]
    public float FontSize
    {
        get => m_FontSize;
        set
        {
            m_FontSize = value;
            if (m_Label != null) m_Label.FontSize = value;
        }
    }
    private float m_FontSize = 7f;

    [UxmlAttribute]
    public Color TextColor
    {
        get => m_TextColor;
        set { m_TextColor = value; if (m_Label != null) m_Label.style.color = value; }
    }
    private Color m_TextColor = Color.white;

    [UxmlAttribute]
    public float OutlineWidthVW
    {
        get => m_OutlineWidthVW;
        set { m_OutlineWidthVW = value; if (m_Label != null) m_Label.OutlineWidthVW = value; }
    }
    private float m_OutlineWidthVW = 0.06f; // 기존 0.6 픽셀 정도의 느낌을 내는 vw값

    [UxmlAttribute]
    public Color OutlineColor
    {
        get => m_OutlineColor;
        set { m_OutlineColor = value; if (m_Label != null) m_Label.style.unityTextOutlineColor = value; }
    }
    private Color m_OutlineColor = new Color(183f / 255f, 111f / 255f, 32f / 255f, 1f);

    [UxmlAttribute]
    public Vector2 ShadowOffsetVW
    {
        get => m_ShadowOffsetVW;
        set { m_ShadowOffsetVW = value; if (m_Label != null) m_Label.ShadowOffsetVW = value; }
    }
    private Vector2 m_ShadowOffsetVW = new Vector2(0f, -0.5f); // 기존 UXML의 0 -10px 느낌

    [UxmlAttribute]
    public float ShadowBlurVW
    {
        get => m_ShadowBlurVW;
        set { m_ShadowBlurVW = value; if (m_Label != null) m_Label.ShadowBlurVW = value; }
    }
    private float m_ShadowBlurVW = 1.5f;

    [UxmlAttribute]
    public Color ShadowColor
    {
        get => m_ShadowColor;
        set { m_ShadowColor = value; if (m_Label != null) m_Label.ShadowColor = value; }
    }
    private Color m_ShadowColor = new Color(1f, 168f / 255f, 0f, 1f);

    #endregion


    #region Initialization & Layout

    // [수정됨] Label 대신 LocalizedLabel 사용
    private LocalizedLabel m_Label;
    private VisualElement m_StarsContainer;
    private Image[] m_StarImages = new Image[5];

    public StageSelect()
    {
        name = "stage-item";
        style.flexGrow = 1;
        style.height = Length.Percent(100);
        style.justifyContent = Justify.SpaceBetween;
        style.alignContent = Align.Stretch;
        style.paddingRight = Length.Percent(10);
        style.paddingLeft = Length.Percent(10);

        m_Label = new LocalizedLabel();
        m_Label.name = "stage-sub_1";
        m_Label.AddToClassList("clay-text");

        m_Label.LocalizationKey = m_LocalizationKey;
        m_Label.FontSize = m_FontSize;
        m_Label.OutlineWidthVW = m_OutlineWidthVW;
        m_Label.ShadowOffsetVW = m_ShadowOffsetVW;
        m_Label.ShadowBlurVW = m_ShadowBlurVW;
        m_Label.ShadowColor = m_ShadowColor;

        m_Label.style.unityFontStyleAndWeight = FontStyle.Normal;
        m_Label.style.unityTextAlign = TextAnchor.MiddleCenter;
        m_Label.style.marginLeft = 0;
        m_Label.style.marginRight = 0;
        m_Label.style.marginTop = 0;
        m_Label.style.marginBottom = 0;
        m_Label.style.paddingTop = 0;
        m_Label.style.paddingRight = 0;
        m_Label.style.paddingBottom = 0;
        m_Label.style.paddingLeft = 0;
        m_Label.style.letterSpacing = -4f;
        m_Label.style.flexGrow = 1;
        m_Label.style.flexShrink = 1;

        hierarchy.Add(m_Label);

        m_StarsContainer = new VisualElement();
        m_StarsContainer.name = "stage-stars";
        m_StarsContainer.style.flexDirection = FlexDirection.Row;
        m_StarsContainer.style.alignContent = Align.Stretch;
        m_StarsContainer.style.alignSelf = Align.Center;
        m_StarsContainer.style.justifyContent = Justify.SpaceBetween;
        m_StarsContainer.style.width = Length.Percent(60);
        m_StarsContainer.style.height = Length.Percent(40);

        for (int i = 0; i < 5; i++)
        {
            var starImg = new Image();
            starImg.name = "star-image";
            starImg.style.width = new StyleLength(StyleKeyword.Auto);

            m_StarImages[i] = starImg;
            m_StarsContainer.Add(starImg);
        }

        hierarchy.Add(m_StarsContainer);

        UpdateStars();
        UpdateTextStyles();
    }

    #endregion


    #region Update Logic

    private void UpdateStars()
    {
        if (m_StarImages == null) return;

        for (int i = 0; i < 5; i++)
        {
            if (m_StarImages[i] == null) continue;

            if (i < m_FilledCount)
            {
                m_StarImages[i].sprite = m_FilledStar;
                m_StarImages[i].tintColor = m_FilledColor;
            }
            else
            {
                m_StarImages[i].sprite = m_EmptyStar;
                m_StarImages[i].tintColor = Color.white;
            }
        }
    }

    private void UpdateTextStyles()
    {
        if (m_Label == null) return;

        if (m_CustomFont != null)
        {
            m_Label.style.unityFont = m_CustomFont;
            m_Label.style.unityFontDefinition = new StyleFontDefinition(StyleKeyword.None);
        }

        m_Label.style.color = m_TextColor;
        m_Label.style.unityTextOutlineColor = m_OutlineColor;
    }

    #endregion
}