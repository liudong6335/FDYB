/*
 * ============================================================
 *  CameraFollow2_5D  -  2.5D相机跟随
 * ============================================================
 *
 * 【功能】
 *   相机平滑跟随目标（玩家），支持 2.5D 视角偏移、
 *   死区（小范围不跟）、边界限制、震屏效果。
 *
 * 【挂载对象】
 *   主摄像机（Main Camera）
 *
 * 【可调节参数】
 *   target          - 跟随目标（拖入玩家对象）
 *   offset          - 相机相对目标的偏移量 (x, y, z)
 *   smoothTime     - 跟随平滑速度（越小越灵敏）
 *   useDeadZone    - 是否启用死区（小范围移动不跟）
 *   deadZoneSize   - 死区范围 (x, y)
 *   useBoundary    - 是否启用边界限制
 *   boundaryMin/Max- 相机可移动的矩形边界
 *   defaultShakeDecay - 震屏衰减速度
 *
 * 【外部调用】
 *   Shake(intensity, decay) - 触发震屏效果
 *   SetTarget(Transform)    - 动态切换跟随目标
 */
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
