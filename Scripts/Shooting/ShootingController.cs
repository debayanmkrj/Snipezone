using UnityEngine;

public class ShootingController : MonoBehaviour
{
    [Header("Weapon Effects")]

    public ParticleSystem muzzleFlash;
    public Animator animator;
    public Transform firePoint;
    public float fireRate = 0.8f;
    public float fireRange = 100f;
    private float nextFireTime = 0f;

    private bool isPlayingShootAnimation = false;

    public int maxAmmo = 20;

    public int fireDamage = 2;

    private int currentAmmo;

    public float reloadTime = 1.5f;
    private bool isReloading = false;

    [Header("Sound Effects")]
    public AudioSource shootAudioSource;
    public AudioClip shootClip;
    public AudioClip reloadClip;

    [Header("Face Tracking")]
    public bool useFaceTracking = true;
    private bool faceShootTriggered = false;

    public ScopeSytem scopeSystem;

    void Start()
    {
        currentAmmo = maxAmmo;

        if (muzzleFlash != null)
        {
            muzzleFlash.Stop();
        }
    }

    void Update()
    {
        if (isReloading)
            return;
        
        // Check input method
        FaceTrackingIntegration faceTracker = FindFirstObjectByType<FaceTrackingIntegration>();
        bool usingFaceTracking = faceTracker != null && faceTracker.useFaceTracking;
        
        // Shooting input - mouse click when not using face tracking
        if (!usingFaceTracking && Input.GetButton("Fire1") && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + (1f / fireRate);
            Shoot();
        }

        if (Input.GetKeyDown(KeyCode.R) && currentAmmo < maxAmmo)
        {
            Reload();
        }
    }

    void Shoot()
    {
        if (currentAmmo > 0)
        {
            RaycastHit hit;
            bool hitSomething = Physics.Raycast(firePoint.position, firePoint.forward, out hit, fireRange);
            
            bool hitEnemy = false;
            
            if (hitSomething)
            {
                Debug.Log(hit.transform.name);

                MoveEnemyAssault enemyAssault = hit.transform.GetComponent<MoveEnemyAssault>();
                MoveEnemySnipper enemySnipper = hit.transform.GetComponent<MoveEnemySnipper>();
                
                if (enemyAssault != null)
                {
                    enemyAssault.TakeDamage(fireDamage);
                    Debug.Log("Hit enemy! Dealing damage");
                    hitEnemy = true;
                }
                else if (enemySnipper != null)
                {
                    enemySnipper.TakeDamage(fireDamage);
                    Debug.Log("Hit enemy! Dealing damage");
                    hitEnemy = true;
                }
                else
                {
                    enemyAssault = hit.transform.GetComponentInParent<MoveEnemyAssault>();
                    enemySnipper = hit.transform.GetComponentInParent<MoveEnemySnipper>();
                    
                    if (enemyAssault != null)
                    {
                        enemyAssault.TakeDamage(fireDamage);
                        Debug.Log("Hit enemy (parent)! Dealing damage");
                        hitEnemy = true;
                    }
                    else if (enemySnipper != null)
                    {
                        enemySnipper.TakeDamage(fireDamage);
                        Debug.Log("Hit enemy (parent)! Dealing damage");
                        hitEnemy = true;
                    }
                }
            }
            
            // Check for near misses if we didn't hit an enemy directly
            if (!hitEnemy)
            {
                RaycastHit[] nearbyHits = Physics.SphereCastAll(firePoint.position, 2f, firePoint.forward, fireRange);
                foreach (RaycastHit nearHit in nearbyHits)
                {
                    MoveEnemyAssault enemyAssault = nearHit.transform.GetComponent<MoveEnemyAssault>();
                    if (enemyAssault == null)
                        enemyAssault = nearHit.transform.GetComponentInParent<MoveEnemyAssault>();
                        
                    if (enemyAssault != null)
                    {
                        enemyAssault.OnNearMiss(nearHit.point);
                        continue;
                    }
                    
                    MoveEnemySnipper enemySnipper = nearHit.transform.GetComponent<MoveEnemySnipper>();
                    if (enemySnipper == null)
                        enemySnipper = nearHit.transform.GetComponentInParent<MoveEnemySnipper>();
                        
                    if (enemySnipper != null)
                    {
                        enemySnipper.OnNearMiss(nearHit.point);
                    }
                }
            }

            if (muzzleFlash != null && !scopeSystem.isScoped)
            {
                muzzleFlash.Play();
            }

            if (!animator.GetBool("Shoot"))
            {
                animator.SetBool("Shoot", true);
                Invoke("ResetShootAnimation", 1.2f);
            }

            currentAmmo--;
            shootAudioSource.PlayOneShot(shootClip);
        }
        else
        {
            Reload();
        }
         AlertNearbyEnemies(1.0f);
    }

    void ResetShootAnimation()
    {
        animator.SetBool("Shoot", false);
        isPlayingShootAnimation = false;
    }

    void Reload()
    {
        if (!isReloading && currentAmmo < maxAmmo)
        {
            if (scopeSystem != null)
            {
                scopeSystem.ForceUnscope();
            }
            animator.SetTrigger("Reload");
            isReloading = true;
            //reload sound
            shootAudioSource.PlayOneShot(reloadClip);
            Invoke("FinishReloading", reloadTime);

        }
        
    }

    void FinishReloading()
    {
        
        currentAmmo = maxAmmo;
        isReloading = false;
        animator.ResetTrigger("Reload");
    }
    void AlertNearbyEnemies(float noiseLevel)
    {
        MoveEnemyAssault[] enemies = FindObjectsByType<MoveEnemyAssault>(FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            enemy.OnHearNoise(transform.position, noiseLevel);
        }
    }
    public void TriggerFaceShoot()
    {
        Debug.Log("TriggerFaceShoot called!");
        if (currentAmmo > 0 && !isReloading && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + (1f / fireRate);
            Shoot();
            Debug.Log("Face shoot executed!");
        }
        else
        {
            Debug.Log($"Face shoot blocked - Ammo: {currentAmmo}, Reloading: {isReloading}");
        }
    }
}