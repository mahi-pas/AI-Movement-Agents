using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlockManager : MonoBehaviour
{
    public string flockTag = "Flock1";
    public GameObject[] flock;
    public Transform target;

    // Start is called before the first frame update
    void Start()
    {
        RefreshFlock();
    }

    public void RefreshFlock(){
        flock = GameObject.FindGameObjectsWithTag(flockTag);
    }

}
