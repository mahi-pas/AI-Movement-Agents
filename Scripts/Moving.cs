using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Moving : MonoBehaviour
{   
    public enum Dir {X, Y};
    public Dir direction = Dir.X;
    public float turnTime = 1f;
    public float speed = 1f;

    private void Start() {
        Invoke("Turn",turnTime);
    }

    // Update is called once per frame
    void Update()
    {
        switch(direction){
            case Dir.X:
                transform.position += new Vector3(speed * Time.deltaTime,0,0);
                break;
            case Dir.Y:
                transform.position += new Vector3(0,speed * Time.deltaTime,0);
                break;
        }
    }

    public void Turn(){
        speed *= -1;
        Invoke("Turn",turnTime);
    }

    public Vector3 OffsetInTime(float time){
        switch(direction){
            case Dir.X:
                return new Vector3(speed * time,0,0);
            case Dir.Y:
                return new Vector3(0,speed * time,0);
        }
        return Vector3.zero;
    }
}
