using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Barracuda;

public class Test : MonoBehaviour
{

    [SerializeField]
    public Text log;

    [SerializeField]
    public Button button;

    [SerializeField]
    public Button buttonTest;

    [SerializeField]
    public Camera _camera;

    [SerializeField]
    public GameObject tiger;

    [SerializeField]
    public ARSessionOrigin arSessionOrigin;

    private string translateVecLayer;
    private string rotateVecLayer;
    
    public ARCameraManager cameraManager;

    private Vector3 origin_position = new Vector3(0.0f, 0.0f, 0.0f);
    private Quaternion origin_rotation = Quaternion.Euler(0,0,0);
    

    void Start()
    {   
        cameraManager = GameObject.Find("AR Camera").GetComponent<ARCameraManager>();
        button.onClick.AddListener(TaskOnClick);
        buttonTest.onClick.AddListener(TaskOnClickTest);
        tiger.SetActive(false);

    }

    void Update() {
        log.text = "Ar Session Origin: "+ "\n";
        log.text += "("+ arSessionOrigin.transform.position.ToString()+ ")"+ "\n";
        log.text += "("+ arSessionOrigin.transform.rotation.ToString()+ ")"+ "\n";
        log.text += "Camera: "+ "\n";
        log.text += "("+ _camera.transform.position.ToString()+ ")"+ "\n";
        log.text += "("+ _camera.transform.rotation.ToString()+ ")"+ "\n";
    }

    void TaskOnClick()
    {   
        tiger.SetActive(true);

        //setCameraCoord(rotateTensor[0], rotateTensor[1], rotateTensor[2], rotateTensor[3]);

        setObjectCoord(tiger, 0.0f, 0.0f, 0.0f, 2.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f);

        arSessionOrigin.transform.position = origin_position;
        arSessionOrigin.transform.rotation = origin_rotation;
        
        //UI output
        //log.text = "Translation Vector: "+ "\n";
        //log.text += "("+ translateTensor[0].ToString() +", "+ translateTensor[1].ToString()+ ", " + translateTensor[2].ToString()+ ")"+ "\n";
        //log.text += "Rotation Vector: "+ "\n";
        //log.text += "("+ rotateTensor[0].ToString()+ ", "+ rotateTensor[1].ToString()+ ", "+ rotateTensor[2].ToString()+ ", "+ rotateTensor[3].ToString()+ ")"+ "\n";

    }

    void TaskOnClickTest()
    {
        arSessionOrigin.transform.position = new Vector3(-_camera.transform.position.x, -_camera.transform.position.y, -_camera.transform.position.z);
        Vector3 camAngles = _camera.transform.rotation.eulerAngles;
        Quaternion q4 = new Quaternion(0.1834517f, 0.01866941f, 0.2822639f, 17.39568f);   //qx, -qy, qz, qw
        q4 = normalize(q4);
        Vector3 v3 = q4.eulerAngles;
        //Quaternion test_rotation = Quaternion.Euler(0,90,0);
        arSessionOrigin.transform.rotation = Quaternion.Euler(-camAngles + q4.eulerAngles);
    }

    Quaternion normalize(Quaternion quat){
        float twoNorm = (float) Math.Sqrt(quat.w*quat.w+quat.x*quat.x+quat.y*quat.y+quat.z*quat.z);
        Quaternion result = new Quaternion(quat.x/twoNorm, quat.y/twoNorm, quat.z/twoNorm, quat.w/twoNorm);
        return result;
    }

    void setCameraCoord(float qw, float qx, float qy, float qz)
    {
        Quaternion orientation = new Quaternion(qx, -qy, qz, qw);
        orientation = normalize(orientation);
        arSessionOrigin.transform.rotation = orientation;
    }

    void setObjectCoord(GameObject gameObject, float xcam,float ycam, float zcam, float xwx, float xwy, float xwz, float qw, float qx, float qy, float qz)
    {
        Vector3 objPosition = new Vector3(xwx-xcam, -xwy+ycam, xwz-zcam);
        gameObject.transform.position = objPosition;
        Quaternion orientation = new Quaternion(qx, -qy, qz, qw);
        gameObject.transform.rotation = orientation;
    }

}
