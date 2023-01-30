using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{   
    public float speed = 10.0f;
    public float rotationSpeed = 100.0f;
    public float currentSpeed = 0;

    [Header("AI")]
    public float predictTime = 0.3f;
    public GameObject breadcrumbPrefab;
    public Transform lastCrumb;
    public float crumbDropRate = 0.3f;
    public float curTime = 0f;

    // Update is called once per frame
    void Update()
    {
        float translation = Input.GetAxis("Vertical") * speed;
        float rotation = Input.GetAxis("Horizontal") * rotationSpeed;
        currentSpeed = translation;

        translation *= Time.deltaTime;
        rotation *= Time.deltaTime;

        transform.Translate(0,translation,0);
        
        transform.Rotate(0, 0, -rotation);

        //bread crumbs
        curTime += Time.deltaTime;
        if(curTime>crumbDropRate){
            //drop
            GameObject bc = Instantiate(breadcrumbPrefab,transform.position,Quaternion.identity);
            bc.GetComponent<Breadcrumb>().next = transform;
            if(lastCrumb != null) lastCrumb.gameObject.GetComponent<Breadcrumb>().next = bc.transform;
            lastCrumb = bc.transform;

            curTime = Time.deltaTime - crumbDropRate;
        }
    }

    //predicts where the agent will be
    public Vector3 PredictOffset(){ 
        return transform.up * currentSpeed * predictTime;
    }
}
