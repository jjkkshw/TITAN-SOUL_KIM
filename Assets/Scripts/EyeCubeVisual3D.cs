using UnityEngine;

[DisallowMultipleComponent]
public sealed class EyeCubeVisual3D : MonoBehaviour
{
    [SerializeField] private float size = 1.3f;
    [SerializeField] private Vector3 topViewTilt = new(-45f, 0f, 0f);

    private static readonly Color[] FaceColors =
    {
        new(0.95f, 0.18f, 0.18f), // right
        new(0.18f, 0.75f, 0.30f), // left
        new(0.20f, 0.45f, 1.00f), // top
        new(1.00f, 0.78f, 0.12f), // bottom
        new(0.75f, 0.25f, 0.95f), // front
        new(0.10f, 0.85f, 0.90f)  // back
    };

    private Transform cubeTransform;
    private Transform viewTiltTransform;
    private Material[] materials;

    private void Awake()
    {
        BuildCube();
    }

    public Quaternion CubeRotation
    {
        get => cubeTransform.localRotation;
        set => cubeTransform.localRotation = value;
    }

    public void SetDefeated(bool defeated)
    {
        if (materials == null)
        {
            return;
        }

        float brightness = defeated ? 0.35f : 1f;
        for (int i = 0; i < materials.Length; i++)
        {
            materials[i].color = FaceColors[i] * brightness;
        }
    }

    private void BuildCube()
    {
        GameObject viewTilt = new("Fixed Top View Tilt");
        viewTilt.transform.SetParent(transform, false);
        viewTiltTransform = viewTilt.transform;
        viewTiltTransform.localRotation = Quaternion.Euler(topViewTilt);

        GameObject cube = new("Colored Cube Visual");
        cube.transform.SetParent(viewTiltTransform, false);
        cubeTransform = cube.transform;

        MeshFilter filter = cube.AddComponent<MeshFilter>();
        MeshRenderer renderer = cube.AddComponent<MeshRenderer>();
        filter.sharedMesh = CreateSixSidedCube();

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        materials = new Material[FaceColors.Length];
        for (int i = 0; i < materials.Length; i++)
        {
            materials[i] = new Material(shader)
            {
                name = $"EyeCube Face {i + 1}",
                color = FaceColors[i]
            };
        }

        renderer.sharedMaterials = materials;
    }

    private Mesh CreateSixSidedCube()
    {
        float h = size * 0.5f;
        Vector3[] vertices =
        {
            new(h,-h,-h), new(h,-h,h), new(h,h,h), new(h,h,-h),
            new(-h,-h,h), new(-h,-h,-h), new(-h,h,-h), new(-h,h,h),
            new(-h,h,-h), new(h,h,-h), new(h,h,h), new(-h,h,h),
            new(-h,-h,h), new(h,-h,h), new(h,-h,-h), new(-h,-h,-h),
            new(-h,-h,-h), new(h,-h,-h), new(h,h,-h), new(-h,h,-h),
            new(h,-h,h), new(-h,-h,h), new(-h,h,h), new(h,h,h)
        };

        Mesh mesh = new() { name = "EyeCube Six-Sided Mesh", vertices = vertices };
        mesh.subMeshCount = 6;
        for (int face = 0; face < 6; face++)
        {
            int start = face * 4;
            mesh.SetTriangles(new[] { start, start + 2, start + 1, start, start + 3, start + 2 }, face);
        }

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void OnDestroy()
    {
        if (cubeTransform != null)
        {
            MeshFilter filter = cubeTransform.GetComponent<MeshFilter>();
            if (filter != null) Destroy(filter.sharedMesh);
        }

        if (materials == null) return;
        foreach (Material material in materials)
        {
            if (material != null) Destroy(material);
        }
    }
}
