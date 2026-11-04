// CameraZoomController.cs
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Camera))]
public class CameraZoomController : MonoBehaviour
{
    public float minSize = 3f;
    public float maxSize = 8f;
    public float smoothTime = 0.15f;
    public Slider zoomSlider; // optional UI slider to control zoom

    Camera cam;
    float velocity = 0f;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (zoomSlider != null)
        {
            zoomSlider.minValue = minSize;
            zoomSlider.maxValue = maxSize;
            zoomSlider.onValueChanged.AddListener(v => SetTargetSize(v));
            zoomSlider.value = cam.orthographicSize;
        }
    }

    public void SetTargetSize(float target)
    {
        StopAllCoroutines();
        StartCoroutine(SmoothTo(target));
    }

    System.Collections.IEnumerator SmoothTo(float target)
    {
        while (Mathf.Abs(cam.orthographicSize - target) > 0.01f)
        {
            cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, target, ref velocity, smoothTime);
            yield return null;
        }
        cam.orthographicSize = target;
    }
}
