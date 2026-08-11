using System;
using System.Collections.Generic;
using UnityEngine;

public class MeshProcessor
{
    public struct ProcessingSettings
    {
        public Vector3 scale;
        public Vector3 rotationEuler;
        public bool generateNormals;
        public bool generateTangents;
        public bool optimizeMesh;
        public bool preserveUV;
        public bool keepOriginalVertexOrder;
    }

    public static Mesh ProcessMesh(Mesh sourceMesh, ProcessingSettings settings)
    {
        if (sourceMesh == null) return null;

        Mesh processed = new Mesh();
        processed.name = sourceMesh.name;

        // Vertices transformation
        Vector3[] sourceVerts = sourceMesh.vertices;
        Vector3[] transformedVerts = new Vector3[sourceVerts.Length];
        Quaternion rotation = Quaternion.Euler(settings.rotationEuler);

        for (int i = 0; i < sourceVerts.Length; i++)
        {
            Vector3 scaled = Vector3.Scale(sourceVerts[i], settings.scale);
            transformedVerts[i] = rotation * scaled;
        }

        if (transformedVerts.Length > 65535)
            processed.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        processed.vertices = transformedVerts;

        // Triangles & Submeshes
        processed.subMeshCount = sourceMesh.subMeshCount;
        for (int i = 0; i < sourceMesh.subMeshCount; i++)
        {
            processed.SetTriangles(sourceMesh.GetTriangles(i), i);
        }

        // UVs
        if (settings.preserveUV && sourceMesh.uv != null && sourceMesh.uv.Length > 0)
        {
            processed.uv = sourceMesh.uv;
        }

        // Vertex Colors
        if (sourceMesh.colors != null && sourceMesh.colors.Length > 0)
        {
            processed.colors = sourceMesh.colors;
        }

        // Normals
        if (!settings.generateNormals && sourceMesh.normals != null && sourceMesh.normals.Length == sourceVerts.Length)
        {
            Vector3[] normArr = new Vector3[sourceMesh.normals.Length];
            for (int i = 0; i < normArr.Length; i++)
            {
                normArr[i] = rotation * sourceMesh.normals[i];
            }
            processed.normals = normArr;
        }
        else
        {
            processed.RecalculateNormals();
        }

        // Tangents
        if (settings.generateTangents)
        {
            processed.RecalculateTangents();
        }
        else if (sourceMesh.tangents != null && sourceMesh.tangents.Length == sourceVerts.Length)
        {
            Vector4[] tanArr = new Vector4[sourceMesh.tangents.Length];
            for (int i = 0; i < tanArr.Length; i++)
            {
                Vector3 rotTan = rotation * (Vector3)sourceMesh.tangents[i];
                tanArr[i] = new Vector4(rotTan.x, rotTan.y, rotTan.z, sourceMesh.tangents[i].w);
            }
            processed.tangents = tanArr;
        }

        // Mesh optimization if enabled and keepOriginalVertexOrder is false
        if (settings.optimizeMesh && !settings.keepOriginalVertexOrder)
        {
            #if UNITY_EDITOR
            UnityEditor.MeshUtility.Optimize(processed);
            #endif
        }

        processed.RecalculateBounds();
        return processed;
    }
}
