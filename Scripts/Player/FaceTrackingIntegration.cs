using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System;

[System.Serializable]
public class FaceData
{
    public float rotationX;
    public float rotationY;
    public bool shoot;
}

public class FaceTrackingIntegration : MonoBehaviour
{
    [Header("Face Tracking Settings")]
    public int udpPort = 12345;
    public float sensitivity = 0.1f;
    public float smoothing = 5f;
    
    [Header("References")]
    public CameraController cameraController;
    public ShootingController shootingController;
    
    [Header("Control Mode")]
    public bool useFaceTracking = true;
    
    // Networking
    private UdpClient udpClient;
    private Thread udpThread;
    private bool shouldStop = false;
    
    // Thread-safe data
    private float targetMouseX = 0f;
    private float targetMouseY = 0f;
    private bool shouldShoot = false;
    
    // Face tracking data
    public float faceMouseX = 0f;
    public float faceMouseY = 0f;
    
    void Start()
    {
        if (cameraController == null)
            cameraController = FindFirstObjectByType<CameraController>();
        
        if (shootingController == null)
            shootingController = FindFirstObjectByType<ShootingController>();
        
        Debug.Log($"Camera Controller: {cameraController?.gameObject.name}");
        Debug.Log($"Shooting Controller: {shootingController?.gameObject.name}");
        
        StartUDPListener();
    }
    
    void StartUDPListener()
    {
        try
        {
            udpClient = new UdpClient(udpPort);
            udpThread = new Thread(UDPListenerThread);
            udpThread.IsBackground = true;
            udpThread.Start();
            Debug.Log($"Face tracking started on port {udpPort}");
        }
        catch (Exception e)
        {
            Debug.LogError($"UDP error: {e.Message}");
        }
    }
    
    void UDPListenerThread()
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        
        while (!shouldStop)
        {
            try
            {
                byte[] data = udpClient.Receive(ref remoteEndPoint);
                string jsonString = Encoding.UTF8.GetString(data);
                
                FaceData faceData = JsonUtility.FromJson<FaceData>(jsonString);
                
                targetMouseX = faceData.rotationX * sensitivity;
                targetMouseY = faceData.rotationY * sensitivity;
                
                if (faceData.shoot)
                {
                    shouldShoot = true;
                }
            }
            catch (Exception)
            {
                if (!shouldStop)
                {
                    // continue listening unless we are stopping
                }
            }
        }
    }
    
    void Update()
    {
        // Apply smoothing
        faceMouseX = Mathf.Lerp(faceMouseX, targetMouseX, Time.deltaTime * smoothing);
        faceMouseY = Mathf.Lerp(faceMouseY, targetMouseY, Time.deltaTime * smoothing);
        
        // Handle shooting 
        if (shouldShoot && shootingController != null)
        {
            Debug.Log("Blink detected - shooting!");
            shootingController.TriggerFaceShoot();
            shouldShoot = false; // Reset flag
        }
    }
    
    void OnDestroy()
    {
        shouldStop = true;
        if (udpThread != null) udpThread.Join(1000);
        if (udpClient != null) udpClient.Close();
    }
    
    void OnApplicationQuit()
    {
        shouldStop = true;
        if (udpThread != null) udpThread.Join(1000);
        if (udpClient != null) udpClient.Close();
    }
}