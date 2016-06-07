using UnityEngine;
using System;
using System.Collections;
using System.Linq;
using System.IO;
using System.Text;
using System.Net;

#if !UNITY_EDITOR
using System.Net.Http;
using System.Threading.Tasks;
#endif


public class Main : MonoBehaviour {

    //string path = @"C:\Users\thomas\dev\unity\StreamingMesh\MeshData";
    string debugMeshData = "";
    FileInfo[] meshDataFiles;
    int meshDataFilesIndex = 0;
    
    //CHANGE THIS to where you are running node, note the HL emulator won't connect to localhost.
    string latestDataUrl = "http://[ip-address]:8080/mesh/?";
    
    GameObject main;
    MeshFilter mainMeshFilter;
    Mesh heightFieldMesh;

    object frameLock = new object();
    byte[] lastframe;
    bool newFrame = false;
#if !UNITY_EDITOR
    HttpClient client;
#endif

    // Use this for initialization
    void Start() {

        Debug.Log("Start!");

        //find the object that holds the mesh
        GameObject main = GameObject.Find("Plane");
        MeshFilter mainMeshFilter = main.GetComponent<MeshFilter>();

        //create mesh for rendering
        heightFieldMesh = new Mesh();
        heightFieldMesh.name = "HoloMesh";
        mainMeshFilter.mesh = heightFieldMesh;
        
        //process files from a folder, otherwise download "latest"
        if (debugMeshData.Length > 0)
        {
            meshDataFiles = Directory.GetFiles(debugMeshData).Select(fn => new FileInfo(fn)).
                                        OrderBy(f => f.CreationTime).ToArray();
            foreach (FileInfo file in meshDataFiles)
            {
                Debug.Log("file " + file.Name);
            }
            //InvokeRepeating("readFile", 0, 0.1f);
        }
        else
        {
            
#if !UNITY_EDITOR
            client = new HttpClient();
            InvokeRepeating("readURL", 0, 0.03f);
#else
            StartCoroutine(getLatest(new WWW(latestDataUrl)));
#endif
        }
    }

    // Update is called once per frame
    void Update()
    {
        if( lastframe != null && newFrame)
        {
            processFrame(lastframe);
            newFrame = false;         
        }
    }

    void readURL()
    {
#if !UNITY_EDITOR
        readURLAsync();
#endif
    }


#if !UNITY_EDITOR
    private async Task readURLAsync() {

        byte[] raw = await client.GetByteArrayAsync(latestDataUrl);
        lock (frameLock)
        {
            lastframe = new byte[raw.Length];
            Array.Copy(raw, lastframe, raw.Length);
            newFrame = true;
        }
    }
#endif


#if UNITY_EDITOR
    //get the latest frame as fast as you can
    IEnumerator getLatest( WWW rev)
    {
        Debug.Log("getLatest!");        
        yield return rev;

        Debug.Log("downloaded!");
        processFrame(rev.bytes);

        yield return new WaitForSeconds(0.03F);

        //get the next frame
        StartCoroutine(getLatest(new WWW(latestDataUrl)));
    }
#endif

    //read the next file in the folder
    void readFile()
    {
        FileInfo file = meshDataFiles[meshDataFilesIndex];
        FileStream f = file.Open(FileMode.Open, FileAccess.Read);

        Debug.Log("Read File: " + file.Name);

        byte[] buff = new byte[f.Length];
        f.Read(buff, 0, buff.Length);

        processFrame(buff);

        meshDataFilesIndex++;
        if (meshDataFilesIndex >= meshDataFiles.Count()) meshDataFilesIndex = 0;
    }

    //process data downloaded or from a file
    void processFrame(byte[] meshData)
    {
        Debug.Log("processFrame " + meshData.Count());

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

        Debug.Log("vert:" + vertDataCount + " color:" + colorDataCount + " triangles:" + faceDataCount);

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
