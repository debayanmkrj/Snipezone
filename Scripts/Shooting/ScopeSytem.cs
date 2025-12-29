using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Rendering;

public class ScopeSytem : MonoBehaviour
{
    public Animator playerAnimator;
    public GameObject scopeImage;

    public float zoomSpeed = 2f;

    public Camera unscopedCamera;
    public Camera scopeCamera;

    public bool isScoped = false;
    private float originalFOV;
    private bool wasShooting = false;

    void Start()
    {
        if (scopeCamera != null)
        {
            originalFOV = scopeCamera.fieldOfView;
        }
    }

    void Update()
    {
        HandleScopeToggle();
        
        // Check if currently shooting
        if (playerAnimator != null && playerAnimator.GetCurrentAnimatorStateInfo(0).IsName("Shoot"))
        {
            if (!wasShooting)
            {
                // Just started shooting - force unscope
                ForceUnscope();
                wasShooting = true;
            }
        }
        else
        {
            wasShooting = false;
        }
        
        // Force unscope during reload
        if (playerAnimator != null && playerAnimator.GetCurrentAnimatorStateInfo(0).IsName("Reload"))
        {
            ForceUnscope();
        }
    }

    private void HandleScopeToggle()
    {
        if (Input.GetMouseButtonDown(1))
        {
            isScoped = !isScoped;
            scopeImage.SetActive(isScoped);

            if (isScoped)
            {
                scopeCamera.enabled = true;
                unscopedCamera.enabled = false;
                scopeCamera.fieldOfView = originalFOV / zoomSpeed;
            }
            else
            {
                scopeCamera.enabled = false;
                unscopedCamera.enabled = true;
                scopeCamera.fieldOfView = originalFOV;
            }
        }

        if (isScoped)
        {
            float zoom = Input.GetAxis("Mouse ScrollWheel");
            scopeCamera.fieldOfView -= zoom * zoomSpeed * 10f;
            scopeCamera.fieldOfView = Mathf.Clamp(scopeCamera.fieldOfView, 10f, 80f);
        }
    }

    public void ForceUnscope()
    {
        isScoped = false;
        scopeImage.SetActive(false);
        scopeCamera.enabled = false;
        unscopedCamera.enabled = true;
        if (scopeCamera != null)
        {
            scopeCamera.fieldOfView = originalFOV;
        }
    }
}