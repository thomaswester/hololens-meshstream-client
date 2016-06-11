using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Net;

#if !UNITY_EDITOR
using System.Net.Http;
using System.Threading.Tasks;
#endif

public class ActiveMesh
{
    /*
     * {
	"free": true,
	"info": {},
	"origin": [1, 0, 0]
    { */

    public int id;

    public bool free;
    public string author;
    public string title;
    public string platform;
    public Vector3 origin;

    public object lastFrameLock = new object();
    private byte[] _lastFrame;
    public bool draw = false;
    public byte[] lastFrame {
        get
        {
            if (_lastFrame == null) return null;

            byte [] r = new byte[_lastFrame.Length];
            lock (lastFrameLock) { 
                _lastFrame.CopyTo(r,0);
                draw = false;
            }
            return r;
        }

        set
        {
            lock (lastFrameLock) { 
                this._lastFrame = value;
                draw = true;
            }
        }
    }

    public GameObject plane;
    public StreamingMeshPlane planeScript;
    
    public ActiveMesh()
    {
        Debug.Log("Create Mesh");
        
    }
    
}

public class Main : MonoBehaviour {

    public GameObject streamingMeshPlane;

    //string path = @"C:\Users\thomas\dev\unity\StreamingMesh\MeshData";
    string debugMeshData = "";
    FileInfo[] meshDataFiles;
    int meshDataFilesIndex = 0;
    
    //CHANGE THIS to where you are running node, note the HL emulator won't connect to localhost.
    //string latestDataUrl = "http://[ip-address]:8080/mesh/?";
    static string baseURL = "http://172.16.0.115:8080";
    string activeMeshURL = baseURL + "/mesh";
    
    //GameObject main;
    //MeshFilter mainMeshFilter;
    //Mesh heightFieldMesh;

    object activeMeshLock = new object();
    List<ActiveMesh> activeMeshes;


    
#if !UNITY_EDITOR
    HttpClient client;
#endif

    // Use this for initialization
    void Start() {

        Debug.Log("Start!");
        
        activeMeshes = new List<ActiveMesh>();
        
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
            StartCoroutine(getLatest(new WWW(activeMeshURL)));

            StartCoroutine(updateMeshes());
#endif
        }
    }

    // Update is called once per frame
    void Update()
    {
        foreach(ActiveMesh a in activeMeshes)
        {
            if(!a.free)
            {
                if (a.draw)
                {
                    if( a.plane == null)
                    {
                       a.plane = (GameObject)Instantiate(streamingMeshPlane, a.origin, new Quaternion());
                       a.planeScript = a.plane.GetComponent<StreamingMeshPlane>();
                    }
                    else
                    {
                        a.planeScript.updateMesh(a.lastFrame);
                    }

                    //processFrame(a.lastFrame, a);
                }
            }
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
    IEnumerator updateMeshes()
    {
        foreach( ActiveMesh a in activeMeshes)
        {
            if (!a.free)
            {
                StartCoroutine(updateMesh( new WWW(baseURL + "/mesh/" + a.id), a));               
            }
        }
        yield return new WaitForSeconds(0.03F);
        StartCoroutine(updateMeshes());
    }

    IEnumerator updateMesh(WWW rev, ActiveMesh a)
    {
        yield return rev;
        a.lastFrame = rev.bytes;
    }

    IEnumerator getLatest( WWW rev)
    {
        yield return rev;
        processMeshJSON(rev.text);
       
        yield return new WaitForSeconds(1.0F);

        //get the next frame
        StartCoroutine(getLatest(new WWW(activeMeshURL)));
    }
#endif

    void processMeshJSON( string json)
    {
        JSONObject obj = new JSONObject(json);
        int count = 0;
        
        foreach (JSONObject j in obj.list)
        {
            ActiveMesh a = null;           
            a = activeMeshes.FirstOrDefault(item => item.id == count);
           
            if (a == null)
            {
                Debug.Log("Create new ActiveMesh");
                a = new ActiveMesh();
                a.id = count;
                a.origin = new Vector3(j["origin"][0].n, j["origin"][1].n, j["origin"][2].n);
                a.free = j["free"].b;

                activeMeshes.Add(a);
            }
            else
            {
                a.free = j["free"].b;
            }
            count++;
        }
    }

    //read the next file in the folder
    void readFile()
    {
        FileInfo file = meshDataFiles[meshDataFilesIndex];
        FileStream f = file.Open(FileMode.Open, FileAccess.Read);

        Debug.Log("Read File: " + file.Name);

        byte[] buff = new byte[f.Length];
        f.Read(buff, 0, buff.Length);

        //processFrame(buff,new Vector3(0,0,0) );

        meshDataFilesIndex++;
        if (meshDataFilesIndex >= meshDataFiles.Count()) meshDataFilesIndex = 0;
    }
    
}
