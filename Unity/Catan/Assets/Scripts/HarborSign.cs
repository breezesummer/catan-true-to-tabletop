using UnityEngine;

public sealed class HarborSign : MonoBehaviour
{
    internal const float DeckHeight = .00122f;
    public Renderer Face { get; private set; }
    internal MeshFilter PickVolume { get; private set; }
    internal MeshFilter Footprint { get; private set; }
    static readonly int Glint = Shader.PropertyToID("_PlaqueGlint");
    static readonly int PlaqueFace = Shader.PropertyToID("_IsTradePlaque");
    MaterialPropertyBlock properties;
    Vector4 glint;

    internal void Initialize(MeshFilter pickVolume = null, MeshFilter footprint = null)
    {
        Face = GetComponent<Renderer>();
        PickVolume = pickVolume;
        Footprint = footprint;
        glint.z = Mathf.Repeat(GetInstanceID() * .618034f, 5.2f);
        if (pickVolume != null)
        {
            properties = new MaterialPropertyBlock();
            Face.GetPropertyBlock(properties);
            properties.SetFloat(PlaqueFace, 1);
            Face.SetPropertyBlock(properties);
        }
    }

    internal void SetGlint(bool available, bool hovered, bool pressed=false)
    {
        float enabled=available ? 1 : 0, focus=hovered ? 1 : 0;
        if (glint.x==enabled && glint.y==focus && !pressed) return;
        glint.x=enabled; glint.y=focus;
        if (pressed) glint.w=Time.time+.19f;
        properties ??= new MaterialPropertyBlock();
        Face.GetPropertyBlock(properties);
        properties.SetVector(Glint,glint);
        Face.SetPropertyBlock(properties);
    }

    internal void FaceCamera(Camera camera)
    {
        if (PickVolume != null) return;
        var right = Vector3.ProjectOnPlane(camera.transform.right, Vector3.up);
        if (right.sqrMagnitude > .001f)
            transform.parent.rotation = Quaternion.LookRotation(Vector3.Cross(right, Vector3.up), Vector3.up);
    }
}
