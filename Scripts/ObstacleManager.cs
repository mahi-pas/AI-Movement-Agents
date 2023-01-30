using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObstacleManager : MonoBehaviour
{
    //All agents reference this obstacle manager. This manager keeps track of all of it to reduce computation.
    public string obstacleTag = "ObstacleIndicator";
    public GameObject[] obstacles;
    // Start is called before the first frame update
    void Start()
    {
        RefreshObstacles();
    }

    public void RefreshObstacles(){
        obstacles = GameObject.FindGameObjectsWithTag(obstacleTag);
    }
}
