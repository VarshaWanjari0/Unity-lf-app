using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class GlbLoader
{
    public struct GlbResult
    {
        public Mesh mesh;
        public List<Material> materials;
        public List<Texture2D> textures;
        public List<string> materialNames;
        public List<string> textureNames;
    }

    [Serializable]
    private class GltfRoot
    {
        public GltfAccessor[] accessors;
        public GltfBufferView[] bufferViews;
        public GltfBuffer[] buffers;
        public GltfMesh[] meshes;
        public GltfMaterial[] materials;
        public GltfImage[] images;
        public GltfTexture[] textures;
    }

    [Serializable]
    private class GltfAccessor
    {
        public int bufferView = -1;
        public int byteOffset = 0;
        public int componentType;
        public int count;
        public string type;
    }

    [Serializable]
    private class GltfBufferView
    {
        public int buffer;
        public int byteOffset = 0;
        public int byteLength;
        public int byteStride = 0;
    }

    [Serializable]
    private class GltfBuffer
    {
        public int byteLength;
        public string uri;
    }

    [Serializable]
    private class GltfMesh
    {
        public string name;
        public GltfPrimitive[] primitives;
    }

    [Serializable]
    private class GltfPrimitive
    {
        public GltfAttributes attributes;
        public int indices = -1;
        public int material = -1;
    }

    [Serializable]
    private class GltfAttributes
    {
        public int POSITION = -1;
        public int NORMAL = -1;
        public int TEXCOORD_0 = -1;
        public int TANGENT = -1;
        public int COLOR_0 = -1;
    }

    [Serializable]
    private class GltfMaterial
    {
        public string name;
    }

    [Serializable]
    private class GltfImage
    {
        public int bufferView = -1;
        public string mimeType;
        public string uri;
        public string name;
    }

    [Serializable]
    private class GltfTexture
    {
        public int source = -1;
    }

    public static GlbResult Load(string filePath)
    {
        byte[] glbBytes = File.ReadAllBytes(filePath);
        if (glbBytes.Length < 12)
            throw new Exception("Invalid GLB file: File too short.");

        uint magic = BitConverter.ToUInt32(glbBytes, 0);
        if (magic != 0x46546C67) // "glTF"
            throw new Exception("Invalid GLB magic header.");

        uint version = BitConverter.ToUInt32(glbBytes, 4);
        uint length = BitConverter.ToUInt32(glbBytes, 8);

        int offset = 12;
        string jsonText = null;
        byte[] binData = null;

        while (offset < glbBytes.Length)
        {
            if (offset + 8 > glbBytes.Length) break;
            uint chunkLength = BitConverter.ToUInt32(glbBytes, offset);
            uint chunkType = BitConverter.ToUInt32(glbBytes, offset + 4);
            offset += 8;

            if (chunkType == 0x4E4F534A) // "JSON"
            {
                jsonText = Encoding.UTF8.GetString(glbBytes, offset, (int)chunkLength);
            }
            else if (chunkType == 0x00414942) // "BIN"
            {
                binData = new byte[chunkLength];
                Array.Copy(glbBytes, offset, binData, 0, chunkLength);
            }
            offset += (int)chunkLength;
        }

        if (string.IsNullOrEmpty(jsonText))
            throw new Exception("Failed to read JSON chunk from GLB.");

        GltfRoot root = JsonUtility.FromJson<GltfRoot>(jsonText);
        if (root == null || root.meshes == null || root.meshes.Length == 0)
            throw new Exception("No mesh data found in GLB JSON.");

        Mesh mesh = new Mesh();
        mesh.name = Path.GetFileNameWithoutExtension(filePath);

        List<Vector3> allPositions = new List<Vector3>();
        List<Vector3> allNormals = new List<Vector3>();
        List<Vector2> allUVs = new List<Vector2>();
        List<Vector4> allTangents = new List<Vector4>();
        List<Color> allColors = new List<Color>();
        List<List<int>> submeshTriangles = new List<List<int>>();

        List<Material> materials = new List<Material>();
        List<Texture2D> textures = new List<Texture2D>();
        List<string> materialNames = new List<string>();
        List<string> textureNames = new List<string>();

        // Parse images/textures if present
        if (root.images != null && binData != null)
        {
            for (int i = 0; i < root.images.Length; i++)
            {
                var img = root.images[i];
                if (img.bufferView >= 0 && img.bufferView < root.bufferViews.Length)
                {
                    var bv = root.bufferViews[img.bufferView];
                    byte[] imgBytes = new byte[bv.byteLength];
                    Array.Copy(binData, bv.byteOffset, imgBytes, 0, bv.byteLength);

                    Texture2D tex = new Texture2D(2, 2);
                    if (tex.LoadImage(imgBytes))
                    {
                        tex.name = !string.IsNullOrEmpty(img.name) ? img.name : "Texture_" + i;
                        textures.Add(tex);
                        textureNames.Add(tex.name);
                    }
                }
            }
        }

        GltfMesh gltfMesh = root.meshes[0];
        for (int p = 0; p < gltfMesh.primitives.Length; p++)
        {
            GltfPrimitive prim = gltfMesh.primitives[p];
            int vertOffset = allPositions.Count;

            // Read Positions
            if (prim.attributes.POSITION >= 0)
            {
                Vector3[] posArr = ReadVector3Accessor(root, prim.attributes.POSITION, binData);
                for (int i = 0; i < posArr.Length; i++)
                {
                    // GLTF right-handed to Unity left-handed (-X)
                    posArr[i].x = -posArr[i].x;
                }
                allPositions.AddRange(posArr);
            }

            // Read Normals
            if (prim.attributes.NORMAL >= 0)
            {
                Vector3[] normArr = ReadVector3Accessor(root, prim.attributes.NORMAL, binData);
                for (int i = 0; i < normArr.Length; i++)
                {
                    normArr[i].x = -normArr[i].x;
                }
                allNormals.AddRange(normArr);
            }

            // Read UVs
            if (prim.attributes.TEXCOORD_0 >= 0)
            {
                Vector2[] uvArr = ReadVector2Accessor(root, prim.attributes.TEXCOORD_0, binData);
                allUVs.AddRange(uvArr);
            }

            // Read Tangents
            if (prim.attributes.TANGENT >= 0)
            {
                Vector4[] tanArr = ReadVector4Accessor(root, prim.attributes.TANGENT, binData);
                allTangents.AddRange(tanArr);
            }

            // Read Indices
            List<int> tris = new List<int>();
            if (prim.indices >= 0)
            {
                int[] rawIndices = ReadIndicesAccessor(root, prim.indices, binData);
                for (int i = 0; i < rawIndices.Length; i += 3)
                {
                    if (i + 2 < rawIndices.Length)
                    {
                        // Reverse winding order for Unity
                        tris.Add(vertOffset + rawIndices[i]);
                        tris.Add(vertOffset + rawIndices[i + 2]);
                        tris.Add(vertOffset + rawIndices[i + 1]);
                    }
                }
            }
            submeshTriangles.Add(tris);

            // Material
            string matName = "Material_" + p;
            if (prim.material >= 0 && root.materials != null && prim.material < root.materials.Length)
            {
                if (!string.IsNullOrEmpty(root.materials[prim.material].name))
                    matName = root.materials[prim.material].name;
            }
            materialNames.Add(matName);

            Material mat = new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse"));
            mat.name = matName;
            if (textures.Count > p)
            {
                mat.mainTexture = textures[p];
            }
            materials.Add(mat);
        }

        if (allPositions.Count > 65535)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.SetVertices(allPositions);
        if (allNormals.Count == allPositions.Count) mesh.SetNormals(allNormals);
        if (allUVs.Count == allPositions.Count) mesh.SetUVs(0, allUVs);
        if (allTangents.Count == allPositions.Count) mesh.SetTangents(allTangents);

        mesh.subMeshCount = submeshTriangles.Count;
        for (int i = 0; i < submeshTriangles.Count; i++)
        {
            mesh.SetTriangles(submeshTriangles[i], i);
        }

        if (allNormals.Count == 0)
            mesh.RecalculateNormals();

        mesh.RecalculateBounds();

        return new GlbResult
        {
            mesh = mesh,
            materials = materials,
            textures = textures,
            materialNames = materialNames,
            textureNames = textureNames
        };
    }

    private static Vector3[] ReadVector3Accessor(GltfRoot root, int accessorIdx, byte[] binData)
    {
        GltfAccessor acc = root.accessors[accessorIdx];
        GltfBufferView bv = root.bufferViews[acc.bufferView];
        int start = bv.byteOffset + acc.byteOffset;

        Vector3[] res = new Vector3[acc.count];
        for (int i = 0; i < acc.count; i++)
        {
            int idx = start + i * 12;
            if (idx + 12 <= binData.Length)
            {
                float x = BitConverter.ToSingle(binData, idx);
                float y = BitConverter.ToSingle(binData, idx + 4);
                float z = BitConverter.ToSingle(binData, idx + 8);
                res[i] = new Vector3(x, y, z);
            }
        }
        return res;
    }

    private static Vector2[] ReadVector2Accessor(GltfRoot root, int accessorIdx, byte[] binData)
    {
        GltfAccessor acc = root.accessors[accessorIdx];
        GltfBufferView bv = root.bufferViews[acc.bufferView];
        int start = bv.byteOffset + acc.byteOffset;

        Vector2[] res = new Vector2[acc.count];
        for (int i = 0; i < acc.count; i++)
        {
            int idx = start + i * 8;
            if (idx + 8 <= binData.Length)
            {
                float x = BitConverter.ToSingle(binData, idx);
                float y = BitConverter.ToSingle(binData, idx + 4);
                res[i] = new Vector2(x, y);
            }
        }
        return res;
    }

    private static Vector4[] ReadVector4Accessor(GltfRoot root, int accessorIdx, byte[] binData)
    {
        GltfAccessor acc = root.accessors[accessorIdx];
        GltfBufferView bv = root.bufferViews[acc.bufferView];
        int start = bv.byteOffset + acc.byteOffset;

        Vector4[] res = new Vector4[acc.count];
        for (int i = 0; i < acc.count; i++)
        {
            int idx = start + i * 16;
            if (idx + 16 <= binData.Length)
            {
                float x = BitConverter.ToSingle(binData, idx);
                float y = BitConverter.ToSingle(binData, idx + 4);
                float z = BitConverter.ToSingle(binData, idx + 8);
                float w = BitConverter.ToSingle(binData, idx + 12);
                res[i] = new Vector4(x, y, z, w);
            }
        }
        return res;
    }

    private static int[] ReadIndicesAccessor(GltfRoot root, int accessorIdx, byte[] binData)
    {
        GltfAccessor acc = root.accessors[accessorIdx];
        GltfBufferView bv = root.bufferViews[acc.bufferView];
        int start = bv.byteOffset + acc.byteOffset;

        int[] res = new int[acc.count];
        for (int i = 0; i < acc.count; i++)
        {
            if (acc.componentType == 5123) // UNSIGNED_SHORT
            {
                int idx = start + i * 2;
                if (idx + 2 <= binData.Length)
                    res[i] = BitConverter.ToUInt16(binData, idx);
            }
            else if (acc.componentType == 5125) // UNSIGNED_INT
            {
                int idx = start + i * 4;
                if (idx + 4 <= binData.Length)
                    res[i] = BitConverter.ToInt32(binData, idx);
            }
            else if (acc.componentType == 5121) // UNSIGNED_BYTE
            {
                int idx = start + i;
                if (idx < binData.Length)
                    res[i] = binData[idx];
            }
        }
        return res;
    }
}
