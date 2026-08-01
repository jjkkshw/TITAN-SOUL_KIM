using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public sealed class EyeCubeVisual3D : MonoBehaviour
{
    [SerializeField] private float size = 1.3f;
    [SerializeField] private Vector3 topViewTilt = new(-45f, 0f, 0f);
    [SerializeField] private Mesh sourceMesh;

    [Header("Face Sprites")]
    [Tooltip("+X face")]
    [SerializeField] private Sprite rightFace;
    [Tooltip("-X face")]
    [SerializeField] private Sprite leftFace;
    [Tooltip("+Y face")]
    [SerializeField] private Sprite topFace;
    [Tooltip("-Y face")]
    [SerializeField] private Sprite bottomFace;
    [Tooltip("Face initially visible to the camera")]
    [SerializeField] private Sprite frontFace;
    [Tooltip("Face opposite the initial camera-facing side")]
    [SerializeField] private Sprite backFace;

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

    private Sprite[] FaceSprites => new[]
    {
        rightFace, leftFace, topFace, bottomFace, frontFace, backFace
    };

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
        Sprite[] faceSprites = FaceSprites;
        for (int i = 0; i < materials.Length; i++)
        {
            Color baseColor = faceSprites[i] == null ? FaceColors[i] : Color.white;
            SetMaterialColor(materials[i], baseColor * brightness);
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
        filter.sharedMesh = sourceMesh == null
            ? CreateSixSidedCube()
            : CreateFaceReadyMesh(sourceMesh);

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        materials = new Material[FaceColors.Length];
        Sprite[] faceSprites = FaceSprites;
        for (int i = 0; i < materials.Length; i++)
        {
            materials[i] = new Material(shader)
            {
                name = $"EyeCube Face {i + 1}"
            };
            Color baseColor = faceSprites[i] == null ? FaceColors[i] : Color.white;
            SetMaterialColor(materials[i], baseColor);
            SetMaterialSprite(materials[i], faceSprites[i]);
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

        Vector2[] uvs = new Vector2[24];
        for (int face = 0; face < 6; face++)
        {
            int start = face * 4;
            uvs[start] = new Vector2(0f, 0f);
            uvs[start + 1] = new Vector2(1f, 0f);
            uvs[start + 2] = new Vector2(1f, 1f);
            uvs[start + 3] = new Vector2(0f, 1f);
        }

        Mesh mesh = new()
        {
            name = "EyeCube Six-Sided Mesh",
            vertices = vertices,
            uv = uvs
        };
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

    private Mesh CreateFaceReadyMesh(Mesh source)
    {
        Vector3[] sourceVertices = source.vertices;
        int[] sourceTriangles = source.triangles;
        Bounds bounds = source.bounds;
        float sourceSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        float scale = sourceSize > 0f ? size / sourceSize : 1f;

        List<Vector3> vertices = new();
        List<Vector2> uvs = new();
        List<int>[] faceTriangles = new List<int>[6];
        for (int i = 0; i < faceTriangles.Length; i++)
            faceTriangles[i] = new List<int>();

        for (int triangle = 0; triangle < sourceTriangles.Length; triangle += 3)
        {
            Vector3 a = sourceVertices[sourceTriangles[triangle]];
            Vector3 b = sourceVertices[sourceTriangles[triangle + 1]];
            Vector3 c = sourceVertices[sourceTriangles[triangle + 2]];
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            int face = ResolveFaceIndex(normal);

            AddFaceVertex(a, face, bounds, scale, vertices, uvs, faceTriangles[face]);
            AddFaceVertex(b, face, bounds, scale, vertices, uvs, faceTriangles[face]);
            AddFaceVertex(c, face, bounds, scale, vertices, uvs, faceTriangles[face]);
        }

        Mesh mesh = new()
        {
            name = $"{source.name} - EyeCube Face Ready",
            vertices = vertices.ToArray(),
            uv = uvs.ToArray(),
            subMeshCount = 6
        };
        for (int face = 0; face < 6; face++)
            mesh.SetTriangles(faceTriangles[face], face);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static int ResolveFaceIndex(Vector3 normal)
    {
        Vector3 absolute = new(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
        if (absolute.x >= absolute.y && absolute.x >= absolute.z)
            return normal.x >= 0f ? 0 : 1;
        if (absolute.y >= absolute.z)
            return normal.y >= 0f ? 2 : 3;
        return normal.z <= 0f ? 4 : 5;
    }

    private static void AddFaceVertex(
        Vector3 sourceVertex,
        int face,
        Bounds bounds,
        float scale,
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles)
    {
        int index = vertices.Count;
        vertices.Add((sourceVertex - bounds.center) * scale);

        Vector3 normalized = sourceVertex - bounds.min;
        normalized = new Vector3(
            normalized.x / Mathf.Max(bounds.size.x, Mathf.Epsilon),
            normalized.y / Mathf.Max(bounds.size.y, Mathf.Epsilon),
            normalized.z / Mathf.Max(bounds.size.z, Mathf.Epsilon));
        Vector2 uv = face switch
        {
            0 or 1 => new Vector2(normalized.z, normalized.y),
            2 or 3 => new Vector2(normalized.x, normalized.z),
            _ => new Vector2(normalized.x, normalized.y)
        };
        uvs.Add(uv);
        triangles.Add(index);
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private static void SetMaterialSprite(Material material, Sprite sprite)
    {
        if (sprite == null) return;

        Texture texture = sprite.texture;
        Rect rect = sprite.rect;
        Vector2 scale = new(
            rect.width / texture.width,
            rect.height / texture.height);
        Vector2 offset = new(
            rect.x / texture.width,
            rect.y / texture.height);

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", scale);
            material.SetTextureOffset("_BaseMap", offset);
        }
        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
            material.SetTextureScale("_MainTex", scale);
            material.SetTextureOffset("_MainTex", offset);
        }
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
