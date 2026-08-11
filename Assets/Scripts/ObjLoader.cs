using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public class ObjLoader
{
    public struct ObjMeshResult
    {
        public Mesh mesh;
        public List<Material> materials;
        public List<Texture2D> textures;
        public List<string> materialNames;
        public List<string> textureNames;
    }

    public static ObjMeshResult Load(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("OBJ file not found: " + filePath);

        string dir = Path.GetDirectoryName(filePath);
        string[] lines = File.ReadAllLines(filePath);

        List<Vector3> positions = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<Vector3> normals = new List<Vector3>();

        Dictionary<string, List<int>> matGroupTriangles = new Dictionary<string, List<int>>();
        List<string> materialOrder = new List<string>();
        string currentMat = "Default";
        matGroupTriangles[currentMat] = new List<int>();
        materialOrder.Add(currentMat);

        Dictionary<string, string> mtlTextures = new Dictionary<string, string>();

        // Store unique face vertices: posIdx, uvIdx, normIdx
        Dictionary<string, int> vertexMap = new Dictionary<string, int>();
        List<Vector3> finalPositions = new List<Vector3>();
        List<Vector2> finalUVs = new List<Vector2>();
        List<Vector3> finalNormals = new List<Vector3>();

        foreach (string line in lines)
        {
            string l = line.Trim();
            if (l.StartsWith("#") || string.IsNullOrEmpty(l)) continue;

            string[] parts = l.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            switch (parts[0])
            {
                case "mtllib":
                    if (parts.Length > 1)
                    {
                        string mtlPath = Path.Combine(dir, parts[1]);
                        ParseMtl(mtlPath, mtlTextures);
                    }
                    break;
                case "usemtl":
                    if (parts.Length > 1)
                    {
                        currentMat = parts[1];
                        if (!matGroupTriangles.ContainsKey(currentMat))
                        {
                            matGroupTriangles[currentMat] = new List<int>();
                            materialOrder.Add(currentMat);
                        }
                    }
                    break;
                case "v":
                    if (parts.Length >= 4)
                    {
                        float x = ParseFloat(parts[1]);
                        float y = ParseFloat(parts[2]);
                        float z = ParseFloat(parts[3]);
                        positions.Add(new Vector3(-x, y, z)); // OBJ to Unity coordinate conversion
                    }
                    break;
                case "vt":
                    if (parts.Length >= 3)
                    {
                        float u = ParseFloat(parts[1]);
                        float v = ParseFloat(parts[2]);
                        uvs.Add(new Vector2(u, v));
                    }
                    break;
                case "vn":
                    if (parts.Length >= 4)
                    {
                        float x = ParseFloat(parts[1]);
                        float y = ParseFloat(parts[2]);
                        float z = ParseFloat(parts[3]);
                        normals.Add(new Vector3(-x, y, z));
                    }
                    break;
                case "f":
                    List<int> faceIndices = new List<int>();
                    for (int i = 1; i < parts.Length; i++)
                    {
                        string key = parts[i];
                        if (!vertexMap.TryGetValue(key, out int vertIdx))
                        {
                            string[] sub = key.Split('/');
                            int pIdx = int.Parse(sub[0]) - 1;
                            int uIdx = (sub.Length > 1 && !string.IsNullOrEmpty(sub[1])) ? int.Parse(sub[1]) - 1 : -1;
                            int nIdx = (sub.Length > 2 && !string.IsNullOrEmpty(sub[2])) ? int.Parse(sub[2]) - 1 : -1;

                            Vector3 pos = (pIdx >= 0 && pIdx < positions.Count) ? positions[pIdx] : Vector3.zero;
                            Vector2 uv = (uIdx >= 0 && uIdx < uvs.Count) ? uvs[uIdx] : Vector2.zero;
                            Vector3 norm = (nIdx >= 0 && nIdx < normals.Count) ? normals[nIdx] : Vector3.zero;

                            vertIdx = finalPositions.Count;
                            finalPositions.Add(pos);
                            finalUVs.Add(uv);
                            finalNormals.Add(norm);
                            vertexMap[key] = vertIdx;
                        }
                        faceIndices.Add(vertIdx);
                    }

                    // Triangulate n-gon fan
                    for (int i = 1; i < faceIndices.Count - 1; i++)
                    {
                        matGroupTriangles[currentMat].Add(faceIndices[0]);
                        matGroupTriangles[currentMat].Add(faceIndices[i + 2]); // Winding order flip
                        matGroupTriangles[currentMat].Add(faceIndices[i + 1]);
                    }
                    break;
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = Path.GetFileNameWithoutExtension(filePath);

        if (finalPositions.Count > 65535)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.SetVertices(finalPositions);
        if (finalUVs.Count == finalPositions.Count) mesh.SetUVs(0, finalUVs);
        if (finalNormals.Count == finalPositions.Count) mesh.SetNormals(finalNormals);

        mesh.subMeshCount = materialOrder.Count;
        List<Material> materials = new List<Material>();
        List<Texture2D> textures = new List<Texture2D>();
        List<string> texNames = new List<string>();

        for (int i = 0; i < materialOrder.Count; i++)
        {
            string matName = materialOrder[i];
            mesh.SetTriangles(matGroupTriangles[matName], i);

            Material mat = new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse"));
            mat.name = matName;

            if (mtlTextures.TryGetValue(matName, out string texFileName))
            {
                string texPath = Path.Combine(dir, texFileName);
                if (File.Exists(texPath))
                {
                    byte[] bytes = File.ReadAllBytes(texPath);
                    Texture2D tex = new Texture2D(2, 2);
                    if (tex.LoadImage(bytes))
                    {
                        tex.name = Path.GetFileName(texPath);
                        mat.mainTexture = tex;
                        textures.Add(tex);
                        texNames.Add(tex.name);
                    }
                }
            }
            materials.Add(mat);
        }

        if (finalNormals.Count == 0)
            mesh.RecalculateNormals();

        mesh.RecalculateBounds();

        return new ObjMeshResult
        {
            mesh = mesh,
            materials = materials,
            textures = textures,
            materialNames = materialOrder,
            textureNames = texNames
        };
    }

    private static void ParseMtl(string mtlPath, Dictionary<string, string> mtlTextures)
    {
        if (!File.Exists(mtlPath)) return;

        string currentMat = "";
        foreach (string line in File.ReadAllLines(mtlPath))
        {
            string l = line.Trim();
            if (l.StartsWith("newmtl "))
            {
                currentMat = l.Substring(7).Trim();
            }
            else if (l.StartsWith("map_Kd ") && !string.IsNullOrEmpty(currentMat))
            {
                string tex = l.Substring(7).Trim();
                mtlTextures[currentMat] = tex;
            }
        }
    }

    private static float ParseFloat(string s)
    {
        float.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out float result);
        return result;
    }
}
