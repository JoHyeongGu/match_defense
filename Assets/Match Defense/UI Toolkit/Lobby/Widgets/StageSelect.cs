using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement]
public partial class StageSelect : VisualElement
{
    #region UI Elements & Layout Attributes

    [UxmlAttribute]
    public string SubText
    {
        get => m_Label.text;
        set => m_Label.text = value;
    }

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
            UpdateFontSize();
        }
    }
    private float m_FontSize = 5f;

    [UxmlAttribute]
    public Color TextColor
    {
        get => m_TextColor;
        set { m_TextColor = value; if (m_Label != null) m_Label.style.color = value; }
    }
    private Color m_TextColor = Color.white;

    [UxmlAttribute]
    public float OutlineWidth
    {
        get => m_OutlineWidth;
        set { m_OutlineWidth = value; if (m_Label != null) m_Label.style.unityTextOutlineWidth = value; }
    }
    private float m_OutlineWidth = 0.6f;

    [UxmlAttribute]
    public Color OutlineColor
    {
        get => m_OutlineColor;
        set { m_OutlineColor = value; if (m_Label != null) m_Label.style.unityTextOutlineColor = value; }
    }
    private Color m_OutlineColor = new Color(183f / 255f, 111f / 255f, 32f / 255f, 1f);

    #endregion


    #region Initialization & Layout

    private Label m_Label;
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

        m_Label = new Label("Keep your important things safe!");
        m_Label.name = "stage-sub_1";
        m_Label.AddToClassList("clay-text");

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

        RegisterCallback<GeometryChangedEvent>(evt => UpdateFontSize());

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
        m_Label.style.unityTextOutlineWidth = m_OutlineWidth;
        m_Label.style.unityTextOutlineColor = m_OutlineColor;

        UpdateFontSize();
    }

    private void UpdateFontSize()
    {
        if (m_Label == null) return;

        float currentWidth = resolvedStyle.width;

        if (currentWidth > 0)
        {
            m_Label.style.fontSize = currentWidth * (m_FontSize / 100f);
        }
    }

    #endregion
}