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
using UnityEngine.Windows.Speech;

#if !UNITY_EDITOR
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Storage;
#endif


public class Main : MonoBehaviour {


    //reference to the mesh that will be use to instantiate incoming meshes
    public GameObject streamingMeshPlane;

    //dialogs
    public GameObject settingDialog;
    public GameObject helpDialog;
    public GameObject statusDialog;

    SpatialMappingRenderer occlusionRender;

    //ability to tap and place meshes
    GestureRecognizer recognizer;

    public string nodeServerURL;//only used in editor mode
    public string nodeServerPort;//only used in editor mode

    public string nodeServerFullURL;//http:// + nodeServerURL : nodeServerPort /
    public string MeshURL;//nodeServerFullURL + /mesh

    List<ActiveMesh> activeMeshes;//list of active holograms
    
    public static Main instance;//singleton reference

    //voice recognition
    private KeywordRecognizer commandKeywordRecognizer;
    private KeywordRecognizer alphabetRecognizer;
    
    //list of voice commands
    string[] Commands = new String[13] {
        "settings",
        "server",
        "port",
        "occlusion",
        "collision",
        "save",
        "help",
        "status",
        "connect",
        "cursor",
        "move",
        "place",
        "reset"
    };

    //alphanumeric list so you can dictate server IP address or DNS name
    List<string> Alphanumeric;
    string dictationBuffer;

    enum WorldState
    {
        DEFAULT,
        SETTINGS,
        HELP,
        PLACING
    }

    WorldState currentState = WorldState.DEFAULT;
    bool showingCursor = false;//showing cursor
    bool showingStatus = false;//showing status dialog

    TextMesh currentSettingMesh;//selected settings variable: server, port
    TextMesh currentSettingLabel;//selected settings variable: server, port
    string currentSettingVar = "";//selected settings variable: server, port

    bool showHelp = false;//show help for first launch?

#if !UNITY_EDITOR
    HttpClient client;
#endif

    // Use this for initialization
    void Awake() {

        instance = this;
        Alphanumeric = new List<string>();

        // - . / 0-9
        for (int i = 45; i < 58; i++)
        {
            Alphanumeric.Add(((char)i).ToString());
        }

        Alphanumeric.Add("backspace");
        Alphanumeric.Add("clear");

        /* alphabet dictation really does not work reliably
        //a-z
        for (int i = 97; i < 97 + 26; i++)
        {
            Alphanumeric.Add(( (char)i).ToString() );
        }
        */

        Debug.Log("Awake!");        
        activeMeshes = new List<ActiveMesh>();

        recognizer = new GestureRecognizer();
        recognizer.SetRecognizableGestures(GestureSettings.Tap);
        recognizer.TappedEvent += Recognizer_TappedEvent;

        recognizer.StartCapturingGestures();

        commandKeywordRecognizer = new KeywordRecognizer(Commands);
        commandKeywordRecognizer.OnPhraseRecognized += CommandKeywordRecognizer_OnPhraseRecognized;
        commandKeywordRecognizer.Start();

        alphabetRecognizer = new KeywordRecognizer(Alphanumeric.ToArray());
        alphabetRecognizer.OnPhraseRecognized += AlphabetRecognizer_OnPhraseRecognized;
        dictationBuffer = "";


#if !UNITY_EDITOR
        var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
        
        if (localSettings.Values["nodeServerURL"] != null ) { 
            nodeServerURL = localSettings.Values["nodeServerURL"].ToString();
            settingDialog.transform.Find("server").GetComponent<TextMesh>().text = nodeServerURL;
        }
        else
        {
            settingDialog.transform.Find("server").GetComponent<TextMesh>().text = "";
        }

        if (localSettings.Values["nodeServerPort"] != null)
        {
            nodeServerPort = localSettings.Values["nodeServerPort"].ToString();
            settingDialog.transform.Find("port").GetComponent<TextMesh>().text = nodeServerPort;
        }
        else
        {
            settingDialog.transform.Find("port").GetComponent<TextMesh>().text = "";
        }

        if(nodeServerURL != "" && nodeServerPort != "")
        {
            SetServer(nodeServerURL, nodeServerPort);
        }
        else if(nodeServerURL != "")
        {
            SetServer(nodeServerURL);
        }

        if (localSettings.Values["showHelp"] != null)
        {
            showHelp = bool.Parse(localSettings.Values["showHelp"].ToString());
        }else
        {
            showHelp = true;
        }
#else
        SetServer(nodeServerURL, nodeServerPort);
#endif

        settingDialog.SetActive(false);
        if(!showHelp) { 
            helpDialog.SetActive(false);
        }
        statusDialog.SetActive(false);

        occlusionRender = Camera.main.GetComponent<SpatialMappingRenderer>();
        occlusionRender.enabled = false;
    }

    // Update is called once per frame
    void Update()
    {
        foreach (ActiveMesh a in activeMeshes)
        {
            if (!a.free)
            {
                //slot is occupied draw it
                if (a.draw)
                {
                    //new data is available draw
                    if (a.plane == null)
                    {
                        a.plane = (GameObject)Instantiate(streamingMeshPlane, a.origin, new Quaternion());

                        a.planeScript = a.plane.GetComponent<StreamingMeshPlane>();
                        a.planeScript.ObjectAnchorStoreName = "mesh " + a.id;
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

        /*
        if (currentDialog.activeSelf)
        {
            currentDialog.transform.position = Camera.main.transform.position + Camera.main.transform.forward * 1.5f;

            Vector3 targetPostition = new Vector3(Camera.main.transform.position.x,
                                       this.transform.position.y,
                                       Camera.main.transform.position.z);

            currentDialog.transform.LookAt(targetPostition);
        }*/
    }

    public void SetServer(string serverurl, string serverport = "")
    {
        if (serverport == "")
        {
            nodeServerFullURL = "http://" + serverurl;
        }
        else
        {
            nodeServerFullURL = "http://" + serverurl + ":" + serverport;
        }

        MeshURL = nodeServerFullURL + "/mesh";
    }

    private void startListening()
    {

#if !UNITY_EDITOR
        client = new HttpClient();
        InvokeRepeating("updateStreamingMeshes", 0, 0.03f);
        InvokeRepeating("getMeshState", 0, 3.0f);
#else
        Debug.Log("Using url :" + MeshURL);
        StartCoroutine(getLatest(new WWW( MeshURL ) ) );
        StartCoroutine(updateMeshes());
#endif

    }

    private void Recognizer_TappedEvent(InteractionSourceKind source, int tapCount, Ray headRay)
    {

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
            if (plane != null)
            {
                plane.Place();

            }
        }
        else
        {
            // If the raycast did not hit a hologram, clear the focused object.
            focusedObject = null;
        }

    }


    private void AlphabetRecognizer_OnPhraseRecognized(PhraseRecognizedEventArgs args)
    {
        Debug.Log("recognized alphabet " + args.text + " " + dictationBuffer);
        ProcessDication(args.text);
    }

    private void CommandKeywordRecognizer_OnPhraseRecognized(PhraseRecognizedEventArgs args)
    {
        Debug.Log("recognized command " + args.text);
        switch (args.text)
        {
            case "connect":
                Connect();
                break;
            case "settings":
                ToggleDialog(args.text);
                break;

            case "server":
                UpdateSetting(args.text);
               break;
            case "port":
                UpdateSetting(args.text);              
                break;
            case "occlusion":
                //UpdateSetting(args.text);//not implemented yet
                break;
            case "collision":
                //UpdateSetting(args.text);//not implemented yet
                break;

            case "save":
                SaveSettings();
                break;

            case "help":
                ToggleDialog(args.text);
                break;

            case "status":
                ToggleDialog(args.text);
                break;

            case "cursor":
                ToggleCursor();
                break;

            case "move":
                MoveSelected();
                break;
            case "place":
                PlaceSelected();
                break;
            case "reset":
                if( currentState == WorldState.SETTINGS)
                {
                    ProcessDication(args.text);
                }
                else
                { 
                    ResetStreams();
                }
                break;
        }
    }

    void UpdateState(WorldState newstate)
    {
        currentState = newstate;
    }

    void Connect()
    {

        startListening();
    }
    
    void ToggleDialog(string dialog)
    {
        GameObject activeDialog = null;

        switch (dialog)
        {
            case "settings":
                if (currentState == WorldState.SETTINGS)
                {
                    if (alphabetRecognizer.IsRunning) alphabetRecognizer.Stop();
                    currentSettingVar = "";
                    dictationBuffer = "";
                    settingDialog.SetActive(false);

                    UpdateState(WorldState.DEFAULT);
                }
                else
                {
                    UpdateState(WorldState.SETTINGS);
                    settingDialog.SetActive(true);

                    activeDialog = settingDialog;
                }
                break;

            case "help":
                if (currentState == WorldState.HELP)
                {
                    helpDialog.SetActive(false);
                    UpdateState(WorldState.DEFAULT);

                }
                else
                {
                    UpdateState(WorldState.HELP);
                    helpDialog.SetActive(true);

                    activeDialog = helpDialog;
                }
                break;
         
        }

        if(activeDialog != null) {

            activeDialog.transform.position = Camera.main.transform.position + Camera.main.transform.forward * 1.5f;

            Vector3 targetPostition = new Vector3(Camera.main.transform.position.x,
                                       activeDialog.transform.position.y,
                                       Camera.main.transform.position.z);

            activeDialog.transform.LookAt(targetPostition);
            activeDialog.transform.Rotate(new Vector3(0, 180, 0));
        }
    }

    void UpdateSetting(string setting)
    {
        if (currentState != WorldState.SETTINGS) return;

        //auto save what we have done before
        if(currentSettingVar != "")
        {
            SaveSettings();
        }

        switch(setting)
        {
            case "server":
                if (!alphabetRecognizer.IsRunning) alphabetRecognizer.Start();
                currentSettingVar = "server";
                currentSettingMesh = settingDialog.transform.Find( currentSettingVar ).GetComponent<TextMesh>();
                currentSettingLabel = settingDialog.transform.Find(currentSettingVar + "_label").GetComponent<TextMesh>();
                currentSettingLabel.color = Color.red;
                currentSettingMesh.text = nodeServerURL;
                dictationBuffer = "";
                break;

            case "port":
                if (!alphabetRecognizer.IsRunning) alphabetRecognizer.Start();
                currentSettingVar = "port";
                currentSettingMesh = settingDialog.transform.Find(currentSettingVar).GetComponent<TextMesh>();
                currentSettingLabel = settingDialog.transform.Find(currentSettingVar + "_label").GetComponent<TextMesh>();
                currentSettingLabel.color = Color.red;
                currentSettingMesh.text = nodeServerPort;
                dictationBuffer = "";
                break;
        }
    }

    void ProcessDication(string text)
    {
        if (currentState == WorldState.SETTINGS)
        {
            switch (text)
            {
                case "clear":
                    dictationBuffer = "";
                    break;

                case "backspace":
                    dictationBuffer = dictationBuffer.Substring(0, dictationBuffer.Length - 2);
                    break;

                case "reset":
                    if (currentSettingVar == "server") dictationBuffer = nodeServerURL;
                    else if(currentSettingVar == "port") dictationBuffer = nodeServerPort;
                    break;

                default:
                    if(Alphanumeric.Contains(text)) dictationBuffer += text;
                    break;
            }
            currentSettingMesh.text = dictationBuffer;
        }
        else
        {
            if (alphabetRecognizer.IsRunning) alphabetRecognizer.Stop();
            currentSettingVar = "";
            dictationBuffer = "";
        }
    }

    void SaveSettings()
    {

#if !UNITY_EDITOR
        var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
        if (currentSettingVar != "") { 
            switch(currentSettingVar)
            {
                case "server":
                    localSettings.Values["nodeServerURL"] = currentSettingMesh.text;
                    break;

                case "port":
                    localSettings.Values["nodeServerPort"] = currentSettingMesh.text;
                    break;
            }

            if (localSettings.Values["nodeServerURL"] != null)
            {
                nodeServerURL = localSettings.Values["nodeServerURL"].ToString();
            }
            if (localSettings.Values["nodeServerPort"] != null)
            {
                nodeServerPort = localSettings.Values["nodeServerPort"].ToString();
            }

            if (nodeServerURL != null && nodeServerPort != null)
            {
                SetServer(nodeServerURL, nodeServerPort);
            }
            else if (nodeServerURL != null)
            {
                SetServer(nodeServerURL);
            }

            currentSettingLabel.color = Color.white;
            currentSettingVar = "";
            currentSettingMesh = null;
            currentSettingLabel = null;
            alphabetRecognizer.Stop();
        }

       
#endif

    }

    void ToggleCursor()
    {

    }

    void MoveSelected()
    {

    }

    void PlaceSelected()
    {

    }

    void ResetStreams()
    {

    }

#if !UNITY_EDITOR
    void getMeshState()
    {
        getMeshStateAsync();
    }

    private async Task getMeshStateAsync() {

        string data = await client.GetStringAsync( MeshURL );
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
        string url = MeshURL + "/" + a.id;
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
                StartCoroutine(updateMesh( new WWW(MeshURL + "/" + a.id), a));               
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
        StartCoroutine(getLatest(new WWW(MeshURL )));
    }
#endif

    void processMeshJSON( string json)
    {
        JSONObject obj = new JSONObject(json);
        int index = 0;
        
        foreach (JSONObject j in obj.list)
        {
            ActiveMesh a = null;         
            
            //find mesh  
            a = activeMeshes.FirstOrDefault(item => item.id == index);
           
            if (a == null)
            {
                Debug.Log("Create new ActiveMesh");
                a = new ActiveMesh();
                if (j["info"]["title"] != null) a.title = j["info"]["title"].ToString(true);
                if (j["info"]["author"] != null) a.author = j["info"]["author"].ToString(true);
                a.id = index;
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
            index++;
        }
    }
    
}
