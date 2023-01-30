using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class Agent : MonoBehaviour
{
    public enum States {Pursue, Evade, Wander, PathFollow, Flocking}
    public enum AvoidanceModes {Raycast, ConeCheck, CollisionPrediction, None}
    public Transform target;
    public Player player;
    public Agent agent;
    public States state;
    public AvoidanceModes avoidMode;

    [Header("Senses")]
    public float predictTime = 0.3f;
    public Vector3 predictOffset;

    [Header("Movement Settings")]
    public float maxSpeed = 5f;
    public float moveSpeed = 0f;
    public float acceleration = 0.5f;
    public float maxAngularSpeed = 10f;
    public float angularSpeed = 0f;
    public float angularAccel = 1f;
    public float defaultAngularSpeed = 30f;
    public float arriveRange = 1f;
    public float arrivalDamp = 2f;
    public float arrivalSpeed = 0.5f;
    public float reachedRange = 0.2f;

    [Header("Wander")]
    public Transform wanderTarget;
    private Vector3 jitterOffset;

    public float wanderROM = 1.5f;
    public float wanderWF = 1f;

    public float pursueROM = 2f;
    public float pursueWF = 2f;

    [Header("Path Following")]
    public string pathTag = "Breadcrumb";
    public float pathReachDistance = 0.5f;
    public Transform next;
    public Transform closest;
    public Transform chosen;

    [Header("Visualization")]
    public Transform visualizer;
    public Text stateText;
    public Transform arriveViz;

    [Header("Flocking")]
    public FlockManager myFlock;
    public float flockDist = 5f;
    public float flockAvoid = 1f;

    [Header("Obstacle Avoidance")]
    public GameObject[] walls;
    public string wallTag = "Wall";
    public float wallThreshold;
    public List<GameObject> wallsInRange;
    public int staticWalls;
    public int movingWalls;

    //[Header("Obstacle Avoidance")]
    [Header("Obstacle Avoidance: Raycast")]
    public float sightDist = 1f;
    public float sideRayAngle = 45f;
    public float avoidDist = 1f;
    public Color rayColor;
    public Transform rayStart;
    public LayerMask avoidLayers;

    [Header("Obstacle Avoidance: Cone Check")]
    public float coneDist = 1.5f;
    [SerializeField]
    public float coneThreshold{
        get{return Mathf.Cos(coneAngle);}
        set{coneAngle = Mathf.Acos(value);}
    }
    public float coneAngle = 15f;
    public string obstacleManagerTag = "ObstacleManager";
    public ObstacleManager om;
    public List<GameObject> obstacles;
    public List<GameObject> obstInCone;

    [Header("Obstacle Avoidance: Collision Prediction")]
    public float predictDist = 1.5f;
    public List<Vector3> predictDistances;


    // Start is called before the first frame update
    void Start()
    {
        angularSpeed = defaultAngularSpeed;
        jitterOffset = Vector3.zero;

        agent = target.gameObject.GetComponent<Agent>();
        player = target.gameObject.GetComponent<Player>();
        arriveViz.localScale = new Vector3(2*arriveRange,2*arriveRange,2*arriveRange);

        next = target;

        //flock
        myFlock = transform.parent.gameObject.GetComponent<FlockManager>();
        gameObject.tag = myFlock.flockTag;
        myFlock.RefreshFlock();

        //cone check
        om = GameObject.FindGameObjectsWithTag(obstacleManagerTag)[0].GetComponent<ObstacleManager>();

        //obst avoidance
        walls = GameObject.FindGameObjectsWithTag(wallTag);
        wallThreshold = Mathf.Max(predictDist, sightDist, coneDist);
    }


    // Update is called once per frame
    void Update()
    {   
        //ChangeMode();
        switch(state){
            case States.Pursue:
                if(!CollisionDetection()){
                    //Jitter(pursueROM, pursueWF);
                    CalculatePredictOffset();
                    Vector3 posi = target.position + predictOffset;
                    
                    Seek(posi);
                }
                break;
            case States.Evade:
                if(!CollisionDetection()){
                    //Jitter(pursueROM, pursueWF);
                    CalculatePredictOffset();
                    Vector3 posu = target.position + predictOffset;
                    visualizer.position = posu;
                    arriveViz.position = posu;
                    Evade(posu);
                }
                break;
            case States.Wander:
                if(!CollisionDetection()){
                    arriveViz.position = target.position;
                    Wander();
                }
                break;
            case States.PathFollow:
                if(!CollisionDetection()){
                    PathFollow();
                }
                break;
            case States.Flocking:
                if(!CollisionDetection()){
                    Flocking();
                }
                break;
        }

    }

    public void Flocking(){
        Vector3 vcentre = Vector3.zero;
        Vector3 vavoid = Vector3.zero;
        float gSpeed = 0.01f;
        float curDist;

        List<GameObject> neighbors = new List<GameObject>();
        foreach (GameObject nbr in myFlock.flock){
            if(nbr == gameObject) continue; //skip if same object
            curDist = Vector3.Distance(transform.position,nbr.transform.position);
            if(curDist<=flockDist){
                Agent ag = nbr.GetComponent<Agent>();
                if(ag == null) continue;
                gSpeed += ag.moveSpeed;
                neighbors.Add(nbr);
                vcentre += nbr.transform.position;

                if(curDist<avoidDist){
                    vavoid += transform.position - nbr.transform.position;
                }
            }
        }

        if(neighbors.Count > 0){
            vcentre = (vcentre/neighbors.Count) + (myFlock.target.position-transform.position);
            moveSpeed = gSpeed / neighbors.Count;

            Vector3 direction = (vcentre + vavoid);
            if(direction != Vector3.zero){
                TurnTo(direction);
                Move();
                visualizer.position = direction;
                arriveViz.position = direction;
            }
        }
        else{
            CalculatePredictOffset();
            Vector3 posi = myFlock.target.position + predictOffset;
            Seek(posi);
        }
        

    }

    public void CalculatePredictOffset(){
        predictOffset = Vector3.zero;
        if(player!=null){
            predictOffset = player.PredictOffset();
        }
        else if (agent!=null){
            predictOffset = agent.PredictOffset();
        }
    }

    public void DecideCollisionMode(){
        //Decide based on how many of each walls are close
        wallsInRange = ObjectsInRange(wallThreshold, walls);
        staticWalls = 0;
        movingWalls = 0;
        foreach (GameObject w in wallsInRange){
            if(w.GetComponent<Moving>()!= null) movingWalls += 1;
            else staticWalls += 1;
        }

        //now decide
        if(movingWalls == 0 && staticWalls>0){
            avoidMode = AvoidanceModes.Raycast;
            SetText("Raycast");
        }
        else if(movingWalls == staticWalls){
            avoidMode = AvoidanceModes.ConeCheck;
            SetText("Cone Check");
        }
        else if(movingWalls > staticWalls){
            avoidMode = AvoidanceModes.CollisionPrediction;
            SetText("Collision Prediction");
        }
    }

    //returns true if collision is detected
    bool CollisionDetection(){
        DecideCollisionMode();
        bool output = false;
        switch(avoidMode){
            case AvoidanceModes.Raycast:
                output = AvoidRaycast();
                break;
            case AvoidanceModes.ConeCheck:
                output = AvoidConeCheck();
                break;
            case AvoidanceModes.CollisionPrediction:
                output = AvoidCollisionPrediction();
                break;
        }
        return output;
    }

    public bool AvoidCollisionPrediction(){
        obstacles = ObjectsInRange(predictDist,om.obstacles);
        predictDistances = PredictDistances(obstacles, predictTime);
        if(predictDistances.Count == 0) return false;
        Vector3 avoidPos = Closest(PositionInTime(predictTime),predictDistances);
        visualizer.position = avoidPos;
        CalculatePredictOffset();
        arriveViz.position = target.position + predictOffset;
        Evade(avoidPos);
        return true;
    }

    public Vector3 Closest(Vector3 pos, List<Vector3> other){
        Vector3 output = pos;
        float minDist = float.MaxValue;
        foreach (Vector3 cur in other){
            float curDist = Vector3.Distance(pos,cur);
            if(curDist<minDist){
                output = cur;
                minDist = curDist;
            }
        }
        return output;
    }

    public List<Vector3> PredictDistances(List<GameObject> objs, float time){
        List<Vector3> output = new List<Vector3>();
        foreach (GameObject obj in objs)
        {
            ObstacleIndicator oi = obj.GetComponent<ObstacleIndicator>();
            if(oi!=null){
                output.Add(oi.PositionInTime(time));
            }
        }
        return output;
    }

    public Vector3 PositionInTime(float time){
        return transform.position + (transform.up * moveSpeed * time);
    }

    public bool AvoidConeCheck(){
        obstacles = ObjectsInRange(coneDist,om.obstacles);
        obstInCone = new List<GameObject>();
        foreach (GameObject obst in obstacles){
            if(InCone(obst)){
                obstInCone.Add(obst);
            }
        }
        if(obstInCone.Count == 0) return false;
        Vector3 avgPos = AveragePosition(obstInCone);
        visualizer.position = avgPos;
        CalculatePredictOffset();
        arriveViz.position = target.position + predictOffset;
        Evade(avgPos);
        return true;
    }

    //gets average position of all objects in list
    public Vector3 AveragePosition(List<GameObject> objs){
        if (objs.Count == 0){
            return Vector3.zero;
        }
        Vector3 sum = Vector3.zero;
        for(int i = 0;i<objs.Count;i++){
            sum += objs[i].transform.position;
        }
        return sum/objs.Count;
    }

    //returns true if object is in Agent's cone
    public bool InCone(GameObject obj){
        return Vector3.Angle(transform.up, obj.transform.position-transform.position) < coneAngle;
    }   

    //returns true List of all objects in range from an array of objects
    public List<GameObject> ObjectsInRange(float range, GameObject[] objs){
        List<GameObject> output = new List<GameObject>();
        foreach (GameObject obj in objs){
            if(Vector3.Distance(transform.position,obj.transform.position)<=range){
                output.Add(obj);
            }
        }
        return output;
    }

    //returns true if collision is detected
    public bool AvoidRaycast(){
        RaycastHit2D hit = Physics2D.Raycast(rayStart.position, transform.up, sightDist, avoidLayers);
        Debug.DrawRay(rayStart.position, transform.up, rayColor);
        if(hit.collider!=null){
            //Seek(collision.position + collision.normal * avoidDist)
            Seek(hit.point + hit.normal * avoidDist);
            return true;
        }
        //side vectors
        Vector2 cur = transform.right;
        hit = Physics2D.Raycast(rayStart.position, cur, sightDist, avoidLayers);
        Debug.DrawRay(rayStart.position, cur, rayColor);
        if(hit.collider!=null){
            //Seek(collision.position + collision.normal * avoidDist)
            Seek(hit.point + hit.normal * avoidDist);
            return true;
        }
        cur = -transform.right;
        hit = Physics2D.Raycast(rayStart.position, cur, sightDist, avoidLayers);
        Debug.DrawRay(rayStart.position, cur, rayColor);
        if(hit.collider!=null){
            //Seek(collision.position + collision.normal * avoidDist)
            Seek(hit.point + hit.normal * avoidDist);
            return true;
        }   
        return false;
    }

    void SetText(string s){
        if(stateText!=null) stateText.text = s;
    }

    void PathFollow(){

        chosen = target;

        //decide chosen between closest and next
        //Get closest breadcrumb
        Transform lastClosest = closest;
        closest = target;
        float minDist = float.MaxValue;
        GameObject[] crumbs = GameObject.FindGameObjectsWithTag(pathTag);
        foreach (GameObject crumb in crumbs){
            float dist = Vector3.Distance(transform.position, crumb.transform.position);
            //if(dist>sightDistance) return; //remove options that are too far away
            float curAge = crumb.GetComponent<Breadcrumb>().age;
            if(dist<minDist){
                closest = crumb.transform;
                minDist = dist;
            }
        }
        //dont pick an older closest
        if(lastClosest!=null && lastClosest != target){
            if(lastClosest.gameObject.GetComponent<Breadcrumb>().age<closest.gameObject.GetComponent<Breadcrumb>().age){
                closest = lastClosest;
            }
        }

        float nextAge = float.MaxValue;
        float minAge = Vector3.Distance(transform.position, closest.position);
        if(next != null && next!=target) nextAge = next.gameObject.GetComponent<Breadcrumb>().age;

        if(next == null && closest == null){
            //nothing
        }
        else if(nextAge<minAge){
            chosen = next;
        }
        else if(minAge<nextAge){
            chosen = closest;
        }
        else{
            chosen = target;
        }

        visualizer.position = chosen.position;
        arriveViz.position = target.position;
        TurnTo(chosen.position);

        float distFromChosen = Vector3.Distance(transform.position, chosen.transform.position);
        if(chosen.transform == target){ //if target is player, stop here
            if(distFromChosen<=reachedRange){
                moveSpeed = 0;
            }
            else if(distFromChosen<=arriveRange){
                moveSpeed = maxSpeed * (distFromChosen/arriveRange);
            }
            else{
                moveSpeed += acceleration;
                moveSpeed = Mathf.Min(moveSpeed,maxSpeed);
            }
        }
        else{ 
            if(distFromChosen<=pathReachDistance){
                next = chosen.GetComponent<Breadcrumb>().next;
            }
            else{
                moveSpeed += acceleration;
                moveSpeed = Mathf.Min(moveSpeed,maxSpeed);
            }
        }

        transform.Translate(0,moveSpeed * Time.deltaTime,0);
    }

    void Wander(){
        Jitter(wanderROM, wanderWF);
        Seek(wanderTarget.position + jitterOffset);
    }

    void Jitter(float rangeOfMotion, float wanderFluctuation){
        if(RandomBoolean()){
            jitterOffset.x = Mathf.Clamp(jitterOffset.x+wanderFluctuation*Time.deltaTime, -rangeOfMotion, rangeOfMotion);
        }
        else{
            jitterOffset.x = Mathf.Clamp(jitterOffset.x-wanderFluctuation*Time.deltaTime, -rangeOfMotion, rangeOfMotion);
        }
        if(RandomBoolean()){
            jitterOffset.y = Mathf.Clamp(jitterOffset.y+wanderFluctuation*Time.deltaTime, -rangeOfMotion, rangeOfMotion);
        }
        else{
            jitterOffset.y = Mathf.Clamp(jitterOffset.y-wanderFluctuation*Time.deltaTime, -rangeOfMotion, rangeOfMotion);
        }
        visualizer.position = wanderTarget.position + jitterOffset;
    }

    public Vector3 PredictOffset(){
        return transform.forward * -moveSpeed * predictTime;
    }

    void TurnTo(Vector3 pos){
        //Turn
        Vector3 diff = pos - transform.position;
        float targetRot = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg - 90;
        angularSpeed += angularAccel * Time.deltaTime;
        angularSpeed = Mathf.Min(angularSpeed,maxAngularSpeed);
        //Mathf.SmoothDamp(transform.rotation.z, targetRot, ref angularSpeed, turnTime, maxAngularSpeed)
        Quaternion look = Quaternion.Euler(0f, 0f, targetRot);

        if(transform.rotation==look){
            angularSpeed = defaultAngularSpeed;
        }
        else{
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, Time.deltaTime * angularSpeed);
        }
    }

    Quaternion TurnVector(Vector3 pos){
        //Turn
        Vector3 diff = pos - transform.position;
        float targetRot = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg - 90;
        angularSpeed += angularAccel * Time.deltaTime;
        angularSpeed = Mathf.Min(angularSpeed,maxAngularSpeed);
        //Mathf.SmoothDamp(transform.rotation.z, targetRot, ref angularSpeed, turnTime, maxAngularSpeed)
        Quaternion look = Quaternion.Euler(0f, 0f, targetRot);

        if(transform.rotation==look){
            angularSpeed = defaultAngularSpeed;
            
        }
        return look;
    }

    void Seek(Vector3 pos){
        TurnTo(pos);
        //Move
        //Dynamic Arrive:
        MoveTo(pos);
    }

    void Move(){
        moveSpeed += acceleration;
        moveSpeed = Mathf.Min(moveSpeed,maxSpeed);
        
        transform.Translate(0,moveSpeed * Time.deltaTime,0);
    }

    void MoveTo(Vector3 pos){
        float dist = Vector3.Distance(transform.position, pos);
        if(dist<=reachedRange){
            if(closest!=null){

            }
            moveSpeed = 0;
        }
        else if(dist<=arriveRange){
            moveSpeed = maxSpeed * (dist/arriveRange);
        }
        else{
            moveSpeed += acceleration;
            moveSpeed = Mathf.Min(moveSpeed,maxSpeed);
        }
        transform.Translate(0,moveSpeed * Time.deltaTime,0);

        //visualize
        visualizer.position = pos;
        arriveViz.position = pos;
    }

    void Evade(Vector3 pos){
        //Turn
        Vector3 diff = transform.position-pos;
        float targetRot = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg - 90;
        angularSpeed += angularAccel * Time.deltaTime;
        angularSpeed = Mathf.Min(angularSpeed,maxAngularSpeed);
        //Mathf.SmoothDamp(transform.rotation.z, targetRot, ref angularSpeed, turnTime, maxAngularSpeed)
        Quaternion look = Quaternion.Euler(0f, 0f, targetRot);
        if(transform.rotation==look){
            angularSpeed = defaultAngularSpeed;
        }
        else{
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, Time.deltaTime * angularSpeed);
        }

        //move
        moveSpeed += acceleration;
        moveSpeed = Mathf.Min(moveSpeed,maxSpeed);
        transform.Translate(0,moveSpeed * Time.deltaTime,0);
    }

    //Utility
    public bool RandomBoolean(){
        if (Random.value >= 0.5)
        {
            return true;
        }
        return false;
    }

        public void StartPursue(){
        state = States.Pursue;
        SetText("Pursue");
    }

    public void StartEvade(){
        state = States.Evade;
        SetText("Evade");
    }

    public void StartWander(){
        state = States.Wander;
        SetText("Wander");
    }

    public void StartPathFollow(){
        state = States.PathFollow;
        SetText("Path Follow");
    }

    public void ChangeMode(){
        if(Input.GetKeyDown(KeyCode.U)){
            StartPursue();            
        }
        if(Input.GetKeyDown(KeyCode.I)){
            StartEvade();
        }
        if(Input.GetKeyDown(KeyCode.O)){
            StartWander();
        }
        if(Input.GetKeyDown(KeyCode.P)){
            StartPathFollow();
        }
    }
}
