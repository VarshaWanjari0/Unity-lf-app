using System;
using UnityEngine;

public class ModelPreviewController : MonoBehaviour
{
    [Header("Target & Camera")]
    public Transform cameraTransform;
    public Transform previewAnchor;

    [Header("Orbit Settings")]
    public float rotateSpeed = 0.5f;
    public float zoomSpeed = 0.05f;
    public float minDistance = 0.5f;
    public float maxDistance = 50f;

    private float currentDistance = 5f;
    private Vector2 rotationAngles = new Vector2(20f, 45f);
    private Vector3 focusCenter = Vector3.zero;
    private GameObject currentModelObject;

    private void Start()
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    private void Update()
    {
        if (previewAnchor == null || cameraTransform == null) return;

        HandleTouchOrMouseInput();
        UpdateCameraTransform();
    }

    private void HandleTouchOrMouseInput()
    {
        #if UNITY_ANDROID || UNITY_IOS
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Moved)
            {
                rotationAngles.x -= touch.deltaPosition.y * rotateSpeed;
                rotationAngles.y += touch.deltaPosition.x * rotateSpeed;
            }
        }
        else if (Input.touchCount == 2)
        {
            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);

            Vector2 prevPos0 = t0.position - t0.deltaPosition;
            Vector2 prevPos1 = t1.position - t1.deltaPosition;

            float prevDistance = (prevPos0 - prevPos1).magnitude;
            float currentDist = (t0.position - t1.position).magnitude;

            float delta = prevDistance - currentDist;
            currentDistance = Mathf.Clamp(currentDistance + delta * zoomSpeed, minDistance, maxDistance);
        }
        #else
        if (Input.GetMouseButton(0))
        {
            rotationAngles.x -= Input.GetAxis("Mouse Y") * rotateSpeed * 10f;
            rotationAngles.y += Input.GetAxis("Mouse X") * rotateSpeed * 10f;
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            currentDistance = Mathf.Clamp(currentDistance - scroll * 5f, minDistance, maxDistance);
        }
        #endif

        rotationAngles.x = Mathf.Clamp(rotationAngles.x, -85f, 85f);
    }

    private void UpdateCameraTransform()
    {
        Quaternion rot = Quaternion.Euler(rotationAngles.x, rotationAngles.y, 0f);
        Vector3 dir = rot * new Vector3(0, 0, -currentDistance);
        cameraTransform.position = focusCenter + dir;
        cameraTransform.LookAt(focusCenter);
    }

    public void FocusOnObject(GameObject target)
    {
        currentModelObject = target;
        if (target == null) return;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        focusCenter = bounds.center;
        float radius = bounds.extents.magnitude;
        currentDistance = Mathf.Clamp(radius * 2.5f, minDistance, maxDistance);

        rotationAngles = new Vector2(20f, 45f);
    }
}
