using UnityEngine;
using UnityEngine.EventSystems;

namespace Rookie100.UI
{
    // Separate from task-window persistence; coordinates follow the native canvas scale.
    public sealed class MonitorHudDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        private const string Key = "Rookie100.MonitorHUD.v1.";
        private RectTransform rect, parent;
        private Vector2 pointer;
        private bool positioned;
        private void Start()
        {
            rect = GetComponent<RectTransform>(); parent = transform.parent as RectTransform;
            if (PlayerPrefs.HasKey(Key + "X"))
            {
                rect.anchoredPosition = new Vector2(PlayerPrefs.GetFloat(Key + "X"), PlayerPrefs.GetFloat(Key + "Y"));
                positioned = true;
            }
        }
        private void LateUpdate()
        {
            if (rect == null || parent == null) return;
            if (!positioned)
            {
                var button = ManagementMenuInstaller.TaskButtonRect;
                if (button != null)
                {
                    var corners = new Vector3[4]; button.GetWorldCorners(corners);
                    float bottom = parent.InverseTransformPoint(corners[0]).y;
                    rect.anchoredPosition = new Vector2(0f, Mathf.Min(-110f, bottom - parent.rect.yMax - 30f));
                    positioned = true;
                }
            }
            // Keep the entire bar visible when resolution or game UI scale changes.
            float half = rect.rect.width * .5f;
            float xLimit = Mathf.Max(0f, parent.rect.width * .5f - half - 8f);
            rect.anchoredPosition = new Vector2(Mathf.Clamp(rect.anchoredPosition.x, -xLimit, xLimit),
                Mathf.Clamp(rect.anchoredPosition.y, -Mathf.Max(30f, parent.rect.height - 30f), -30f));
            // Very small canvases: scale the whole bar, including controls, together.
            float scale = Mathf.Min(1f, Mathf.Max(.1f, (parent.rect.width - 16f) / 480f));
            rect.localScale = new Vector3(scale, scale, 1f);
        }
        public void OnBeginDrag(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, e.position, e.pressEventCamera, out pointer);
            positioned = true; e.Use();
        }
        public void OnDrag(PointerEventData e)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, e.position, e.pressEventCamera, out var next))
            { rect.anchoredPosition += next - pointer; pointer = next; }
            e.Use();
        }
        public void OnEndDrag(PointerEventData e)
        {
            LateUpdate(); PlayerPrefs.SetFloat(Key + "X", rect.anchoredPosition.x);
            PlayerPrefs.SetFloat(Key + "Y", rect.anchoredPosition.y); PlayerPrefs.Save(); e.Use();
        }
        public void OnPointerClick(PointerEventData e)
        {
            if (e.clickCount != 2) return;
            GetComponent<DuplicantMonitorView>()?.LocateCurrent(); e.Use();
        }
        public void ResetPosition()
        {
            PlayerPrefs.DeleteKey(Key + "X"); PlayerPrefs.DeleteKey(Key + "Y"); PlayerPrefs.Save();
            positioned = false;
            if (rect != null) { rect.anchoredPosition = new Vector2(0f, -110f); LateUpdate(); }
        }
    }
}
