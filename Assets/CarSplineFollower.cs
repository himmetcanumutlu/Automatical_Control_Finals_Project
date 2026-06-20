using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

public class CarSplineFollower : MonoBehaviour
{
    [Header("Spline")]
    public SplineContainer roadSpline;

    [Header("Car Settings")]
    public float carMass = 1200f;
    public float targetSpeed = 8f;
    public float currentSpeed = 0f;
    public float maxSpeed = 20f;

    [Header("PID Values")]
    public float Kp = 800f;
    public float Ki = 20f;
    public float Kd = 150f;

    [Header("Live Output")]
    public float error;
    public float pValue;
    public float iValue;
    public float dValue;
    public float pidOutput;
    public float torque;

    [Header("Lap Info")]
    public int lapCount = 0;
    public float splineProgress = 0f;

    private float previousError;
    private float splineT;

    void Start()
    {
        if (gameObject.GetComponent<CarVisualizer>() == null)
        {
            gameObject.AddComponent<CarVisualizer>();
        }
        splineT = 0f;
        lapCount = 0;
        ResetPID();
    }

    void Update()
    {
        if (roadSpline == null)
            return;

        float dt = Time.deltaTime;

        if (dt <= 0f)
            return;

        error = targetSpeed - currentSpeed;

        pValue = Kp * error;

        iValue += Ki * error * dt;
        iValue = Mathf.Clamp(iValue, -3000f, 3000f);

        dValue = Kd * (error - previousError) / dt;

        pidOutput = pValue + iValue + dValue;
        pidOutput = Mathf.Clamp(pidOutput, -3000f, 3000f);

        torque = pidOutput;

        float acceleration = carMass > 0f ? torque / carMass : 0f;

        currentSpeed += acceleration * dt;
        currentSpeed = Mathf.Clamp(currentSpeed, 0f, maxSpeed);

        float splineLength = roadSpline.CalculateLength();

        if (splineLength > 0f)
        {
            splineT += (currentSpeed * dt) / splineLength;
        }

        if (splineT > 1f)
        {
            splineT -= 1f;
            lapCount++;
        }

        splineProgress = splineT;

        roadSpline.Evaluate(splineT, out float3 position, out float3 tangent, out float3 up);

        Vector3 pos = new Vector3(position.x, position.y + 0.6f, position.z);
        Vector3 forward = new Vector3(tangent.x, tangent.y, tangent.z).normalized;

        transform.position = pos;

        if (forward != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        previousError = error;
    }

    public void ResetPID()
    {
        error = 0f;
        pValue = 0f;
        iValue = 0f;
        dValue = 0f;
        pidOutput = 0f;
        torque = 0f;
        previousError = 0f;
    }

    public void ResetSimulation()
    {
        currentSpeed = 0f;
        splineT = 0f;
        splineProgress = 0f;
        lapCount = 0;
        ResetPID();
    }
}