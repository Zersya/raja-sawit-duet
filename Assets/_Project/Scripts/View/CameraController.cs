using System;
using UnityEngine;
using UnityEngine.EventSystems;
#if RS_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using ETouch = UnityEngine.InputSystem.EnhancedTouch;
#endif

namespace RajaSawit.View
{
    /// <summary>
    /// Portrait camera: one finger drags the map, two fingers pinch to zoom, a short touch is a tap.
    /// In the Editor the mouse does the same (drag, scroll wheel, click).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraController : MonoBehaviour
    {
        [SerializeField] private IsoMapView map;
        [SerializeField] private float minZoom = 3f;
        [SerializeField] private float maxZoom = 18f;
        [SerializeField] private float startZoom = 7f;
        [Tooltip("Pixels a finger may move and still count as a tap.")]
        [SerializeField] private float tapSlopPixels = 18f;
        [SerializeField] private float mouseWheelZoomSpeed = 1.2f;

        /// <summary>Screen position of a tap that did not land on UI.</summary>
        public event Action<Vector2> Tapped;

        private Camera cam;
        private Vector2 pressStart;
        private bool pressing;
        private bool dragged;
        private bool pressStartedOverUi;
        private float lastPinchDistance = -1f;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = startZoom;
        }

        private void Start()
        {
            if (map != null)
            {
                Bounds b = map.MapBounds();
                transform.position = new Vector3(b.center.x, b.center.y, transform.position.z);
            }
        }

#if RS_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
        private void OnEnable() => ETouch.EnhancedTouchSupport.Enable();
        private void OnDisable() => ETouch.EnhancedTouchSupport.Disable();

        private void Update()
        {
            var touches = ETouch.Touch.activeTouches;
            if (touches.Count >= 2)
            {
                Vector2 a = touches[0].screenPosition, b = touches[1].screenPosition;
                Pinch(Vector2.Distance(a, b), (a + b) * 0.5f);
                dragged = true;
                return;
            }
            lastPinchDistance = -1f;

            if (touches.Count == 1)
            {
                var t = touches[0];
                if (t.phase == UnityEngine.InputSystem.TouchPhase.Began) Press(t.screenPosition, t.touchId);
                else if (t.phase == UnityEngine.InputSystem.TouchPhase.Moved) Drag(t.screenPosition, t.delta);
                else if (t.phase == UnityEngine.InputSystem.TouchPhase.Ended) Release(t.screenPosition);
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 pos = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame) Press(pos, -1);
            else if (mouse.leftButton.isPressed) Drag(pos, mouse.delta.ReadValue());
            else if (mouse.leftButton.wasReleasedThisFrame) Release(pos);

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && !PointerOverUi(-1)) ZoomBy(-Mathf.Sign(scroll) * mouseWheelZoomSpeed, pos);
        }
#else
        private void Update()
        {
            if (Input.touchCount >= 2)
            {
                Vector2 a = Input.GetTouch(0).position, b = Input.GetTouch(1).position;
                Pinch(Vector2.Distance(a, b), (a + b) * 0.5f);
                dragged = true;
                return;
            }
            lastPinchDistance = -1f;

            if (Input.touchCount == 1)
            {
                Touch t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began) Press(t.position, t.fingerId);
                else if (t.phase == TouchPhase.Moved) Drag(t.position, t.deltaPosition);
                else if (t.phase == TouchPhase.Ended) Release(t.position);
                return;
            }

            Vector2 pos = Input.mousePosition;
            if (Input.GetMouseButtonDown(0)) { Press(pos, -1); lastMouse = pos; }
            else if (Input.GetMouseButton(0)) { Drag(pos, pos - lastMouse); lastMouse = pos; }
            else if (Input.GetMouseButtonUp(0)) Release(pos);

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f && !PointerOverUi(-1)) ZoomBy(-Mathf.Sign(scroll) * mouseWheelZoomSpeed, pos);
        }

        private Vector2 lastMouse;
#endif

        private void Press(Vector2 screen, int pointerId)
        {
            pressing = true;
            dragged = false;
            pressStart = screen;
            pressStartedOverUi = PointerOverUi(pointerId);
        }

        private void Drag(Vector2 screen, Vector2 delta)
        {
            if (!pressing || pressStartedOverUi) return;
            if (!dragged && (screen - pressStart).sqrMagnitude < tapSlopPixels * tapSlopPixels) return;
            dragged = true;
            float unitsPerPixel = cam.orthographicSize * 2f / Screen.height;
            transform.position -= new Vector3(delta.x * unitsPerPixel, delta.y * unitsPerPixel, 0f);
            ClampToMap();
        }

        private void Release(Vector2 screen)
        {
            if (pressing && !dragged && !pressStartedOverUi) Tapped?.Invoke(screen);
            pressing = false;
        }

        private void Pinch(float distance, Vector2 center)
        {
            if (lastPinchDistance > 0f && distance > 1f)
            {
                float ratio = lastPinchDistance / distance;
                SetZoom(cam.orthographicSize * ratio, center);
            }
            lastPinchDistance = distance;
            pressing = false;
        }

        private void ZoomBy(float amount, Vector2 screenFocus) => SetZoom(cam.orthographicSize + amount, screenFocus);

        /// <summary>Zooms while keeping the world point under the fingers in place.</summary>
        private void SetZoom(float size, Vector2 screenFocus)
        {
            Vector3 before = cam.ScreenToWorldPoint(screenFocus);
            cam.orthographicSize = Mathf.Clamp(size, minZoom, maxZoom);
            Vector3 after = cam.ScreenToWorldPoint(screenFocus);
            transform.position += new Vector3(before.x - after.x, before.y - after.y, 0f);
            ClampToMap();
        }

        private void ClampToMap()
        {
            if (map == null) return;
            Bounds b = map.MapBounds();
            Vector3 p = transform.position;
            p.x = Mathf.Clamp(p.x, b.min.x, b.max.x);
            p.y = Mathf.Clamp(p.y, b.min.y, b.max.y);
            transform.position = p;
        }

        private static bool PointerOverUi(int pointerId)
        {
            EventSystem es = EventSystem.current;
            if (es == null) return false;
            return pointerId >= 0 ? es.IsPointerOverGameObject(pointerId) : es.IsPointerOverGameObject();
        }

        public Vector3 ScreenToWorld(Vector2 screen) => cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -transform.position.z));
    }
}
