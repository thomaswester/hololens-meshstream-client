using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Net;

public class StreamingMeshPlane : MonoBehaviour {

    Mesh heightFieldMesh;

	// Use this for initialization
	void Start () {
        Debug.Log("started!");
        MeshFilter mainMeshFilter = GetComponent<MeshFilter>();

        //create mesh for rendering
        heightFieldMesh = new Mesh();
        heightFieldMesh.name = "StreamingMesh";
        mainMeshFilter.mesh = heightFieldMesh;     
    }

    // Update is called once per frame
    void Update () {
	
	}

    public void updateMesh( byte[] meshData)
    {

        int offset = 0;
        int taglength = Encoding.ASCII.GetByteCount("MESHDATA");

        string tag = Encoding.ASCII.GetString(meshData, 0, taglength);
        if (tag != "MESHDATA")
        {
            Debug.LogError("invalid frame taglength: " + taglength + " tag:" + tag);
            //return;
        }

        //get the sizes of the different arrays
        offset = taglength;


        int vertDataCount = BitConverter.ToInt16(meshData, offset);
        offset += 2;

        int colorDataCount = BitConverter.ToInt16(meshData, offset);
        offset += 2;

        int faceDataCount = BitConverter.ToInt16(meshData, offset);
        offset += 2;

        //unused
        offset += 2;

        //Debug.Log("vert:" + vertDataCount + " color:" + colorDataCount + " triangles:" + faceDataCount);

        /*
            Vertex position data:
            Type:  32 bit float
            Count: Vertex count x 3
        */

        int arraylen = vertDataCount * 3 * 4;
        Vector3[] vertData = new Vector3[vertDataCount];

        int index = 0;
        for (int i = 0; i < arraylen; i += 12)
        {
            Vector3 v = new Vector3();
            v.x = BitConverter.ToSingle(meshData, offset + i);
            v.y = BitConverter.ToSingle(meshData, offset + i + 4);
            v.z = BitConverter.ToSingle(meshData, offset + i + 8);

            vertData[index] = v;
            index++;
        }
        offset += arraylen;


        /*
            Color data:
            Type:     Unsigned bytes
            Count:    Color count x 3 
            Comments: Colors are stored in 8 bits per channel, R G B, R G B, R G B ... 
        */

        arraylen = colorDataCount * 3;
        Color[] colorData = new Color[colorDataCount];
        index = 0;
        for (int i = 0; i < arraylen; i += 3)
        {

            float r = (int)meshData[offset + i] / 255.0f;
            float g = (int)meshData[offset + i + 1] / 255.0f;
            float b = (int)meshData[offset + i + 2] / 255.0f;

            colorData[index] = new Color(r, g, b);
            index++;
        }
        offset += arraylen;


        /*
            Triangle data:
            Type:     Unsigned 16 bit integers
            Count:    Number of triangles x 3 (three vertex indices per triangle)
            Comments: These numbers index into the vertex array
         */

        arraylen = faceDataCount * 3 * 2;
        int[] triangleData = new int[faceDataCount * 3];

        index = 0;
        for (int i = 0; i < arraylen; i += 2)
        {
            triangleData[index] = BitConverter.ToInt16(meshData, offset + i);
            index++;
        }
        offset += arraylen;


        
        heightFieldMesh.Clear();

        try
        {
            heightFieldMesh.vertices = vertData;
            heightFieldMesh.colors = colorData;
            //heightFieldMesh.uv = meshUVCoords;
            heightFieldMesh.triangles = triangleData; 
        }
        catch (Exception ex)
        {
            Debug.LogError("error updating mesh");
        }
    }
}
