using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XFramework;
using XFramework.Draw;

[System.Serializable]
public class GraphicsTest : ProcedureBase
{
    public bool activeDrawMesh = true;

    public override void Init()
    {
        if (!activeDrawMesh)
        {
            for (int i = 0; i < 100; i++)
            {
                for (int j = 0; j < 100; j++)
                {
                    for (int k = 0; k < 10; k++)
                    {
                        UUtility.CreatPrimitiveType(PrimitiveType.Cube, new Vector3(i, j, k) * 2);
                    }
                }
            }
        }
        else
        {
            Material mat = new Material(Shader.Find("Standard"));
            Mesh mesh = GameObject.CreatePrimitive(PrimitiveType.Cube).GetComponent<MeshFilter>().mesh;
            GraphicsManager.Instance.AddGraphics(Camera.main, () =>
            {
                MeshManager.Instance.LineMaterial.SetPass(0);
                GL.Begin(GL.LINES);
                GL.Color(Color.red);
                GL.Vertex(Vector3.zero);
                GL.Vertex(Vector3.up * 10);
                GL.End();

                for (int i = 0; i < 100; i++)
                {
                    for (int j = 0; j < 100; j++)
                    {
                        for (int k = 0; k < 10; k++)
                        {
                            mat.SetPass(0);
                            Graphics.DrawMeshNow(mesh, new Vector3(i, j, k) * 2, Quaternion.identity);
                        }
                    }
                }
            });
        }
    }
}
