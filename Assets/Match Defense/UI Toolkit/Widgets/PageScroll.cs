using System;
using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement]
public partial class PageScroll : VisualElement
{
    public event Action<float> OnScrollProgressChanged;
    public event Action<int> OnPageSettled;

    public int CurrentPage => m_CurrentPage;
    public int PageCount => m_ContentContainer != null ? m_ContentContainer.childCount : 0;

    private VisualElement m_ContentContainer;
    public override VisualElement contentContainer => m_ContentContainer;

    private int m_CurrentPage = 0;
    private float m_CurrentOffset = 0f;
    private float m_TargetOffset = 0f;

    private bool m_IsDragging = false;
    private float m_PointerStartX;
    private float m_OffsetStartX;

    private float m_ViewWidth;
    private IVisualElementScheduledItem m_AnimationTask;

    public PageScroll()
    {
        style.overflow = Overflow.Hidden;
        style.flexGrow = 1;

        m_ContentContainer = new VisualElement();
        m_ContentContainer.style.flexDirection = FlexDirection.Row;
        m_ContentContainer.style.height = Length.Percent(100);
        hierarchy.Add(m_ContentContainer);

        RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

        RegisterCallback<PointerDownEvent>(OnPointerDown);
        RegisterCallback<PointerMoveEvent>(OnPointerMove);
        RegisterCallback<PointerUpEvent>(OnPointerUp);
        RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        RegisterCallback<PointerCancelEvent>(OnPointerCancel);

        m_AnimationTask = schedule.Execute(AnimateSnap).Every(16);
        m_AnimationTask.Pause();
    }

    public void GoToPage(int pageIndex, bool animate = true)
    {
        int maxIdx = Mathf.Max(0, m_ContentContainer.childCount - 1);
        int clamped = Mathf.Clamp(pageIndex, 0, maxIdx);

        if (m_ViewWidth <= 0f)
        {
            m_CurrentPage = clamped;
            return;
        }

        SnapToPage(clamped, animate);
    }

    public void BeginExternalDrag(float pointerX)
    {
        if (m_ViewWidth <= 0f) return;
        m_IsDragging = true;
        m_PointerStartX = pointerX;
        m_OffsetStartX = m_CurrentOffset;
        m_AnimationTask.Pause();
    }

    public void UpdateExternalDrag(float pointerX)
    {
        if (!m_IsDragging) return;
        float delta = pointerX - m_PointerStartX;
        UpdateOffset(m_OffsetStartX + delta);
    }

    public void EndExternalDrag(float pointerX)
    {
        if (!m_IsDragging) return;
        EndDrag(pointerX);
    }

    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        m_ViewWidth = resolvedStyle.width;
        if (m_ViewWidth <= 0) return;

        foreach (var child in m_ContentContainer.Children())
        {
            child.style.width = m_ViewWidth;
            child.style.flexShrink = 0;
        }

        SnapToPage(m_CurrentPage, false);
    }

    private void UpdateOffset(float newOffset)
    {
        m_CurrentOffset = newOffset;
        m_ContentContainer.style.translate = new Translate(m_CurrentOffset, 0, 0);

        if (m_ViewWidth > 0)
        {
            float progress = -m_CurrentOffset / m_ViewWidth;
            OnScrollProgressChanged?.Invoke(progress);
        }
    }

    private void OnPointerDown(PointerDownEvent evt)
    {
        if (evt.button != 0) return;

        this.CapturePointer(evt.pointerId);
        m_IsDragging = true;
        m_PointerStartX = evt.position.x;
        m_OffsetStartX = m_CurrentOffset;

        m_AnimationTask.Pause();
        evt.StopPropagation();
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (!m_IsDragging || !this.HasPointerCapture(evt.pointerId)) return;

        float delta = evt.position.x - m_PointerStartX;
        UpdateOffset(m_OffsetStartX + delta);

        evt.StopPropagation();
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (!m_IsDragging || !this.HasPointerCapture(evt.pointerId)) return;

        EndDrag(evt.position.x);
        this.ReleasePointer(evt.pointerId);
        evt.StopPropagation();
    }

    private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
    {
        if (!m_IsDragging) return;
        EndDrag(m_PointerStartX);
    }

    private void OnPointerCancel(PointerCancelEvent evt)
    {
        if (!m_IsDragging) return;
        EndDrag(m_PointerStartX);
        this.ReleasePointer(evt.pointerId);
    }

    private void EndDrag(float endX)
    {
        m_IsDragging = false;
        float dragDistance = endX - m_PointerStartX;
        float threshold = m_ViewWidth * 0.15f;

        if (dragDistance < -threshold && m_CurrentPage < m_ContentContainer.childCount - 1)
        {
            m_CurrentPage++;
        }
        else if (dragDistance > threshold && m_CurrentPage > 0)
        {
            m_CurrentPage--;
        }

        SnapToPage(m_CurrentPage, true);
    }

    private void SnapToPage(int page, bool animate)
    {
        m_CurrentPage = Mathf.Clamp(page, 0, Mathf.Max(0, m_ContentContainer.childCount - 1));
        m_TargetOffset = -m_CurrentPage * m_ViewWidth;

        if (animate)
        {
            m_AnimationTask.Resume();
        }
        else
        {
            UpdateOffset(m_TargetOffset);
            OnPageSettled?.Invoke(m_CurrentPage);
        }
    }

    private void AnimateSnap()
    {
        float newOffset = Mathf.Lerp(m_CurrentOffset, m_TargetOffset, 0.15f);
        UpdateOffset(newOffset);

        if (Mathf.Abs(m_CurrentOffset - m_TargetOffset) < 0.5f)
        {
            UpdateOffset(m_TargetOffset);
            m_AnimationTask.Pause();
            OnPageSettled?.Invoke(m_CurrentPage);
        }
    }
}
