using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Barracuda;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils;
using Rect = OpenCVForUnity.CoreModule.Rect;

public class Display : MonoBehaviour
{
    const int RESIZE_IMAGE_SIZE = 256;
    const int CROP_IMAGE_SIZE = 224;
    private Texture2D orgTexture;
    private Texture2D dstTexture;
    Mat originImage = null;
    Mat dstImage = null;
    Rect rectCrop = null;
    Mat croppedImage = null;
    Tensor translateTensor;
    Tensor rotateTensor;

    [SerializeField]
    public Camera _camera;

    [SerializeField]
    public GameObject tiger;

    [SerializeField]
    public GameObject tigerModel;

    [SerializeField]
    public ARSessionOrigin arSessionOrigin;

    [SerializeField]
    private NNModel poseNetModel;

    [SerializeField]
    public Text log;

    [SerializeField]
    public Text guidance;

    [SerializeField]
    public Button buttonLocate;

    [SerializeField]
    public Button buttonHide;
        
    public GameObject _cube;
    
    private Model runtimeModel;
    private IWorker worker;
    private string translateVecLayer;
    private string rotateVecLayer;
    public ARCameraManager cameraManager;
    private Gyroscope _gyro;

    public List<Vector3> historyPosition = new List<Vector3>();
    private int count;  //used for computing the running time
    private bool recordFlag = false;
    private float orientation;
    private float historyOrientation;
    private bool hisEmptyFlag = true;


    void Start()
    {   
        cameraManager = GameObject.Find("AR Camera").GetComponent<ARCameraManager>();

        tiger.SetActive(false);

        runtimeModel = ModelLoader.Load(poseNetModel);
        worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Auto, runtimeModel);
        translateVecLayer = runtimeModel.outputs[0];
        rotateVecLayer = runtimeModel.outputs[1];

        buttonLocate.onClick.AddListener(TaskOnClickLocate);
        buttonHide.onClick.AddListener(TaskOnClickHide);
        Application.lowMemory += OnLowMemory;
        count = 0;

        _gyro = Input.gyro;
        _gyro.enabled = true;
        _cube.GetComponent<Renderer>().enabled = false;
        historyOrientation = 0;
    }

    private void Update() {
        _cube.transform.localRotation = _gyro.attitude;
        _cube.transform.Rotate(0f, 0f, 180f, Space.Self); // Swap "handedness" of quaternion from gyro.
        _cube.transform.Rotate(90f, 180f, 0f, Space.World); // Rotate to make sense as a camera pointing out the back of your device.

        orientation = _cube.transform.eulerAngles.y;
    }

    private void OnLowMemory()
    {
        // release all cached textures
        Resources.UnloadUnusedAssets();
    }

    unsafe private void FixedUpdate() 
    {

        if((recordFlag == true)&&((DataAnalysis.enoughMotion(orientation, historyOrientation))||(hisEmptyFlag == true)))
        {
            guidance.text = "Please keep scanning horizontally.";
            hisEmptyFlag = false;
            historyOrientation = orientation;

            if (!cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
                return;

            var conversionParams = new XRCpuImage.ConversionParams
            {
                // Get the entire image.
                inputRect = new RectInt(0, 0, image.width, image.height),

                outputDimensions = new Vector2Int(image.width, image.height),

                // Choose RGBA format.
                outputFormat = TextureFormat.RGBA32,
                    
                transformation = XRCpuImage.Transformation.MirrorY

            };

            // See how many bytes you need to store the final image.
            int size = image.GetConvertedDataSize(conversionParams);

            // Allocate a buffer to store the image.
            var buffer = new NativeArray<byte>(size, Allocator.Temp);

            // Extract the image data
            image.Convert(conversionParams, new IntPtr(buffer.GetUnsafePtr()), buffer.Length);

            // The image was converted to RGBA32 format and written into the provided buffer
            // so you can dispose of the XRCpuImage. You must do this or it will leak resources.
            image.Dispose();

            // At this point, you can process the image, pass it to a computer vision algorithm, etc.
            // In this example, you apply it to a texture to visualize it.

            // You've got the data; let's put it into a texture so you can visualize it.
            orgTexture = new Texture2D(
                conversionParams.outputDimensions.x,
                conversionParams.outputDimensions.y,
                conversionParams.outputFormat,
                false);

            orgTexture.LoadRawTextureData(buffer);
            orgTexture.Apply();
            
            //orgTexture = Preprocess.rotateTexture(orgTexture,false);
            
            originImage = new Mat(orgTexture.height,orgTexture.width,CvType.CV_8UC3);
            Utils.texture2DToMat(orgTexture,originImage);
            
            dstImage = new Mat();
            //1. resize
            Imgproc.resize(originImage,dstImage, new Size(RESIZE_IMAGE_SIZE,RESIZE_IMAGE_SIZE),0,0,Imgproc.INTER_AREA);

            //2. crop
            rectCrop = new Rect(16, 16, 224, 224);
            croppedImage = new Mat(dstImage, rectCrop);
            
            dstTexture = new Texture2D(CROP_IMAGE_SIZE,CROP_IMAGE_SIZE,TextureFormat.RGBA32,false);
            Utils.matToTexture2D(croppedImage,dstTexture);
            
            dstTexture.Apply();

            buffer.Dispose();

            using Tensor inputTensor = new Tensor(dstTexture, 3);
            worker.Execute(inputTensor);
            //Release GPU resources allocated for the Tensor
            inputTensor.Dispose();
            translateTensor = worker.PeekOutput(translateVecLayer);
            rotateTensor = worker.PeekOutput(rotateVecLayer);
            historyPosition.Add(new Vector3(translateTensor[0], translateTensor[1],translateTensor[2]));

            count += 1;
            
            if(count >= 6)
            {
                tiger.SetActive(true);
                tigerModel.GetComponent<Renderer>().enabled = true;

                //set the current orientation of camera
                setCamera(rotateTensor[0], rotateTensor[1], rotateTensor[2], rotateTensor[3]);

                //pass to the data analyzer to determine which history output from posenet needs to be thrown away
                Vector3 accuratePosition = DataAnalysis.getAccuratePosition(historyPosition);

                //set position using value after analysis
                setObject(tiger, accuratePosition[0], accuratePosition[1], accuratePosition[2]);

                //UI output
                log.text = "Translation Vector: "+ "\n";
                log.text += "("+ accuratePosition[0].ToString() +", "+ accuratePosition[1].ToString()+ ", " + accuratePosition[2].ToString()+ ")"+ "\n";
                log.text += "Rotation Vector: "+ "\n";
                log.text += "("+ rotateTensor[0].ToString()+ ", "+ rotateTensor[1].ToString()+ ", "+ rotateTensor[2].ToString()+ ", "+ rotateTensor[3].ToString()+ ")"+ "\n";
                
                translateTensor.Dispose();
                rotateTensor.Dispose();

                count = 0;
                historyPosition.Clear();
                recordFlag = false;
                guidance.text = "";
            }

        }
    }

    unsafe void TaskOnClickLocate()
    {   
        recordFlag = true;
        hisEmptyFlag = true;

        /*
        //write the image to phone
        var bytes = dstTexture.EncodeToJPG();
        var path = String.Format(Application.persistentDataPath + "/camera_texture{0}.jpg", count);
        File.WriteAllBytes(path, bytes);
        */


    }

    void OnDestroy()
    {
        worker?.Dispose();
    }

    Quaternion normalize(Quaternion quat){
        float twoNorm = (float) Math.Sqrt(quat.w*quat.w+quat.x*quat.x+quat.y*quat.y+quat.z*quat.z);
        Quaternion result = new Quaternion(quat.x/twoNorm, quat.y/twoNorm, quat.z/twoNorm, quat.w/twoNorm);
        return result;
    }

    void setCamera(float qw, float qx, float qy, float qz)
    {
        Quaternion lookRotation = new Quaternion(qx, -qy, qz, qw);
        lookRotation = lookRotation * Quaternion.Inverse(_camera.transform.rotation);
        lookRotation = lookRotation * arSessionOrigin.transform.rotation;
        arSessionOrigin.transform.rotation = lookRotation;
        
        arSessionOrigin.transform.position = new Vector3(arSessionOrigin.transform.position.x-_camera.transform.position.x, arSessionOrigin.transform.position.y-_camera.transform.position.y, arSessionOrigin.transform.position.z-_camera.transform.position.z);
    }

    void setObject(GameObject gameObject, float xcam, float ycam, float zcam)
    {
        float xwx=-4.242127665489746f, xwy=1.459612974610578f, xwz=14.691679135096857f, qw=0.8868265980163835f, qx=-0.1855131299110484f, qy=0.400039234950614f, qz=-0.13817407203025386f;
        Vector3 objPosition = new Vector3(xwx-xcam, -xwy+ycam, xwz-zcam);
        gameObject.transform.position = objPosition;
        Quaternion orientation = new Quaternion(qx, -qy, qz, qw);
        gameObject.transform.rotation = orientation;
    }

    /*
    void OnEnable()
    {
        cameraManager.frameReceived += OnCameraFrameReceived;
    }

    void OnDisable()
    {
        cameraManager.frameReceived -= OnCameraFrameReceived;
    }

    unsafe void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
    }
    */

    void TaskOnClickHide()
    {
        tigerModel.GetComponent<Renderer>().enabled = false;
    }

}
