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
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.UtilsModule;
using System.Diagnostics;

public class Display : MonoBehaviour
{
    const int IMAGE_SIZE = 224;
    private Texture2D orgTexture;
    private Texture2D dstTexture;
    
    [SerializeField]
    public Text log;

    [SerializeField]
    public Button button;

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
    public Button buttonHide;
    
    private Model runtimeModel;
    private IWorker worker;
    private string translateVecLayer;
    private string rotateVecLayer;
    public ARCameraManager cameraManager;

    public int count;

    void Start()
    {   
        cameraManager = GameObject.Find("AR Camera").GetComponent<ARCameraManager>();
        button.onClick.AddListener(TaskOnClick);
        buttonHide.onClick.AddListener(TaskOnClickHide);

        tiger.SetActive(false);

        runtimeModel = ModelLoader.Load(poseNetModel);
        worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Auto, runtimeModel);
        translateVecLayer = runtimeModel.outputs[0];
        rotateVecLayer = runtimeModel.outputs[1];

        count = 0;
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
    */

    unsafe void TaskOnClick()
    {   
        /*
        // create and start a Stopwatch instance
        Stopwatch stopwatchAll = Stopwatch.StartNew();
        stopwatchAll.Start();
        Stopwatch stopwatch = Stopwatch.StartNew(); 
        stopwatch.Start();
        */

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

        /*
        stopwatch.Stop();
        long time1 = stopwatch.ElapsedMilliseconds;
        stopwatch.Restart();
        */

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
            
        // orgTexture = rotateTexture(orgTexture,false);
           
        Mat originImage = new Mat(orgTexture.height,orgTexture.width,CvType.CV_8UC3);
        Utils.texture2DToMat(orgTexture,originImage);
        Mat dstImage = new Mat();
        Imgproc.resize(originImage,dstImage, new Size(IMAGE_SIZE,IMAGE_SIZE),0,0,Imgproc.INTER_AREA);
        dstTexture = new Texture2D(IMAGE_SIZE,IMAGE_SIZE,TextureFormat.RGBA32,false);
        Utils.matToTexture2D(dstImage,dstTexture);
        dstTexture.Apply();

        buffer.Dispose();

        /*
        stopwatch.Stop();
        long time2 = stopwatch.ElapsedMilliseconds;
        stopwatch.Restart();
        */

        tiger.SetActive(true);
        tigerModel.GetComponent<Renderer>().enabled = true;

        /*
        Texture2D tex = (Texture2D)Resources.Load("camera_texture2");
        Mat originImage = new Mat(tex.height,tex.width,CvType.CV_8UC3);
        Utils.texture2DToMat(tex,originImage);
        Mat dstImage = new Mat();
        Imgproc.resize(originImage,dstImage, new Size(IMAGE_SIZE,IMAGE_SIZE),0,0,Imgproc.INTER_AREA);
        Texture2D dstTexture = new Texture2D(IMAGE_SIZE,IMAGE_SIZE,TextureFormat.RGBA32,false);
        Utils.matToTexture2D(dstImage,dstTexture);
        //tex = Preprocess.ScaleTexture(tex, 224, 224);
        //tex.Resize(224, 224, TextureFormat.RGBA32, true);
        dstTexture.Apply();
        */

        //EditorUtility.CompressTexture(tex, TextureFormat.RGBA32, TextureCompressionQuality.Best);
        //tex.Compress(true);

        using Tensor inputTensor = new Tensor(dstTexture, 3);
        worker.Execute(inputTensor);
        Tensor translateTensor = worker.PeekOutput(translateVecLayer);
        Tensor rotateTensor = worker.PeekOutput(rotateVecLayer);

        setCameraCoord(rotateTensor[0], rotateTensor[1], rotateTensor[2], rotateTensor[3]);

        setObjectCoord(tiger, translateTensor[0], translateTensor[1], translateTensor[2], 11.02863615000455f, 2.240555382307285f, 11.948401630064142f, 0.9828390530260043f, -0.17040718441657982f, -0.06918005146429226f, 0.014244571792747083f);
        
        /*
        stopwatch.Stop();
        long time3 = stopwatch.ElapsedMilliseconds;

        stopwatchAll.Stop();
        long time4 = stopwatchAll.ElapsedMilliseconds;
        */

        //UI output
        log.text = "Translation Vector: "+ "\n";
        log.text += "("+ translateTensor[0].ToString() +", "+ translateTensor[1].ToString()+ ", " + translateTensor[2].ToString()+ ")"+ "\n";
        log.text += "Rotation Vector: "+ "\n";
        log.text += "("+ rotateTensor[0].ToString()+ ", "+ rotateTensor[1].ToString()+ ", "+ rotateTensor[2].ToString()+ ", "+ rotateTensor[3].ToString()+ ")"+ "\n";

        /*
        string filePath = Application.persistentDataPath + "/PoseNet.txt";
        if (!File.Exists(filePath))
        {
            using (StreamWriter sw = File.CreateText (filePath))
            {
                sw.WriteLine("Computation time of MobileNet (ms):");
            }
        }

        using (StreamWriter sr = File.AppendText(filePath))
        {
            sr.WriteLine ("Test Number {0}:", count);
            sr.WriteLine ("Time1: {0}, Time2: {1}, Time3: {2}, Total time: {3}", time1, time2, time3, time4);
        }
        count = count+1;
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

    void setCameraCoord(float qw, float qx, float qy, float qz)
    {
        //arSessionOrigin.transform.rotation = new Quaternion(0,0,0,0);

        Quaternion lookRotation = new Quaternion(qx, -qy, qz, qw);
        lookRotation = lookRotation * Quaternion.Inverse(_camera.transform.rotation);
        lookRotation = lookRotation * arSessionOrigin.transform.rotation;
        arSessionOrigin.transform.rotation = lookRotation;
        
        arSessionOrigin.transform.position = new Vector3(arSessionOrigin.transform.position.x-_camera.transform.position.x, arSessionOrigin.transform.position.y-_camera.transform.position.y, arSessionOrigin.transform.position.z-_camera.transform.position.z);
    }

    void setObjectCoord(GameObject gameObject, float xcam,float ycam, float zcam, float xwx, float xwy, float xwz, float qw, float qx, float qy, float qz)
    {
        Vector3 objPosition = new Vector3(xwx-xcam, -xwy+ycam, xwz-zcam);
        gameObject.transform.position = objPosition;
        Quaternion orientation = new Quaternion(qx, -qy, qz, qw);
        gameObject.transform.rotation = orientation;
    }

    /*
    unsafe void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
    }
    */

     Texture2D rotateTexture(Texture2D originalTexture, bool clockwise)
     {
         Color32[] original = originalTexture.GetPixels32();
         Color32[] rotated = new Color32[original.Length];
         int w = originalTexture.width;
         int h = originalTexture.height;
 
         int iRotated, iOriginal;
 
         for (int j = 0; j < h; ++j)
         {
             for (int i = 0; i < w; ++i)
             {
                 iRotated = (i + 1) * h - j - 1;
                 iOriginal = clockwise ? original.Length - 1 - (j * w + i) : j * w + i;
                 rotated[iRotated] = original[iOriginal];
             }
         }
 
         Texture2D rotatedTexture = new Texture2D(h, w);
         rotatedTexture.SetPixels32(rotated);
         rotatedTexture.Apply();
         return rotatedTexture;
     }

     void TaskOnClickHide()
     {
        tigerModel.GetComponent<Renderer>().enabled = false;
     }
}
