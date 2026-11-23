using System;
using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement]
public partial class SafeArea : VisualElement
{
    private readonly Action applyAction;
    private VisualElement registeredRoot;
    private IVisualElementScheduledItem pollTask;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;

    public SafeArea()
    {
        applyAction = ApplySafeArea;

        style.position = Position.Absolute;
        style.left = Length.Percent(0f);
        style.top = Length.Percent(0f);
        style.right = Length.Percent(0f);
        style.bottom = Length.Percent(0f);
        style.width = StyleKeyword.Auto;
        style.height = StyleKeyword.Auto;

        RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
    }

    #region Panel Lifecycle
    private void OnAttachToPanel(AttachToPanelEvent evt)
    {
        UnregisterRoot();

        if (evt.destinationPanel == null) return;

        registeredRoot = evt.destinationPanel.visualTree;
        registeredRoot?.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);

        lastSafeArea = Rect.zero;
        lastScreenSize = Vector2Int.zero;
        ApplySafeArea();

        if (pollTask == null)
            pollTask = schedule.Execute(applyAction).Every(200);
        else
            pollTask.Resume();
    }

    private void OnDetachFromPanel(DetachFromPanelEvent evt)
    {
        pollTask?.Pause();
        UnregisterRoot();
    }

    private void UnregisterRoot()
    {
        if (registeredRoot != null)
        {
            registeredRoot.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            registeredRoot = null;
        }
    }

    private void OnRootGeometryChanged(GeometryChangedEvent evt)
    {
        ApplySafeArea();
    }
    #endregion

    #region Layout
    public void ApplySafeArea()
    {
        if (panel == null) return;

#if UNITY_EDITOR
        if (panel.contextType == ContextType.Editor) return;
#endif

        int screenW = Screen.width;
        int screenH = Screen.height;
        if (screenW <= 0 || screenH <= 0) return;

        Rect safeArea = Screen.safeArea;
        Vector2Int screenSize = new Vector2Int(screenW, screenH);
        if (safeArea == lastSafeArea && screenSize == lastScreenSize) return;

        lastSafeArea = safeArea;
        lastScreenSize = screenSize;

        float leftPct = (safeArea.xMin / screenW) * 100f;
        float rightPct = ((screenW - safeArea.xMax) / screenW) * 100f;
        float topPct = ((screenH - safeArea.yMax) / screenH) * 100f;
        float bottomPct = (safeArea.yMin / screenH) * 100f;

        style.position = Position.Absolute;
        style.width = StyleKeyword.Auto;
        style.height = StyleKeyword.Auto;
        style.left = Length.Percent(leftPct);
        style.right = Length.Percent(rightPct);
        style.top = Length.Percent(topPct);
        style.bottom = Length.Percent(bottomPct);
    }
    #endregion
}