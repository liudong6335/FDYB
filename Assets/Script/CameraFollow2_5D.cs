using UnityEngine;
using System.Collections;

public class CameraFollow2_5D : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -10f);
    [SerializeField] private float smoothTime = 0.125f;

    [Header("Dead Zone")]
    [SerializeField] private bool useDeadZone = true;
    [SerializeField] private Vector2 deadZoneSize = new Vector2(2f, 1f);

    [Header("Boundary")]
    [SerializeField] private bool useBoundary;
    [SerializeField] private Vector2 boundaryMin;
    [SerializeField] private Vector2 boundaryMax;

    [Header("Screen Shake")]
    [SerializeField] private float defaultShakeDecay = 4f;

    private Vector3 velocity;
    private float shakeIntensity;
    private float shakeDecay;

    private void Start()
    {
        if (target == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.position + offset;

        // Dead zone: skip tiny movements
        if (useDeadZone)
        {
            float dx = desired.x - transform.position.x;
            float dy = desired.y - transform.position.y;
            if (Mathf.Abs(dx) < deadZoneSize.x * 0.5f) desired.x = transform.position.x;
            if (Mathf.Abs(dy) < deadZoneSize.y * 0.5f) desired.y = transform.position.y;
        }

        Vector3 smoothed = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);

        // Boundary clamp
        if (useBoundary)
        {
            smoothed.x = Mathf.Clamp(smoothed.x, boundaryMin.x, boundaryMax.x);
            smoothed.y = Mathf.Clamp(smoothed.y, boundaryMin.y, boundaryMax.y);
        }

        // Screen shake
        if (shakeIntensity > 0.01f)
        {
            smoothed.x += Random.Range(-1f, 1f) * shakeIntensity;
            smoothed.y += Random.Range(-1f, 1f) * shakeIntensity;
            shakeIntensity = Mathf.Lerp(shakeIntensity, 0f, Time.deltaTime * shakeDecay);
        }

        transform.position = smoothed;
    }

    public void SetTarget(Transform t) { target = t; }

    public void Shake(float intensity, float decay = -1f)
    {
        shakeIntensity = Mathf.Max(shakeIntensity, intensity);
        shakeDecay = decay > 0f ? decay : defaultShakeDecay;
    }
}