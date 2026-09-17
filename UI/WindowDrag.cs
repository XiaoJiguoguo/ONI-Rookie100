using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rookie100.UI
{
    /// <summary>
    /// 窗口拖拽组件：挂在标题栏上，拖动目标窗口根节点，结束后保存位置。
    /// （模式移植自 StorageNetwork 的 WindowDrag，布局持久化接 LayoutStore。）
    /// </summary>
    public sealed class WindowDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private RectTransform target;
        private RectTransform parent;
        private Vector2 lastLocalPointerPosition;

        public WindowDrag Configure(RectTransform targetRect)
        {
            target = targetRect;
            parent = targetRect != null ? targetRect.parent as RectTransform : null;
            return this;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (parent != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, eventData.position, eventData.pressEventCamera, out lastLocalPointerPosition);
            }

            eventData.Use();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (target == null)
            {
                return;
            }

            if (parent != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, eventData.position, eventData.pressEventCamera, out var localPointerPosition))
            {
                target.anchoredPosition += localPointerPosition - lastLocalPointerPosition;
                lastLocalPointerPosition = localPointerPosition;
            }
            else
            {
                target.anchoredPosition += eventData.delta;
            }

            ClampToScreen(target);
            eventData.Use();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (target != null)
            {
                ClampToScreen(target);
                LayoutStore.Save(new LayoutStore.WindowLayout
                {
                    X = target.anchoredPosition.x,
                    Y = target.anchoredPosition.y,
                    Width = target.rect.width,
                    Height = target.rect.height
                });
            }

            eventData.Use();
        }

        /// <summary>拖动时至少保留 48px 在屏幕内，避免窗口拖出屏幕找不回。</summary>
        public static void ClampToScreen(RectTransform rectTransform, float visibleMargin = 48f)
        {
            if (rectTransform == null)
            {
                return;
            }

            var parentRect = rectTransform.parent as RectTransform;
            float parentWidth = parentRect != null && parentRect.rect.width > 0f ? parentRect.rect.width : Screen.width;
            float parentHeight = parentRect != null && parentRect.rect.height > 0f ? parentRect.rect.height : Screen.height;
            Vector2 size = rectTransform.rect.size;
            Vector2 pivot = rectTransform.pivot;
            Vector2 anchor = rectTransform.anchorMin;
            Vector2 parentPivot = parentRect != null ? parentRect.pivot : new Vector2(0.5f, 0.5f);
            var anchorOffset = new Vector2(
                (anchor.x - parentPivot.x) * parentWidth,
                (anchor.y - parentPivot.y) * parentHeight);
            Vector2 localPosition = anchorOffset + rectTransform.anchoredPosition;

            float minX = -parentWidth * 0.5f - size.x * pivot.x + visibleMargin;
            float maxX = parentWidth * 0.5f + size.x * (1f - pivot.x) - visibleMargin;
            float minY = -parentHeight * 0.5f - size.y * pivot.y + visibleMargin;
            float maxY = parentHeight * 0.5f + size.y * (1f - pivot.y) - visibleMargin;

            var clamped = new Vector2(
                Mathf.Clamp(localPosition.x, minX, maxX),
                Mathf.Clamp(localPosition.y, minY, maxY));
            rectTransform.anchoredPosition = clamped - anchorOffset;
        }

        /// <summary>启动时恢复上次窗口位置/尺寸。</summary>
        public static bool TryApplyLayout(RectTransform rectTransform, Vector2 defaultSize)
        {
            var layout = LayoutStore.Load();
            if (layout == null || rectTransform == null)
            {
                return false;
            }

            if (layout.Width > 0f && layout.Height > 0f)
            {
                rectTransform.sizeDelta = new Vector2(layout.Width, layout.Height);
            }
            else
            {
                rectTransform.sizeDelta = defaultSize;
            }

            rectTransform.anchoredPosition = new Vector2(layout.X, layout.Y);
            ClampToScreen(rectTransform);
            return true;
        }
    }
}
