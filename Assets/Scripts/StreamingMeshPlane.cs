using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Net;

using UnityEngine.VR.WSA.Persistence;
using UnityEngine.VR.WSA;


public class StreamingMeshPlane : MonoBehaviour {

    Mesh heightFieldMesh;
    MeshRenderer meshRenderer;
    BoxCollider bCollider; 

    public string ObjectAnchorStoreName;

    WorldAnchorStore anchorStore;
    bool Placing = false;

    GameObject txt;
    TextMesh txtField;

    // Use this for initialization
    void Start () {
        
        WorldAnchorStore.GetAsync(AnchorStoreReady);

        MeshFilter mainMeshFilter = GetComponent<MeshFilter>();

        //create mesh for renderingx
        heightFieldMesh = new Mesh();
        heightFieldMesh.MarkDynamic();
        heightFieldMesh.name = "StreamingMesh";
        mainMeshFilter.mesh = heightFieldMesh;

        meshRenderer = GetComponent<MeshRenderer>();
        bCollider = GetComponent<BoxCollider>();

        txt = Instantiate(Resources.Load("TextMesh"), transform.position + (Vector3.up *0.5f), transform.rotation) as GameObject;
        txt.transform.parent = this.transform;

        txtField = txt.GetComponent<TextMesh>();
        txtField.text = ObjectAnchorStoreName;        
    }
    
    void AnchorStoreReady(WorldAnchorStore store)
    {
        anchorStore = store;
        Placing = true;

        Debug.Log("Find Anchor for " + ObjectAnchorStoreName);
        string[] ids = anchorStore.GetAllIds();
        for (int index = 0; index < ids.Length; index++)
        {
            Debug.Log(ids[index]);
            if (ids[index] == ObjectAnchorStoreName)
            {
                WorldAnchor wa = anchorStore.Load(ids[index], gameObject);
                Placing = false;
                break;
            }
        }
    }

    // Update is called once per frame
    void Update () {

        if (Placing)
        {
            gameObject.transform.position = Camera.main.transform.position + Camera.main.transform.forward * 2;

            Vector3 targetPostition = new Vector3(Camera.main.transform.position.x,
                                       this.transform.position.y,
                                       Camera.main.transform.position.z);

            gameObject.transform.LookAt(targetPostition);
        }
    }

    public void Place()
    {
        Debug.Log("Place " + gameObject.name);
        if (anchorStore == null)
        {
            return;
        }

        if (Placing)
        {
            WorldAnchor attachingAnchor = gameObject.AddComponent<WorldAnchor>();
            if (attachingAnchor.isLocated)
            {
                bool saved = anchorStore.Save(ObjectAnchorStoreName, attachingAnchor);
                Debug.Log("Anchor saved: " + saved);
            }
            else
            {
                attachingAnchor.OnTrackingChanged += AttachingAnchor_OnTrackingChanged;
            }
        }
        else
        {
            WorldAnchor anchor = gameObject.GetComponent<WorldAnchor>();
            if (anchor != null)
            {
                DestroyImmediate(anchor);
            }

            string[] ids = anchorStore.GetAllIds();
            for (int index = 0; index < ids.Length; index++)
            {
                Debug.Log(ids[index]);
                if (ids[index] == ObjectAnchorStoreName)
                {
                    bool deleted = anchorStore.Delete(ids[index]);
                    Debug.Log("Anchor deleted: " + deleted);
                    break;
                }
            }

            gameObject.transform.rotation = Quaternion.identity;
        }

        Placing = !Placing;
    }

    private void AttachingAnchor_OnTrackingChanged(WorldAnchor self, bool located)
    {
        if (located)
        {
            
            bool saved = anchorStore.Save(ObjectAnchorStoreName, self);
            Debug.Log("Saving persisted position in callback " + saved);
            self.OnTrackingChanged -= AttachingAnchor_OnTrackingChanged;
        }
    }

    public void clear()
    {
        heightFieldMesh.Clear();
    }

    //update the dynamic mesh
    public void updateMesh( ActiveMesh a)
    {
        txtField.text = a.author + "\r\n" + a.title;
        
        byte[] meshData = a.lastFrame;

        int offset = 0;
        if(meshData == null) return;
        if(meshData.Length < 5) return;

        int taglength = Encoding.ASCII.GetByteCount("MESHDATA");

        string tag = Encoding.ASCII.GetString(meshData, 0, taglength);
        if (tag != "MESHDATA")
        {
            Debug.LogError("Invalid frame taglength");
            return;
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

            heightFieldMesh.RecalculateBounds();
            heightFieldMesh.RecalculateNormals();

            bCollider.center = meshRenderer.bounds.center - transform.position;
            bCollider.size = heightFieldMesh.bounds.size;

        }
        catch (Exception ex)
        {
            Debug.LogError("Error updating mesh " + ex.Message);
        }
    }
}
