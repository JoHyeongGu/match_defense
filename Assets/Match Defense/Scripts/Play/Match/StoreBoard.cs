using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

namespace MatchDefense.Match
{
    public enum SliderFillMode
    {
        RadialU,
        Horizontal,
        Vertical
    }

    [UxmlElement]
    public partial class StoreBoard : VisualElement
    {
        private const int MaxSlots = 5;

        private const string BoardClass = "store-board";
        private const string FrameClass = "store-frame";
        private const string FrameLockedClass = "store-frame--locked";
        private const string ThumbClass = "store-thumb";
        private const string SliderClass = "store-slider";
        private const string CountClass = "store-count";
        private const string LockOverlayClass = "store-lock-overlay";
        private const string LockIconClass = "store-lock-icon";

        private Sprite sliderSprite;
        private Sprite lockSprite;
        private SliderFillMode fillMode = SliderFillMode.RadialU;
        private float previewFill = 0.5f;
        private float slotSpacing = 0.04f;
        private float frameScale = 1.5f;
        private float countFontScale = 0.22f;
        private float fillDuration = 0.4f;
        private Color dimThumbColor = new Color(0.08f, 0.08f, 0.08f, 0.4f);

        [UxmlAttribute]
        public Sprite SliderSprite
        {
            get => sliderSprite;
            set { sliderSprite = value; ApplySliderSettings(); }
        }

        [UxmlAttribute]
        public Sprite LockSprite
        {
            get => lockSprite;
            set { lockSprite = value; ApplyLockSprite(); }
        }

        [UxmlAttribute]
        public SliderFillMode FillMode
        {
            get => fillMode;
            set { fillMode = value; ApplySliderSettings(); }
        }

        [UxmlAttribute]
        public float PreviewFill
        {
            get => previewFill;
            set { previewFill = Mathf.Clamp01(value); ApplyPreviewFill(); }
        }

        [UxmlAttribute]
        public float SlotSpacing
        {
            get => slotSpacing;
            set { slotSpacing = Mathf.Max(0f, value); UpdateBoardLayout(); }
        }

        [UxmlAttribute]
        public float FrameScale
        {
            get => frameScale;
            set { frameScale = Mathf.Max(0.1f, value); UpdateBoardLayout(); }
        }

        [UxmlAttribute]
        public float CountFontScale
        {
            get => countFontScale;
            set { countFontScale = Mathf.Max(0.05f, value); UpdateBoardLayout(); }
        }

        [UxmlAttribute]
        public float FillDuration
        {
            get => fillDuration;
            set { fillDuration = Mathf.Max(0.05f, value); }
        }

        [UxmlAttribute]
        public Color DimThumbColor
        {
            get => dimThumbColor;
            set { dimThumbColor = value; ApplyDimThumbColor(); }
        }

        private readonly StoreSlotElement[] slots = new StoreSlotElement[MaxSlots];

        private MonoBehaviour coroutineRunner;
        private bool isInitialized;

        public StoreBoard()
        {
            AddToClassList(BoardClass);

            for (int i = 0; i < MaxSlots; i++)
            {
                var slot = new StoreSlotElement(i);
                slots[i] = slot;
                Add(slot);
            }

            ApplyDimThumbColor();
            ApplyLockSprite();
            ApplyPreviewFill();
            RegisterCallback<GeometryChangedEvent>(_ => UpdateBoardLayout());
            RegisterCallback<DetachFromPanelEvent>(_ => CleanupAllSlots());
        }

        public void Initialize(
            MonoBehaviour runner,
            MatchUnitData[] unitDatas,
            Action<MatchUnitData> beginDragCallback,
            Action<Vector2> dragCallback,
            Func<bool, bool> endDragCallback)
        {
            coroutineRunner = runner;
            isInitialized = true;

            for (int i = 0; i < MaxSlots; i++)
            {
                slots[i].SetSliderVisual(sliderSprite, fillMode);
                slots[i].SetDimThumbColor(dimThumbColor);
                slots[i].SetLockSprite(lockSprite);

                if (unitDatas != null && i < unitDatas.Length && unitDatas[i] != null)
                    slots[i].Bind(runner, unitDatas[i], beginDragCallback, dragCallback, endDragCallback);
                else
                    slots[i].SetLockedEmpty();
            }

            UpdateBoardLayout();
        }

        public void AddCharge(int unitIndex, int matchCount)
        {
            if (coroutineRunner == null || unitIndex < 0 || unitIndex >= MaxSlots) return;
            if (!slots[unitIndex].IsBound || !slots[unitIndex].IsUnlocked) return;
            slots[unitIndex].EnqueueCharge(matchCount, fillDuration);
        }

        public void EnsureAtLeastOneUnit(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxSlots) return;
            if (!slots[slotIndex].IsBound || !slots[slotIndex].IsUnlocked) return;
            if (slots[slotIndex].AvailableUnits <= 0)
                slots[slotIndex].GrantAvailableUnitsImmediate(1);
        }

        public void GrantTutorialUnit(int slotIndex, int count = 1)
        {
            if (slotIndex < 0 || slotIndex >= MaxSlots) return;
            if (!slots[slotIndex].IsBound || !slots[slotIndex].IsUnlocked) return;
            slots[slotIndex].GrantAvailableUnitsImmediate(count);
        }

        public void ResetAllSlots()
        {
            for (int i = 0; i < MaxSlots; i++)
                slots[i].ResetState();
        }

        private void UpdateBoardLayout()
        {
            float availW = contentRect.width;
            float availH = contentRect.height;
            if (float.IsNaN(availW) || float.IsNaN(availH) || availW <= 0f || availH <= 0f) return;

            float denomX = MaxSlots + (MaxSlots - 1) * slotSpacing;
            float frameSize = Mathf.Max(0f, Mathf.Min(availW / denomX, availH)) * frameScale;
            float fontSize = frameSize * countFontScale;

            for (int i = 0; i < MaxSlots; i++)
                slots[i]?.SetFrameLayout(frameSize, fontSize);
        }

        private void ApplySliderSettings()
        {
            for (int i = 0; i < MaxSlots; i++)
                slots[i]?.SetSliderVisual(sliderSprite, fillMode);
        }

        private void ApplyLockSprite()
        {
            for (int i = 0; i < MaxSlots; i++)
                slots[i]?.SetLockSprite(lockSprite);
        }

        private void ApplyDimThumbColor()
        {
            for (int i = 0; i < MaxSlots; i++)
                slots[i]?.SetDimThumbColor(dimThumbColor);
        }

        private void ApplyPreviewFill()
        {
            if (isInitialized) return;
            for (int i = 0; i < MaxSlots; i++)
                slots[i]?.SetPreviewFill(previewFill);
        }

        private void CleanupAllSlots()
        {
            for (int i = 0; i < MaxSlots; i++)
                slots[i]?.StopAndKill();
        }

        #region Slot Element
        private class StoreSlotElement : VisualElement
        {
            public bool IsBound { get; private set; }
            public bool IsUnlocked { get; private set; }
            public int AvailableUnits => availableUnits;

            private MonoBehaviour runner;
            private MatchUnitData data;
            private Action<MatchUnitData> onBeginDrag;
            private Action<Vector2> onDrag;
            private Func<bool, bool> onEndDrag;

            private readonly VisualElement thumbRoot;
            private readonly VisualElement dimThumb;
            private readonly VisualElement thumbMask;
            private readonly VisualElement brightThumb;
            private readonly FilledSpriteElement sliderElement;
            private readonly Label countLabel;
            private readonly VisualElement lockOverlay;
            private readonly Label lockFallbackLabel;

            private readonly Queue<int> pendingMatches = new Queue<int>();
            private Coroutine chargeCoroutine;

            private int charge;
            private int availableUnits;
            private bool isDragging;
            private int activePointerId = -1;

            private float currentSliderFill;
            private float currentThumbFill;
            private Tween sliderTween;
            private Tween thumbTween;
            private Sequence bounceSeq;
            private Sequence sliderBounceSeq;

            public StoreSlotElement(int slotIndex)
            {
                name = $"store-slot-{slotIndex}";
                AddToClassList(FrameClass);
                style.flexGrow = 0;
                style.flexShrink = 0;

                thumbRoot = new VisualElement { pickingMode = PickingMode.Ignore };
                thumbRoot.AddToClassList(ThumbClass);
                thumbRoot.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));

                dimThumb = CreateOverlayElement();
                ApplyContainSprite(dimThumb);

                thumbMask = new VisualElement { pickingMode = PickingMode.Ignore };
                thumbMask.style.position = Position.Absolute;
                thumbMask.style.left = 0;
                thumbMask.style.right = 0;
                thumbMask.style.bottom = 0;
                thumbMask.style.height = Length.Percent(0f);
                thumbMask.style.overflow = Overflow.Hidden;

                brightThumb = new VisualElement { pickingMode = PickingMode.Ignore };
                brightThumb.style.position = Position.Absolute;
                brightThumb.style.left = 0;
                brightThumb.style.bottom = 0;
                ApplyContainSprite(brightThumb);

                thumbMask.Add(brightThumb);
                thumbRoot.Add(dimThumb);
                thumbRoot.Add(thumbMask);

                thumbRoot.RegisterCallback<GeometryChangedEvent>(evt =>
                {
                    brightThumb.style.width = evt.newRect.width;
                    brightThumb.style.height = evt.newRect.height;
                });

                sliderElement = new FilledSpriteElement { pickingMode = PickingMode.Ignore };
                sliderElement.AddToClassList(SliderClass);
                sliderElement.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));

                countLabel = new Label("0") { pickingMode = PickingMode.Ignore };
                countLabel.AddToClassList(CountClass);

                lockOverlay = CreateOverlayElement();
                lockOverlay.AddToClassList(LockOverlayClass);
                lockOverlay.style.justifyContent = Justify.Center;
                lockOverlay.style.alignItems = Align.Center;
                lockOverlay.style.display = DisplayStyle.None;

                lockFallbackLabel = new Label("LOCK") { pickingMode = PickingMode.Ignore };
                lockFallbackLabel.AddToClassList(LockIconClass);
                lockOverlay.Add(lockFallbackLabel);

                Add(thumbRoot);
                Add(sliderElement);
                Add(countLabel);
                Add(lockOverlay);

                RegisterCallback<PointerDownEvent>(OnPointerDown);
                RegisterCallback<PointerMoveEvent>(OnPointerMove);
                RegisterCallback<PointerUpEvent>(OnPointerUp);
                RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            }

            public void SetFrameLayout(float size, float fontSize)
            {
                style.width = size;
                style.height = size;
                countLabel.style.fontSize = fontSize;
                lockFallbackLabel.style.fontSize = Mathf.Max(10f, fontSize * 0.85f);
            }

            public void SetSliderVisual(Sprite sprite, SliderFillMode mode)
            {
                sliderElement.SetConfig(sprite, mode);
            }

            public void SetLockSprite(Sprite sprite)
            {
                if (sprite != null)
                {
                    lockFallbackLabel.text = string.Empty;
                    lockFallbackLabel.style.backgroundImage = new StyleBackground(sprite);
                    ApplyContainSprite(lockFallbackLabel);
                    lockFallbackLabel.style.width = Length.Percent(46f);
                    lockFallbackLabel.style.height = Length.Percent(46f);
                }
                else
                {
                    lockFallbackLabel.style.backgroundImage = StyleKeyword.None;
                    lockFallbackLabel.text = "LOCK";
                }
            }

            public void SetDimThumbColor(Color color)
            {
                dimThumb.style.unityBackgroundImageTintColor = color;
            }

            public void SetPreviewFill(float value)
            {
                currentSliderFill = Mathf.Clamp01(value);
                sliderElement.FillAmount = currentSliderFill;
            }

            public void Bind(
                MonoBehaviour coroutineRunner,
                MatchUnitData unitData,
                Action<MatchUnitData> beginDrag,
                Action<Vector2> drag,
                Func<bool, bool> endDrag)
            {
                runner = coroutineRunner;
                data = unitData;
                onBeginDrag = beginDrag;
                onDrag = drag;
                onEndDrag = endDrag;
                IsBound = true;
                IsUnlocked = unitData.isUnlocked;

                Sprite thumbSprite = data.thumbnailSprite != null
                    ? data.thumbnailSprite
                    : data.itemVisual.defaultSprite;

                var thumbBg = thumbSprite != null ? new StyleBackground(thumbSprite) : StyleKeyword.None;
                dimThumb.style.backgroundImage = thumbBg;
                brightThumb.style.backgroundImage = thumbBg;
                sliderElement.FillColor = data.sliderColor;

                ApplyLockState();
                ResetState();
            }

            public void SetLockedEmpty()
            {
                StopAndKill();
                data = null;
                IsBound = false;
                IsUnlocked = false;
                dimThumb.style.backgroundImage = StyleKeyword.None;
                brightThumb.style.backgroundImage = StyleKeyword.None;
                ApplyLockState();
            }

            private void ApplyLockState()
            {
                EnableInClassList(FrameLockedClass, !IsUnlocked);
                lockOverlay.style.display = IsUnlocked ? DisplayStyle.None : DisplayStyle.Flex;

                thumbRoot.style.display = IsBound ? DisplayStyle.Flex : DisplayStyle.None;
                sliderElement.style.display = IsUnlocked ? DisplayStyle.Flex : DisplayStyle.None;
                countLabel.style.display = IsUnlocked ? DisplayStyle.Flex : DisplayStyle.None;
            }

            public void GrantAvailableUnitsImmediate(int count)
            {
                if (!IsBound || !IsUnlocked || count <= 0) return;
                availableUnits += count;
                SetThumbFillImmediate(1f);
                UpdateCountUI();
                PlayBounce(1);
            }

            public void EnqueueCharge(int matchCount, float duration)
            {
                if (runner == null || data == null || !IsUnlocked || matchCount <= 0) return;
                pendingMatches.Enqueue(matchCount);
                if (chargeCoroutine == null)
                    chargeCoroutine = runner.StartCoroutine(ProcessChargeQueue(duration));
            }

            private IEnumerator ProcessChargeQueue(float duration)
            {
                while (pendingMatches.Count > 0)
                {
                    int matchCount = pendingMatches.Dequeue();
                    PlayBounce(matchCount);

                    int maxCharge = Mathf.Max(1, data.maxCharge);
                    charge += matchCount;

                    if (charge >= maxCharge)
                    {
                        float fullDuration = duration * 0.8f;
                        AnimateSliderFill(1f, fullDuration, Ease.OutQuad);
                        if (availableUnits == 0) AnimateThumbFill(1f, fullDuration, Ease.OutQuad);

                        for (float t = 0f; t < fullDuration; t += Time.unscaledDeltaTime) yield return null;

                        while (charge >= maxCharge)
                        {
                            availableUnits++;
                            charge -= maxCharge;
                        }

                        UpdateCountUI();
                        SetThumbFillImmediate(1f);

                        for (float t = 0f; t < 0.08f; t += Time.unscaledDeltaTime) yield return null;
                        SetSliderFillImmediate(0f);

                        if (charge > 0)
                        {
                            float remainRatio = GetChargeRatio();
                            float remainDuration = duration * 0.7f;
                            AnimateSliderFill(remainRatio, remainDuration, Ease.OutQuad);
                            for (float t = 0f; t < remainDuration; t += Time.unscaledDeltaTime) yield return null;
                        }
                    }
                    else
                    {
                        float fillRatio = GetChargeRatio();
                        AnimateSliderFill(fillRatio, duration, Ease.OutQuad);
                        if (availableUnits == 0) AnimateThumbFill(fillRatio, duration, Ease.OutQuad);
                        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime) yield return null;
                    }
                }

                chargeCoroutine = null;
            }

            public void ResetState()
            {
                StopAndKill();
                charge = 0;
                availableUnits = 0;
                isDragging = false;
                activePointerId = -1;
                thumbRoot.style.scale = new Scale(Vector3.one);
                sliderElement.style.scale = new Scale(Vector3.one);
                style.opacity = 1f;
                SetSliderFillImmediate(0f);
                SetThumbFillImmediate(0f);
                UpdateCountUI();
            }

            public void StopAndKill()
            {
                pendingMatches.Clear();
                if (chargeCoroutine != null && runner != null)
                {
                    runner.StopCoroutine(chargeCoroutine);
                    chargeCoroutine = null;
                }
                sliderTween?.Kill();
                thumbTween?.Kill();
                bounceSeq?.Kill();
                sliderBounceSeq?.Kill();
            }

            private float GetChargeRatio()
            {
                if (data == null) return 0f;
                return (float)charge / Mathf.Max(1, data.maxCharge);
            }

            private void OnPointerDown(PointerDownEvent evt)
            {
                if (!IsBound || !IsUnlocked || availableUnits <= 0 || isDragging || data.unitPrefab == null) return;

                isDragging = true;
                activePointerId = evt.pointerId;
                this.CapturePointer(evt.pointerId);
                style.opacity = 0.65f;
                evt.StopPropagation();

                onBeginDrag?.Invoke(data);
            }

            private void OnPointerMove(PointerMoveEvent evt)
            {
                if (!isDragging || evt.pointerId != activePointerId) return;
                onDrag?.Invoke(PanelToScreen(evt.position));
            }

            private void OnPointerUp(PointerUpEvent evt)
            {
                if (!isDragging || evt.pointerId != activePointerId) return;
                FinishDrag(evt.pointerId, true);
            }

            private void OnPointerCancel(PointerCancelEvent evt)
            {
                if (!isDragging || evt.pointerId != activePointerId) return;
                FinishDrag(evt.pointerId, false);
            }

            private void FinishDrag(int pointerId, bool confirmSpawn)
            {
                isDragging = false;
                activePointerId = -1;
                this.ReleasePointer(pointerId);
                style.opacity = 1f;

                bool spawned = onEndDrag != null && onEndDrag.Invoke(confirmSpawn);
                if (spawned && availableUnits > 0)
                {
                    availableUnits--;
                    UpdateCountUI();

                    if (availableUnits == 0)
                        AnimateThumbFill(GetChargeRatio(), 0.35f, Ease.OutQuad);
                }
            }

            private Vector2 PanelToScreen(Vector3 panelPos)
            {
                if (panel?.visualTree == null) return Vector2.zero;
                Vector2 panelSize = panel.visualTree.layout.size;
                if (panelSize.x <= 0f || panelSize.y <= 0f) return Vector2.zero;

                float screenX = (panelPos.x / panelSize.x) * Screen.width;
                float screenY = (1f - (panelPos.y / panelSize.y)) * Screen.height;
                return new Vector2(screenX, screenY);
            }

            private void UpdateCountUI()
            {
                countLabel.text = availableUnits.ToString();
            }

            private void PlayBounce(int count)
            {
                bounceSeq?.Kill();
                thumbRoot.style.scale = new Scale(Vector3.one);
                bounceSeq = DOTween.Sequence().SetUpdate(true);

                for (int i = 0; i < count; i++)
                {
                    bounceSeq.Append(DOVirtual.Float(1f, 1.25f, 0.08f, v => thumbRoot.style.scale = new Scale(new Vector3(v, v, 1f))).SetEase(Ease.OutQuad));
                    bounceSeq.Append(DOVirtual.Float(1.25f, 1f, 0.1f, v => thumbRoot.style.scale = new Scale(new Vector3(v, v, 1f))).SetEase(Ease.OutBack));
                }

                sliderBounceSeq?.Kill();
                sliderElement.style.scale = new Scale(Vector3.one);
                sliderBounceSeq = DOTween.Sequence().SetUpdate(true);
                sliderBounceSeq.Append(DOVirtual.Float(1f, 1.1f, 0.08f, v => sliderElement.style.scale = new Scale(new Vector3(v, v, 1f))).SetEase(Ease.OutQuad));
                sliderBounceSeq.Append(DOVirtual.Float(1.1f, 1f, 0.14f, v => sliderElement.style.scale = new Scale(new Vector3(v, v, 1f))).SetEase(Ease.OutBack));
            }

            private void SetSliderFillImmediate(float value)
            {
                sliderTween?.Kill();
                currentSliderFill = Mathf.Clamp01(value);
                sliderElement.FillAmount = currentSliderFill;
            }

            private void SetThumbFillImmediate(float value)
            {
                thumbTween?.Kill();
                currentThumbFill = Mathf.Clamp01(value);
                thumbMask.style.height = Length.Percent(currentThumbFill * 100f);
            }

            private void AnimateSliderFill(float target, float duration, Ease ease)
            {
                sliderTween?.Kill();
                float clamped = Mathf.Clamp01(target);
                sliderTween = DOTween.To(
                    () => currentSliderFill,
                    v =>
                    {
                        currentSliderFill = Mathf.Clamp01(v);
                        sliderElement.FillAmount = currentSliderFill;
                    },
                    clamped,
                    duration
                ).SetEase(ease).SetUpdate(true);
            }

            private void AnimateThumbFill(float target, float duration, Ease ease)
            {
                thumbTween?.Kill();
                float clamped = Mathf.Clamp01(target);
                thumbTween = DOTween.To(
                    () => currentThumbFill,
                    v =>
                    {
                        currentThumbFill = Mathf.Clamp01(v);
                        thumbMask.style.height = Length.Percent(currentThumbFill * 100f);
                    },
                    clamped,
                    duration
                ).SetEase(ease).SetUpdate(true);
            }

            private static VisualElement CreateOverlayElement()
            {
                var el = new VisualElement { pickingMode = PickingMode.Ignore };
                el.style.position = Position.Absolute;
                el.style.left = 0;
                el.style.top = 0;
                el.style.right = 0;
                el.style.bottom = 0;
                return el;
            }

            private static void ApplyContainSprite(VisualElement el)
            {
                el.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                el.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
                el.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
                el.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
            }
        }
        #endregion

        #region Filled Sprite Mesh
        private class FilledSpriteElement : VisualElement
        {
            private Sprite sprite;
            private SliderFillMode fillMode = SliderFillMode.RadialU;
            private float fillAmount;
            private Color fillColor = Color.yellow;
            private Color trackColor = new Color(0f, 0f, 0f, 0.35f);

            public float FillAmount
            {
                get => fillAmount;
                set
                {
                    float clamped = Mathf.Clamp01(value);
                    if (Mathf.Approximately(fillAmount, clamped)) return;
                    fillAmount = clamped;
                    MarkDirtyRepaint();
                }
            }

            public Color FillColor
            {
                get => fillColor;
                set
                {
                    fillColor = value;
                    MarkDirtyRepaint();
                }
            }

            public FilledSpriteElement()
            {
                generateVisualContent += OnGenerateVisualContent;
            }

            public void SetConfig(Sprite targetSprite, SliderFillMode mode)
            {
                sprite = targetSprite;
                fillMode = mode;
                MarkDirtyRepaint();
            }

            private void OnGenerateVisualContent(MeshGenerationContext mgc)
            {
                if (sprite == null || sprite.texture == null) return;

                Rect r = contentRect;
                if (r.width < 0.01f || r.height < 0.01f) return;

                Texture2D tex = sprite.texture;
                Rect texRect = sprite.textureRect;
                if (texRect.width <= 0f || texRect.height <= 0f) return;

                float spriteAspect = texRect.width / texRect.height;
                float rectAspect = r.width / r.height;
                float drawW;
                float drawH;

                if (rectAspect > spriteAspect)
                {
                    drawH = r.height;
                    drawW = drawH * spriteAspect;
                }
                else
                {
                    drawW = r.width;
                    drawH = drawW / spriteAspect;
                }

                float drawX = r.x + (r.width - drawW) * 0.5f;
                float drawY = r.y + (r.height - drawH) * 0.5f;

                float uvMinX = texRect.xMin / tex.width;
                float uvMaxX = texRect.xMax / tex.width;
                float uvMinY = texRect.yMin / tex.height;
                float uvMaxY = texRect.yMax / tex.height;

                float f = Mathf.Clamp01(fillAmount);
                bool hasFill = f > 0.001f;

                int fillVertCount = 0;
                int fillIdxCount = 0;
                int radialCase = 0;

                if (hasFill)
                {
                    if (fillMode == SliderFillMode.RadialU && f < 0.9999f)
                    {
                        float theta = Mathf.PI * f;
                        float a1 = Mathf.Atan2(drawH, drawW * 0.5f);
                        float a2 = Mathf.PI - a1;

                        if (theta <= a1) { radialCase = 1; fillVertCount = 3; fillIdxCount = 3; }
                        else if (theta <= a2) { radialCase = 2; fillVertCount = 4; fillIdxCount = 6; }
                        else { radialCase = 3; fillVertCount = 5; fillIdxCount = 9; }
                    }
                    else
                    {
                        fillVertCount = 4;
                        fillIdxCount = 6;
                    }
                }

                MeshWriteData mwd = mgc.Allocate(4 + fillVertCount, 6 + fillIdxCount, tex);
                if (mwd.vertexCount == 0) return;

                Rect uvReg = mwd.uvRegion;

                Vertex MakeVert(float px, float py, Color32 tint)
                {
                    float tx = (px - drawX) / drawW;
                    float ty = (py - drawY) / drawH;
                    float u = Mathf.Lerp(uvMinX, uvMaxX, tx);
                    float v = Mathf.Lerp(uvMaxY, uvMinY, ty);
                    return new Vertex
                    {
                        position = new Vector3(px, py, Vertex.nearZ),
                        tint = tint,
                        uv = new Vector2(uvReg.x + u * uvReg.width, uvReg.y + v * uvReg.height)
                    };
                }

                float left = drawX;
                float right = drawX + drawW;
                float top = drawY;
                float bottom = drawY + drawH;

                Color32 trackTint = trackColor;
                mwd.SetNextVertex(MakeVert(left, bottom, trackTint));
                mwd.SetNextVertex(MakeVert(left, top, trackTint));
                mwd.SetNextVertex(MakeVert(right, top, trackTint));
                mwd.SetNextVertex(MakeVert(right, bottom, trackTint));

                mwd.SetNextIndex(0); mwd.SetNextIndex(1); mwd.SetNextIndex(2);
                mwd.SetNextIndex(2); mwd.SetNextIndex(3); mwd.SetNextIndex(0);

                if (!hasFill) return;

                Color32 fillTint = fillColor;

                if (fillMode == SliderFillMode.RadialU && radialCase > 0)
                {
                    float cx = drawX + drawW * 0.5f;
                    float halfW = drawW * 0.5f;
                    float theta = Mathf.PI * f;
                    float dirX = -Mathf.Cos(theta);
                    float dirY = Mathf.Sin(theta);

                    float t = float.MaxValue;
                    if (dirX < -1e-5f) t = Mathf.Min(t, -halfW / dirX);
                    else if (dirX > 1e-5f) t = Mathf.Min(t, halfW / dirX);
                    if (dirY > 1e-5f) t = Mathf.Min(t, drawH / dirY);

                    float ex = cx + dirX * t;
                    float ey = top + dirY * t;

                    mwd.SetNextVertex(MakeVert(cx, top, fillTint));
                    mwd.SetNextVertex(MakeVert(left, top, fillTint));

                    if (radialCase == 1)
                    {
                        mwd.SetNextVertex(MakeVert(ex, ey, fillTint));
                        mwd.SetNextIndex(4); mwd.SetNextIndex(6); mwd.SetNextIndex(5);
                    }
                    else if (radialCase == 2)
                    {
                        mwd.SetNextVertex(MakeVert(left, bottom, fillTint));
                        mwd.SetNextVertex(MakeVert(ex, ey, fillTint));
                        mwd.SetNextIndex(4); mwd.SetNextIndex(6); mwd.SetNextIndex(5);
                        mwd.SetNextIndex(4); mwd.SetNextIndex(7); mwd.SetNextIndex(6);
                    }
                    else
                    {
                        mwd.SetNextVertex(MakeVert(left, bottom, fillTint));
                        mwd.SetNextVertex(MakeVert(right, bottom, fillTint));
                        mwd.SetNextVertex(MakeVert(ex, ey, fillTint));
                        mwd.SetNextIndex(4); mwd.SetNextIndex(6); mwd.SetNextIndex(5);
                        mwd.SetNextIndex(4); mwd.SetNextIndex(7); mwd.SetNextIndex(6);
                        mwd.SetNextIndex(4); mwd.SetNextIndex(8); mwd.SetNextIndex(7);
                    }
                }
                else
                {
                    float fLeft = left;
                    float fRight = right;
                    float fTop = top;
                    float fBottom = bottom;

                    if (fillMode == SliderFillMode.Horizontal)
                        fRight = left + drawW * f;
                    else if (fillMode == SliderFillMode.Vertical)
                        fTop = bottom - drawH * f;

                    mwd.SetNextVertex(MakeVert(fLeft, fBottom, fillTint));
                    mwd.SetNextVertex(MakeVert(fLeft, fTop, fillTint));
                    mwd.SetNextVertex(MakeVert(fRight, fTop, fillTint));
                    mwd.SetNextVertex(MakeVert(fRight, fBottom, fillTint));

                    mwd.SetNextIndex(4); mwd.SetNextIndex(5); mwd.SetNextIndex(6);
                    mwd.SetNextIndex(6); mwd.SetNextIndex(7); mwd.SetNextIndex(4);
                }
            }
        }
        #endregion
    }
}
