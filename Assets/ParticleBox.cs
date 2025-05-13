using UnityEngine;

public class ParticleBox : MonoBehaviour
{
    public Vector2 position = Vector2.zero;
    public Vector2 max = new Vector2(5f, 5f);
    public Vector2 min = new Vector2(-5f, -1f);

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Vector2 bottomLeft = new Vector2(min.x, min.y);
        Vector2 topLeft = new Vector2(min.x, max.y);
        Vector2 topRight = new Vector2(max.x, max.y);
        Vector2 bottomRight = new Vector2(max.x, min.y);

        Gizmos.DrawLine(bottomLeft, topLeft);
        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(bottomRight, bottomLeft);
    }
}
