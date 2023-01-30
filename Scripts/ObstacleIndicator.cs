using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObstacleIndicator : MonoBehaviour
{   
    public Moving ms;

    // Start is called before the first frame update
    void Start()
    {
        ms = transform.parent.gameObject.GetComponent<Moving>();
    }

    public Vector3 PositionInTime(float time){
        if(ms!=null) return transform.position + ms.OffsetInTime(time);
        else return transform.position;
    }

}
