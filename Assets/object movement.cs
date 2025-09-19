using UnityEngine;
using UnityEngine.Splines;

public class ObjectMovement : MonoBehaviour
{
    public SplineContainer splineContainer;
    public float speed = 2f;
    private float t = 0f;

    void Update()
    {
        if (splineContainer == null) return;

        t += (speed / splineContainer.CalculateLength()) * Time.deltaTime;
        t %= 1f; // Loop

        Vector3 position = splineContainer.EvaluatePosition(t);
        transform.position = position;
    }
}
