using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

public class UnityPyExporter
{
    [Serializable]
    public class MaterialJsonData
    {
        public string name;
        public string textureName;
    }

    [Serializable]
    public class MaterialsContainer
    {
        public List<MaterialJsonData> materials = new List<MaterialJsonData>();
    }

    [Serializable]
    public class InfoJsonData
    {
        public string mesh_name;
        public int vertex_count;
        public int triangle_count;
        public int submesh_count;
        public List<string> material_names = new List<string>();
        public List<string> texture_names = new List<string>();
    }

    public static string ExportToZip(Mesh mesh, List<Material> materials, List<Texture2D> textures, string outputDirectory, string customMeshName = null)
    {
        if (mesh == null) throw new ArgumentNullException("mesh");

        string exportName = !string.IsNullOrEmpty(customMeshName) ? customMeshName : mesh.name;
        if (string.IsNullOrEmpty(exportName)) exportName = "ExportedMesh";

        if (!Directory.Exists(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        string zipFilePath = Path.Combine(outputDirectory, exportName + ".zip");
        if (File.Exists(zipFilePath))
            File.Delete(zipFilePath);

        using (FileStream zipStream = new FileStream(zipFilePath, FileMode.Create))
        {
            using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                // 1. Export mesh binary files
                // vertices.bin
                ZipArchiveEntry vertEntry = archive.CreateEntry("mesh/vertices.bin");
                using (BinaryWriter writer = new BinaryWriter(vertEntry.Open()))
                {
                    Vector3[] verts = mesh.vertices;
                    for (int i = 0; i < verts.Length; i++)
                    {
                        writer.Write(verts[i].x);
                        writer.Write(verts[i].y);
                        writer.Write(verts[i].z);
                    }
                }

                // triangles.bin
                ZipArchiveEntry triEntry = archive.CreateEntry("mesh/triangles.bin");
                int totalTriangles = 0;
                using (BinaryWriter writer = new BinaryWriter(triEntry.Open()))
                {
                    int[] triangles = mesh.triangles;
                    totalTriangles = triangles.Length / 3;
                    for (int i = 0; i < triangles.Length; i++)
                    {
                        writer.Write(triangles[i]);
                    }
                }

                // uv.bin
                ZipArchiveEntry uvEntry = archive.CreateEntry("mesh/uv.bin");
                using (BinaryWriter writer = new BinaryWriter(uvEntry.Open()))
                {
                    Vector2[] uvs = mesh.uv;
                    for (int i = 0; i < uvs.Length; i++)
                    {
                        writer.Write(uvs[i].x);
                        writer.Write(uvs[i].y);
                    }
                }

                // normals.bin
                ZipArchiveEntry normEntry = archive.CreateEntry("mesh/normals.bin");
                using (BinaryWriter writer = new BinaryWriter(normEntry.Open()))
                {
                    Vector3[] normals = mesh.normals;
                    for (int i = 0; i < normals.Length; i++)
                    {
                        writer.Write(normals[i].x);
                        writer.Write(normals[i].y);
                        writer.Write(normals[i].z);
                    }
                }

                // tangents.bin
                ZipArchiveEntry tanEntry = archive.CreateEntry("mesh/tangents.bin");
                using (BinaryWriter writer = new BinaryWriter(tanEntry.Open()))
                {
                    Vector4[] tangents = mesh.tangents;
                    for (int i = 0; i < tangents.Length; i++)
                    {
                        writer.Write(tangents[i].x);
                        writer.Write(tangents[i].y);
                        writer.Write(tangents[i].z);
                        writer.Write(tangents[i].w);
                    }
                }

                // colors.bin (if present)
                if (mesh.colors != null && mesh.colors.Length > 0)
                {
                    ZipArchiveEntry colEntry = archive.CreateEntry("mesh/colors.bin");
                    using (BinaryWriter writer = new BinaryWriter(colEntry.Open()))
                    {
                        Color[] colors = mesh.colors;
                        for (int i = 0; i < colors.Length; i++)
                        {
                            writer.Write(colors[i].r);
                            writer.Write(colors[i].g);
                            writer.Write(colors[i].b);
                            writer.Write(colors[i].a);
                        }
                    }
                }

                // 2. Export textures
                List<string> texNames = new List<string>();
                if (textures != null)
                {
                    for (int i = 0; i < textures.Count; i++)
                    {
                        Texture2D tex = textures[i];
                        if (tex != null)
                        {
                            string texName = !string.IsNullOrEmpty(tex.name) ? tex.name : "texture_" + i;
                            if (!texName.ToLower().EndsWith(".png")) texName += ".png";

                            ZipArchiveEntry texEntry = archive.CreateEntry("textures/" + texName);
                            using (Stream s = texEntry.Open())
                            {
                                byte[] pngData = tex.EncodeToPNG();
                                if (pngData != null)
                                {
                                    s.Write(pngData, 0, pngData.Length);
                                    texNames.Add(texName);
                                }
                            }
                        }
                    }
                }

                // 3. Export materials/materials.json
                MaterialsContainer matContainer = new MaterialsContainer();
                List<string> matNames = new List<string>();
                if (materials != null)
                {
                    for (int i = 0; i < materials.Count; i++)
                    {
                        Material m = materials[i];
                        string mName = (m != null && !string.IsNullOrEmpty(m.name)) ? m.name : "Material_" + i;
                        matNames.Add(mName);

                        string texName = (textures != null && i < textures.Count && textures[i] != null) ? textures[i].name : "";
                        matContainer.materials.Add(new MaterialJsonData
                        {
                            name = mName,
                            textureName = texName
                        });
                    }
                }

                ZipArchiveEntry matJsonEntry = archive.CreateEntry("materials/materials.json");
                using (StreamWriter sw = new StreamWriter(matJsonEntry.Open(), Encoding.UTF8))
                {
                    sw.Write(JsonUtility.ToJson(matContainer, true));
                }

                // 4. Export info.json
                InfoJsonData info = new InfoJsonData
                {
                    mesh_name = exportName,
                    vertex_count = mesh.vertexCount,
                    triangle_count = totalTriangles,
                    submesh_count = mesh.subMeshCount,
                    material_names = matNames,
                    texture_names = texNames
                };

                ZipArchiveEntry infoEntry = archive.CreateEntry("info.json");
                using (StreamWriter sw = new StreamWriter(infoEntry.Open(), Encoding.UTF8))
                {
                    sw.Write(JsonUtility.ToJson(info, true));
                }
            }
        }

        return zipFilePath;
    }
}
