using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Net;

using UnityEngine.VR.WSA.Persistence;
using UnityEngine.VR.WSA.Input;
using UnityEngine.VR.WSA;


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

    //"slot" data
    public int id;
    public bool free;    
    public string author;
    public string title;
    public string platform;
    public Vector3 origin;

    //store last frame async
    public object lastFrameLock = new object();
    private byte[] _lastFrame;
    public bool draw = false;
    public bool wait = false;

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

    GestureRecognizer recognizer;

    //it's a hack
    static string getMeshUrl() {
        return Main.instance.nodeServerURL + "/mesh";
    }

    static string ipConfigURL = "http://www.dropbox.com/s/gth4sax66ezbvo9/config.json?dl=1";
    //static string baseURL = "http://127.0.0.1:8080";
    //static string baseURL = "http://192.168.0.23:8080";

    string nodeServerURL;
    object activeMeshLock = new object();
    List<ActiveMesh> activeMeshes;
    
    public static Main instance;

#if !UNITY_EDITOR
    HttpClient client;
#endif

    // Use this for initialization
    void Awake() {

        instance = this;

        Debug.Log("Awake!");        
        activeMeshes = new List<ActiveMesh>();

        recognizer = new GestureRecognizer();
        recognizer.SetRecognizableGestures(GestureSettings.Tap);
        recognizer.TappedEvent += Recognizer_TappedEvent;

        recognizer.StartCapturingGestures();


        StartCoroutine( getServerIp(new WWW(ipConfigURL)) );

    }

    IEnumerator getServerIp( WWW ipConfigURL)
    {
        yield return ipConfigURL;
        
        JSONObject obj = new JSONObject(ipConfigURL.text);
        nodeServerURL = obj["ipAddress"].ToString( true );

        Debug.Log("Connect to: " + nodeServerURL );
        startListening();
    }

    private void startListening()
    {

#if !UNITY_EDITOR
        client = new HttpClient();
        InvokeRepeating("updateStreamingMeshes", 0, 0.03f);
        InvokeRepeating("getMeshState", 0, 3.0f);
#else
        Debug.Log("Using url :" + Main.getMeshUrl() );
        StartCoroutine(getLatest(new WWW(Main.getMeshUrl() ) ) );
        StartCoroutine(updateMeshes());
#endif

    }

    private void Recognizer_TappedEvent(InteractionSourceKind source, int tapCount, Ray headRay)
    {
        Debug.Log("tapped");

        // Figure out which hologram is focused this frame.
        GameObject focusedObject;

        // Do a raycast into the world based on the user's
        // head position and orientation.
        var headPosition = Camera.main.transform.position;
        var gazeDirection = Camera.main.transform.forward;

        RaycastHit hitInfo;
        if (Physics.Raycast(headPosition, gazeDirection, out hitInfo))
        {
            // If the raycast hit a hologram, use that as the focused object.
            focusedObject = hitInfo.collider.gameObject;
            StreamingMeshPlane plane = focusedObject.GetComponent<StreamingMeshPlane>();
            if (plane != null) {
                plane.Place();
                Debug.Log("focusedObject is" + plane.ObjectAnchorStoreName);
            }
        }
        else
        {
            // If the raycast did not hit a hologram, clear the focused object.
            focusedObject = null;
            Debug.Log("focusedObject is null" );
        }
        
    }

    // Update is called once per frame
    void Update()
    {
        foreach(ActiveMesh a in activeMeshes)
        {
            if(!a.free)
            {
                //slot is occupied draw it
                if (a.draw)
                {
                    //new data is available draw
                    if( a.plane == null)
                    {
                        a.plane = (GameObject)Instantiate(streamingMeshPlane, a.origin, new Quaternion());
                        
                        a.planeScript = a.plane.GetComponent<StreamingMeshPlane>();
                        a.planeScript.ObjectAnchorStoreName = "mesh " +a.id;
                    }
                    else
                    {
                        a.planeScript.updateMesh(a);
                    }
                }
            }
            else
            {
                //clear if freed
                if (a.plane != null)
                {
                    a.planeScript.clear();
                }
            }
        }
    }

#if !UNITY_EDITOR
    void getMeshState()
    {
        getMeshStateAsync();
    }

    private async Task getMeshStateAsync() {

        string data = await client.GetStringAsync( Main.getMeshUrl() );
        processMeshJSON( data );

        
    }

    void updateStreamingMeshes()
    {
        foreach (ActiveMesh a in activeMeshes)
        {
            if (!a.free && !a.wait)
            {
                updateStreamingMeshAsync(a);
            }
        }      
    }

    private async Task updateStreamingMeshAsync( ActiveMesh a)
    {
        a.wait = true;
        string url = Main.getMeshUrl() + "/" + a.id;
        byte[] raw = await client.GetByteArrayAsync(url);
        a.lastFrame = raw;
        a.wait = false;
    }
    

#endif

#if UNITY_EDITOR
    IEnumerator updateMeshes()
    {
        foreach( ActiveMesh a in activeMeshes)
        {
            if (!a.free)
            {
                StartCoroutine(updateMesh( new WWW(Main.getMeshUrl() + "/" + a.id), a));               
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
        StartCoroutine(getLatest(new WWW(Main.getMeshUrl() )));
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
                if (j["info"]["title"] != null) a.title = j["info"]["title"].ToString(true);
                if (j["info"]["author"] != null) a.author = j["info"]["author"].ToString(true);
                a.id = count;
                a.origin = new Vector3(j["origin"][0].n, j["origin"][1].n, j["origin"][2].n);
                a.free = j["free"].b;

                activeMeshes.Add(a);
            }
            else
            {
                if (j["info"]["title"] != null) a.title = j["info"]["title"].ToString(true);
                if (j["info"]["author"] != null) a.author = j["info"]["author"].ToString(true);

                a.free = j["free"].b;
            }
            count++;
        }
    }
    
}
