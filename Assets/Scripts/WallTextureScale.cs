using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class WallTextureScale : MonoBehaviour
{
    [Tooltip("World units per one texture repeat. Lower = denser.")]
    public float worldUnitsPerTile = 4f;

    private MaterialPropertyBlock mpb;

    void OnValidate() => Apply();
    void Update() { if (!Application.isPlaying) Apply(); }  // live preview while editing
    void Start() => Apply();

    void Apply()
    {
        var r = GetComponent<Renderer>();
        if (r == null || worldUnitsPerTile <= 0f) return;
        if (mpb == null) mpb = new MaterialPropertyBlock();

        Vector3 s = transform.lossyScale;
        float width  = Mathf.Max(s.x, s.z);   // the long horizontal face
        float height = s.y;                    // wall height

        r.GetPropertyBlock(mpb);
        mpb.SetVector("_BaseMap_ST",
            new Vector4(width / worldUnitsPerTile, height / worldUnitsPerTile, 0f, 0f));
        r.SetPropertyBlock(mpb);
    }
}