using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement]
public partial class SafeArea : VisualElement
{
    public SafeArea()
    {
        if (panel != null)
        {
            panel.visualTree.RegisterCallback<GeometryChangedEvent>(UpdateGeometry);
        }
        else
        {
            RegisterCallback<GeometryChangedEvent>(UpdateGeometry);
        }
    }
    
    private void UpdateGeometry(GeometryChangedEvent evt)
    {
        if (panel == null)
            return;

#if UNITY_EDITOR
        if (panel.contextType == ContextType.Editor)
        {
            return;
        }
#endif

        Rect safeArea = Screen.safeArea;
        float screenHeight = (float)Screen.height;

        Vector2 safeAreaLeftTop = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(safeArea.xMin, screenHeight - safeArea.yMax));
        Vector2 safeAreaRightBottom = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(Screen.width - safeArea.xMax, safeArea.yMin));

        style.paddingLeft = safeAreaLeftTop.x;
        style.paddingTop = safeAreaLeftTop.y;
        style.paddingRight = safeAreaRightBottom.x;
        style.paddingBottom = safeAreaRightBottom.y;
    }
}