using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Breadcrumb : MonoBehaviour
{

    public float age = 0;
    public Transform next;
    public float maxAge = 5;
    public bool immortal = false;

    [Header("Visualization")]
    public SpriteRenderer sp;
    public Color col;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(immortal) return;
        age += Time.deltaTime;
        if(age>maxAge){
            Destroy(gameObject);
        }

        sp.color = new Color(col.r,col.g,col.b,1-age/maxAge);
    }
}
