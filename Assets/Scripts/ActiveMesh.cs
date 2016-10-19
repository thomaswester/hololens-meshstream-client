using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

// Represents 1 streaming hologram
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

    public byte[] lastFrame
    {
        get
        {
            if (_lastFrame == null) return null;

            byte[] r = new byte[_lastFrame.Length];
            lock (lastFrameLock)
            {
                _lastFrame.CopyTo(r, 0);
                draw = false;
            }
            return r;
        }

        set
        {
            lock (lastFrameLock)
            {
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