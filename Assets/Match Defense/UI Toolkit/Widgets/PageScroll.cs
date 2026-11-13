using System;
using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement]
public partial class PageScroll : VisualElement
{
    // --- 외부에서 구독할 수 있는 이벤트 ---
    
    // 스크롤이 움직일 때마다 호출 (드래그 및 스냅 애니메이션 중 모두 호출됨)
    // 0페이지면 0.0, 1페이지면 1.0, 1페이지에서 2페이지로 넘어가는 중간이면 1.5 등을 반환
    public event Action<float> OnScrollProgressChanged; 
    
    // 페이지가 완전히 넘어가서 스냅이 종료되었을 때 호출 (도착한 페이지 인덱스 반환)
    public event Action<int> OnPageSettled;
    
    // ------------------------------------

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

    // 위치를 이동시키고 이벤트를 발생시키는 공통 메서드
    private void UpdateOffset(float newOffset)
    {
        m_CurrentOffset = newOffset;
        m_ContentContainer.style.translate = new Translate(m_CurrentOffset, 0, 0);

        if (m_ViewWidth > 0)
        {
            // 스크롤 진행도를 계산 (예: 첫 화면 = 0.0, 다음 화면 = 1.0)
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
            OnPageSettled?.Invoke(m_CurrentPage); // 애니메이션 없이 즉시 안착 시 이벤트 호출
        }
    }

    private void AnimateSnap()
    {
        float newOffset = Mathf.Lerp(m_CurrentOffset, m_TargetOffset, 0.15f);
        UpdateOffset(newOffset);

        // 목표 지점에 도달했을 때 (오차 0.5 픽셀 이내)
        if (Mathf.Abs(m_CurrentOffset - m_TargetOffset) < 0.5f)
        {
            UpdateOffset(m_TargetOffset);
            m_AnimationTask.Pause();
            
            // 페이지가 완전히 넘어간 상태를 외부에 알림
            OnPageSettled?.Invoke(m_CurrentPage);
        }
    }
}