using UnityEngine;

public sealed class HarborSign : MonoBehaviour
{
    internal const float DeckHeight = .00122f;
    public Renderer Face { get; private set; }

    internal void Initialize()
    {
        Face = GetComponent<Renderer>();
    }

    internal void FaceCamera(Camera camera)
    {
        var right = Vector3.ProjectOnPlane(camera.transform.right, Vector3.up);
        if (right.sqrMagnitude > .001f)
            transform.parent.rotation = Quaternion.LookRotation(Vector3.Cross(right, Vector3.up), Vector3.up);
    }
}
