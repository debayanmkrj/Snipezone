using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Image = UnityEngine.UI.Image;

// <summary>
// ASSAULT ENEMY AI - 3D Sniper Game
// 
// Features:
// - State Machine: Idle, Wander, Seek, Fire, TakingCover, Flanking, Investigating, Dying
// - Accuracy system with spread
// - Flanking behavior
// - Hearing system for sound detection
// - Cover system with tactical scoring
// - Repositioning after missed shots
// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class MoveEnemyAssault : MonoBehaviour
{
    public enum EnemyState
    {
        Idle,
        Wander,
        Seek,
        Fire,
        TakingCover,
        Flanking,
        Investigating,
        Dying
    }

    [Header("Target")]
    public Transform Target;

    [Header("Health")]
    public int maxHealth = 100;
    public int currentHealth;

    [Header("Health UI")]
    public Image healthBarFill;
    public Canvas healthBarCanvas;

    [Header("Ranges")]
    public float DetectionRange = 100f;
    public float OptimalAttackDistance = 50f;
    public float MinAttackDistance = 50f;
    public float MaxAttackDistance = 75f;
    public float attackRange = 100f;

    [Header("Movement")]
    public float seekSpeed = 5f;
    public float coverSpeed = 7f;
    public float wanderSpeed = 2.5f;
    public float flankSpeed = 6f;
    public float rotationSpeed = 8f;
    private float actualMovementSpeed = 0f;
    private Vector3 lastFramePosition;

    [Header("Combat")]
    public float fireRate = 0.5f;
    public float attackDamage = 10f;
    public ParticleSystem muzzleFlash;
    public Transform firePoint;
    public AudioClip shootSound;
    public GameObject bloodScreenEffect;
    public float bloodEffectDuration = 0.1f;
    public LayerMask playerLayer;
    public int maxShotsBeforeReposition = 3;
    
    private int consecutiveMisses = 0;
    private int consecutiveBlockedShots = 0;
    private int totalShotsFired = 0;
    private bool shouldRepositionForBetterShot = false;
    private float lastShotTime = 0f;

    [Header("Accuracy")]
    public float baseAccuracy = 0.8f;
    public float movingAccuracyPenalty = 0.3f;
    public float maxSpreadAngle = 5f;
    public float distanceAccuracyFalloff = 0.005f;

    [Header("Cover System")]
    public Transform[] coverPoints;
    public float coverDuration = 3f;
    public float coverSearchRadius = 30f;
    public float healthThresholdForCover = 50f;
    public float minHealthToExitCover = 0.7f;

    [Header("Flanking")]
    public float flankDistance = 15f;
    public float flankChance = 0.3f;
    private Vector3 flankTarget;
    private float lastFlankAttempt = 0f;
    private float flankCooldown = 10f;

    [Header("Hearing")]
    public float hearingRange = 40f;
    public float investigationTime = 5f;
    private Vector3 lastHeardPosition;
    private float investigationEndTime = 0f;
    private bool hasHeardNoise = false;

    [Header("Vision")]
    public LayerMask visionObstacleMask;
    public float eyeHeight = 1.6f;
    public float losCheckInterval = 0.1f;

    [Header("Wander")]
    public float wanderRadius = 15f;
    public float wanderWaitTime = 0.5f;

    [Header("Tuning")]
    public float arrivalThreshold = 2f;
    public float retreatDistance = 5f;
    public float advanceDistance = 5f;
    public float navMeshSampleRange = 10f;
    public int shotsToRepositionThreshold = 3;
    public float maxSeekTime = 5f;

    [Header("Audio")]
    private AudioSource audioSource;
    public AudioClip footstepSound;
    public float footstepIntervalWalk = 0.5f;
    public float footstepIntervalRun = 0.35f;
    public float footstepIntervalSprint = 0.25f;

    [Header("Audio Feedback")]
    public AudioClip hitTakenSound;
    public AudioClip nearMissSound;
    public float nearMissDistance = 2f;

    [Header("Debug")]
    // public bool showDebugGizmos = true; // to debug  in Scene view
    public bool showDebugLogs = false;

    // Internal state
    private NavMeshAgent agent;
    private Animator animator;
    private EnemyState currentState = EnemyState.Wander;
    private EnemyState lastAnimatedState = EnemyState.Idle;
    
    private float nextFireTime = 0f;
    private float nextFootstepTime = 0f;
    private float lastLOSCheck = 0f;
    
    private bool hasLOS = false;
    private bool isDead = false;
    private Transform currentCoverPoint = null;
    private float coverExitTime = 0f;
    private PlayerMovement playerScript;

    private float stuckInSeekTime = 0f;
    
    // Wander state
    private Vector3 wanderTarget;
    private bool isWaitingAtWander = false;
    private float wanderWaitTimer = 0f;
    
    // Damage tracking
    private float lastHealthCheck = 0f;
    private bool shouldSeekCover = false;
    private int consecutiveHitsTaken = 0;
    private float lastHitTime = 0f;
    private float hitTimeWindow = 5f;
    private int hitsBeforeCover = 2;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        
        agent.updateRotation = false;
        agent.updatePosition = false;
        agent.updateUpAxis = true;
        
        audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.spatialBlend = 0.8f;
        audioSource.minDistance = 1f;
        audioSource.maxDistance = 100f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.volume = 1f;
    }

    void Start()
    {
        currentHealth = maxHealth;
        lastHealthCheck = maxHealth;
        UpdateHealthUI();
        
        if (!agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, navMeshSampleRange, NavMesh.AllAreas))
            {
                transform.position = hit.position;
                if (showDebugLogs) Debug.Log($"Warped enemy to NavMesh at {hit.position}");
            }
            else
            {
                Debug.LogError("Enemy not on NavMesh and couldn't find valid position!");
            }
        }
        
        agent.isStopped = false;
        agent.speed = seekSpeed;
        agent.acceleration = 8f;
        agent.angularSpeed = 360f;
        agent.autoBraking = false;
        
        if (Target != null)
        {
            playerScript = Target.GetComponent<PlayerMovement>();
        }
        else
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                Target = player.transform;
                playerScript = player.GetComponent<PlayerMovement>();
            }
        }
        
        currentState = EnemyState.Wander;
        OnStateEnter(currentState);
        
        lastFramePosition = transform.position;
    }

    void Update()
    {
        if (isDead) return;

        CheckHealthForCover();
        ValidateTarget();
        UpdateLOS();
        
        EnemyState newState = DetermineState();
        
        if (newState != currentState)
        {
            if (showDebugLogs) Debug.Log($"State change: {currentState} -> {newState}");
            OnStateExit(currentState);
            currentState = newState;
            OnStateEnter(currentState);
        }

        ExecuteCurrentState();
        ManuallyMoveAgent();
        UpdateAnimator();
    }

    // HELPER METHODS


    private void CheckHealthForCover()
    {
        if (currentHealth < lastHealthCheck)
        {
            float healthPercent = (currentHealth / (float)maxHealth) * 100f;
            if (healthPercent < healthThresholdForCover && !shouldSeekCover)
            {
                shouldSeekCover = true;
                if (showDebugLogs) Debug.Log("Health low - will seek cover!");
            }
            lastHealthCheck = currentHealth;
        }
    }

    private void ValidateTarget()
    {
        if (Target == null || playerScript == null || playerScript.currentHealth <= 0)
        {
            if (currentState != EnemyState.Wander && currentState != EnemyState.Investigating)
            {
                OnStateExit(currentState);
                currentState = EnemyState.Wander;
                OnStateEnter(currentState);
            }
        }
    }

    private void UpdateLOS()
    {
        if (Time.time - lastLOSCheck >= losCheckInterval)
        {
            lastLOSCheck = Time.time;
            hasLOS = CheckLineOfSight();
        }
    }

    private float GetCurrentSpeed()
    {
        switch (currentState)
        {
            case EnemyState.Wander:
            case EnemyState.Investigating:
                return wanderSpeed;
            case EnemyState.Seek:
                return seekSpeed;
            case EnemyState.TakingCover:
                return coverSpeed;
            case EnemyState.Flanking:
                return flankSpeed;
            case EnemyState.Fire:
            case EnemyState.Idle:
            case EnemyState.Dying:
                return 0f;
            default:
                return seekSpeed;
        }
    }

    // STATE MACHINE
    
    private EnemyState DetermineState()
    {
        if (isDead) return EnemyState.Dying;
        
        // In cover - stay until conditions met
        if (currentCoverPoint != null)
        {
            float distToCover = Vector3.Distance(transform.position, currentCoverPoint.position);
            
            if (distToCover > arrivalThreshold || !ShouldExitCover())
                return EnemyState.TakingCover;
            else
            {
                currentCoverPoint = null;
                shouldSeekCover = false;
                consecutiveHitsTaken = 0;
            }
        }
        
        // Need cover
        if (shouldSeekCover && coverPoints != null && coverPoints.Length > 0)
        {
            Transform cover = FindNearestCover();
            if (cover != null)
            {
                currentCoverPoint = cover;
                coverExitTime = Time.time + coverDuration;
                if (showDebugLogs) Debug.Log("Taking cover due to damage!");
                return EnemyState.TakingCover;
            }
            else
            {
                shouldSeekCover = false;
            }
        }
        
        // Flanking in progress
        if (currentState == EnemyState.Flanking)
        {
            float distToFlank = Vector3.Distance(transform.position, flankTarget);
            if (distToFlank > arrivalThreshold)
                return EnemyState.Flanking;
        }
        
        // No target - check investigation or wander
        if (Target == null || playerScript == null || playerScript.currentHealth <= 0)
        {
            if (hasHeardNoise && Time.time < investigationEndTime)
                return EnemyState.Investigating;
            return EnemyState.Wander;
        }
        
        float distanceToTarget = Vector3.Distance(transform.position, Target.position);
        
        // Out of detection range
        if (distanceToTarget > DetectionRange)
        {
            if (hasHeardNoise && Time.time < investigationEndTime)
                return EnemyState.Investigating;
            return EnemyState.Wander;
        }
        
        // Has LOS - decide between Fire or Flank
        if (hasLOS && distanceToTarget <= DetectionRange)
        {
            if (shouldRepositionForBetterShot)
            {
                shouldRepositionForBetterShot = false;
                consecutiveMisses = 0;
                consecutiveBlockedShots = 0;
                
                // Maybe flank instead of just seeking
                if (ShouldAttemptFlank())
                {
                    return EnemyState.Flanking;
                }
                
                stuckInSeekTime = Time.time;
                return EnemyState.Seek;
            }
            
            return EnemyState.Fire;
        }
        
        // No LOS - seek or investigate
        if (!hasLOS)
        {
            if (currentState == EnemyState.Seek)
            {
                if (Time.time - stuckInSeekTime > maxSeekTime)
                {
                    if (showDebugLogs) Debug.Log("Stuck in Seek too long - switching to Wander");
                    stuckInSeekTime = 0f;
                    return EnemyState.Wander;
                }
            }
            else
            {
                stuckInSeekTime = Time.time;
            }
            
            return EnemyState.Seek;
        }
        
        return EnemyState.Seek;
    }

    private bool ShouldAttemptFlank()
    {
        if (Time.time - lastFlankAttempt < flankCooldown) return false;
        if (Random.value > flankChance) return false;
        
        Vector3 flankPos = GetFlankPosition();
        NavMeshHit hit;
        if (NavMesh.SamplePosition(flankPos, out hit, navMeshSampleRange, NavMesh.AllAreas))
        {
            flankTarget = hit.position;
            lastFlankAttempt = Time.time;
            if (showDebugLogs) Debug.Log("Attempting flank maneuver!");
            return true;
        }
        
        return false;
    }

    private Vector3 GetFlankPosition()
    {
        if (Target == null) return transform.position;
        
        Vector3 dirToPlayer = (Target.position - transform.position).normalized;
        Vector3 perpendicular = Vector3.Cross(dirToPlayer, Vector3.up);
        float side = Random.value > 0.5f ? 1f : -1f;
        
        return Target.position + perpendicular * side * flankDistance - dirToPlayer * OptimalAttackDistance;
    }

    private bool ShouldExitCover()
    {
        if (Time.time < coverExitTime) return false;
        
        return true;
    }

    // STATE CALLBACKS
    
    private void OnStateEnter(EnemyState state)
    {
        if (showDebugLogs) Debug.Log($"Enter state: {state}");
        
        switch (state)
        {
            case EnemyState.Wander:
                SetNewWanderPoint();
                agent.speed = wanderSpeed;
                break;
                
            case EnemyState.Seek:
                agent.isStopped = false;
                agent.speed = seekSpeed;
                break;
                
            case EnemyState.Fire:
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                agent.ResetPath();
                break;
                
            case EnemyState.TakingCover:
                agent.isStopped = false;
                agent.speed = coverSpeed;
                if (currentCoverPoint != null)
                {
                    agent.SetDestination(currentCoverPoint.position);
                }
                break;
                
            case EnemyState.Flanking:
                agent.isStopped = false;
                agent.speed = flankSpeed;
                agent.SetDestination(flankTarget);
                break;
                
            case EnemyState.Investigating:
                agent.isStopped = false;
                agent.speed = wanderSpeed;
                agent.SetDestination(lastHeardPosition);
                break;
                
            case EnemyState.Dying:
                agent.isStopped = true;
                agent.enabled = false;
                break;
        }
    }
    
    private void OnStateExit(EnemyState state)
    {
        if (showDebugLogs) Debug.Log($"Exit state: {state}");
    
        switch (state)
        {
            case EnemyState.TakingCover:
                agent.speed = seekSpeed;
                break;
                
            case EnemyState.Fire:
                agent.isStopped = false;
                break;
                
            case EnemyState.Flanking:
                lastFlankAttempt = Time.time;
                break;
                
            case EnemyState.Investigating:
                hasHeardNoise = false;
                break;
        }
    }
    
    private void ExecuteCurrentState()
    {
        switch (currentState)
        {
            case EnemyState.Idle:
                ExecuteIdle();
                break;
            case EnemyState.Wander:
                ExecuteWander();
                break;
            case EnemyState.Seek:
                ExecuteSeek();
                break;
            case EnemyState.Fire:
                ExecuteFire();
                break;
            case EnemyState.TakingCover:
                ExecuteTakingCover();
                break;
            case EnemyState.Flanking:
                ExecuteFlanking();
                break;
            case EnemyState.Investigating:
                ExecuteInvestigating();
                break;
            case EnemyState.Dying:
                ExecuteDying();
                break;
        }
    }

    // STATE EXECUTION
    
    private void ExecuteIdle()
    {
        agent.isStopped = true;
    }
    
    private void ExecuteWander()
    {
        if (!agent.hasPath || agent.remainingDistance < arrivalThreshold)
        {
            if (isWaitingAtWander)
            {
                if (Time.time >= wanderWaitTimer)
                {
                    isWaitingAtWander = false;
                    SetNewWanderPoint();
                }
            }
            else
            {
                isWaitingAtWander = true;
                wanderWaitTimer = Time.time + wanderWaitTime;
                agent.isStopped = true;
            }
        }
        else
        {
            agent.isStopped = false;
            agent.speed = wanderSpeed;
        }
    }
    
    private void ExecuteSeek()
    {
        if (Target == null) return;
        
        agent.isStopped = false;
        agent.speed = seekSpeed;
        
        if (!agent.hasPath || agent.remainingDistance < arrivalThreshold)
        {
            Vector3 bestPosition = FindPositionWithLOS();
            
            if (bestPosition != Vector3.zero)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(bestPosition, out hit, navMeshSampleRange, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit.position);
                    if (showDebugLogs) Debug.Log($"Seeking to position with LOS: {hit.position}");
                }
            }
            else
            {
                Vector3 directionToTarget = (Target.position - transform.position).normalized;
                Vector3 closePosition = Target.position - directionToTarget * OptimalAttackDistance;
                
                NavMeshHit hit;
                if (NavMesh.SamplePosition(closePosition, out hit, navMeshSampleRange, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit.position);
                }
            }
        }
        
        // Fire while moving if LOS
        if (hasLOS && Time.time >= nextFireTime)
        {
            FireWeapon();
            nextFireTime = Time.time + fireRate * 1.5f;
        }
        
        FaceTarget();
    }
    
    private void ExecuteFire()
    {
        if (Target == null) return;
        
        float distanceToTarget = Vector3.Distance(transform.position, Target.position);
        
        if (distanceToTarget < MinAttackDistance)
        {
            // Too close - back away
            agent.isStopped = false;
            agent.speed = seekSpeed * 0.5f;
            
            Vector3 awayDirection = (transform.position - Target.position).normalized;
            Vector3 retreatPosition = transform.position + awayDirection * retreatDistance;
            
            NavMeshHit hit;
            if (NavMesh.SamplePosition(retreatPosition, out hit, navMeshSampleRange, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }
        else if (distanceToTarget > MaxAttackDistance)
        {
            // Too far - advance
            agent.isStopped = false;
            agent.speed = seekSpeed * 0.5f;
            
            Vector3 advanceDirection = (Target.position - transform.position).normalized;
            Vector3 advancePosition = transform.position + advanceDirection * advanceDistance;
            
            NavMeshHit hit;
            if (NavMesh.SamplePosition(advancePosition, out hit, navMeshSampleRange, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }
        else
        {
            // Optimal range
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.ResetPath();
        }
        
        FaceTarget();
        
        if (Time.time >= nextFireTime)
        {
            FireWeapon();
            nextFireTime = Time.time + fireRate;
        }
    }
    
    private void ExecuteTakingCover()
    {
        if (currentCoverPoint == null)
        {
            if (showDebugLogs) Debug.Log("No cover point in TakingCover state!");
            return;
        }
        
        agent.isStopped = false;
        agent.speed = coverSpeed;
        
        float distanceToCover = Vector3.Distance(transform.position, currentCoverPoint.position);
        
        // Fire at target while moving to cover if we have LOS
        if (hasLOS && Target != null && Time.time >= nextFireTime)
        {
            FaceTarget();
            FireWeapon();
            nextFireTime = Time.time + fireRate * 2f; // Slower while running to cover
        }
    }
    
    private void ExecuteFlanking()
    {
        agent.isStopped = false;
        agent.speed = flankSpeed;
        
        // Fire while flanking if we have LOS
        if (hasLOS && Target != null && Time.time >= nextFireTime)
        {
            FaceTarget();
            FireWeapon();
            nextFireTime = Time.time + fireRate * 1.5f;
        }
        else if (!hasLOS && Target != null)
        {
            // Face movement direction if no LOS
            if (agent.velocity.sqrMagnitude > 0.01f)
            {
                Vector3 moveDir = agent.velocity.normalized;
                moveDir.y = 0f;
                if (moveDir.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                }
            }
        }
    }
    
    private void ExecuteInvestigating()
    {
        if (!agent.hasPath || agent.remainingDistance < arrivalThreshold)
        {
            // Reached investigation point - look around
            agent.isStopped = true;
            
            // Slowly rotate to look around
            transform.Rotate(0f, 30f * Time.deltaTime, 0f);
        }
        else
        {
            agent.isStopped = false;
            agent.speed = wanderSpeed;
        }
        
        // Check if investigation time expired
        if (Time.time >= investigationEndTime)
        {
            hasHeardNoise = false;
        }
    }
    
    private void ExecuteDying()
    {
        // Animation handles this
    }

    // MOVEMENT
    
    private void ManuallyMoveAgent()
    {
        if (!agent.enabled || !agent.isOnNavMesh)
        {
            actualMovementSpeed = 0f;
            return;
        }
        
        if (agent.hasPath && !agent.isStopped)
        {
            float currentSpeed = GetCurrentSpeed();
            
            Vector3 targetPosition = agent.nextPosition;
            Vector3 currentPosition = transform.position;
            
            Vector3 currentPosFlat = new Vector3(currentPosition.x, 0f, currentPosition.z);
            Vector3 targetPosFlat = new Vector3(targetPosition.x, 0f, targetPosition.z);
            Vector3 horizontalDirection = (targetPosFlat - currentPosFlat).normalized;
            float horizontalDistance = Vector3.Distance(currentPosFlat, targetPosFlat);
            
            if (horizontalDistance > 0.01f && currentSpeed > 0f)
            {
                if (currentState != EnemyState.Fire)
                {
                    if (horizontalDirection.sqrMagnitude > 0.001f)
                    {
                        Quaternion targetRotation = Quaternion.LookRotation(horizontalDirection);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                    }
                }
                
                float horizontalMovement = currentSpeed * Time.deltaTime;
                
                if (horizontalMovement > horizontalDistance)
                {
                    horizontalMovement = horizontalDistance;
                }
                
                Vector3 newPosition = currentPosition + horizontalDirection * horizontalMovement;
                newPosition.y = targetPosition.y;
                
                transform.position = newPosition;
                agent.nextPosition = transform.position;
                
                actualMovementSpeed = (transform.position - lastFramePosition).magnitude / Time.deltaTime;
                lastFramePosition = transform.position;
                
                PlayFootstepAudio(actualMovementSpeed);
            }
            else
            {
                agent.nextPosition = transform.position;
                actualMovementSpeed = 0f;
            }
        }
        else
        {
            actualMovementSpeed = 0f;
        }
    }
    
    private void PlayFootstepAudio(float currentSpeed)
    {
        if (footstepSound == null || audioSource == null) return;
        if (Time.time < nextFootstepTime) return;
        
        float footstepInterval = footstepIntervalWalk;
        
        if (currentSpeed >= coverSpeed * 0.9f)
        {
            footstepInterval = footstepIntervalSprint;
        }
        else if (currentSpeed >= seekSpeed * 0.9f)
        {
            footstepInterval = footstepIntervalRun;
        }
        
        audioSource.PlayOneShot(footstepSound, 0.5f);
        nextFootstepTime = Time.time + footstepInterval;
    }

    // COMBAT  
    private void FireWeapon()
    {
        if (Target == null) return;
        
        lastShotTime = Time.time;
        totalShotsFired++;
        
        float distanceToTarget = Vector3.Distance(transform.position, Target.position);
        
        if (distanceToTarget > attackRange)
        {
            consecutiveMisses++;
            if (consecutiveMisses >= shotsToRepositionThreshold)
            {
                shouldRepositionForBetterShot = true;
                ResetShotCounters();
            }
            return;
        }
        
        // Muzzle flash
        if (muzzleFlash != null)
        {
            muzzleFlash.Play();
        }
        
        // Sound
        if (shootSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(shootSound);
        }
        
        // Get target position
        Vector3 targetCenter = Target.position + Vector3.up * 1f;
        Collider targetCollider = Target.GetComponent<Collider>();
        if (targetCollider != null)
        {
            targetCenter = targetCollider.bounds.center;
        }
        
        // Calculate direction with accuracy spread
        Vector3 direction = (targetCenter - firePoint.position).normalized;
        direction = ApplyAccuracySpread(direction, distanceToTarget);
        
        if (showDebugLogs)
        {
            Debug.DrawRay(firePoint.position, direction * attackRange, Color.yellow, 1f);
        }
        
        // Raycast
        RaycastHit hit;
        bool hitPlayer = false;
        
        if (Physics.Raycast(firePoint.position, direction, out hit, attackRange))
        {
            if (showDebugLogs)
            {
                Debug.DrawLine(firePoint.position, hit.point, Color.red, 2f);
            }
            
            PlayerMovement playerMovement = hit.transform.GetComponentInParent<PlayerMovement>();
            if (playerMovement == null)
            {
                playerMovement = hit.transform.root.GetComponent<PlayerMovement>();
            }
            if (playerMovement == null)
            {
                playerMovement = hit.transform.GetComponent<PlayerMovement>();
            }
            
            if (playerMovement != null)
            {
                hitPlayer = true;
                if (showDebugLogs) Debug.Log($"HIT PLAYER! Dealing {attackDamage} damage!");
                playerMovement.TakeDamage((int)attackDamage);
                
                if (bloodScreenEffect != null)
                {
                    StartCoroutine(ShowBloodEffect());
                }
                
                consecutiveMisses = 0;
                consecutiveBlockedShots = 0;
            }
            else
            {
                consecutiveBlockedShots++;
                if (showDebugLogs) Debug.Log($"SHOT BLOCKED by {hit.collider.name}!");
            }
        }
        else
        {
            consecutiveMisses++;
            if (showDebugLogs) Debug.Log($"MISS! Consecutive misses: {consecutiveMisses}");
        }
        
        // Check if need to reposition
        if (consecutiveMisses >= shotsToRepositionThreshold || 
            consecutiveBlockedShots >= shotsToRepositionThreshold ||
            totalShotsFired >= maxShotsBeforeReposition)
        {
            shouldRepositionForBetterShot = true;
            ResetShotCounters();
        }
    }
    
    private Vector3 ApplyAccuracySpread(Vector3 direction, float distance)
    {
        float accuracy = baseAccuracy;
        
        // Penalty for moving
        if (actualMovementSpeed > 0.5f)
        {
            accuracy -= movingAccuracyPenalty;
        }
        
        // Penalty for distance
        accuracy -= distance * distanceAccuracyFalloff;
        
        // Clamp accuracy
        accuracy = Mathf.Clamp(accuracy, 0.1f, 1f);
        
        // Calculate spread based on accuracy
        float spread = maxSpreadAngle * (1f - accuracy);
        float randomAngleX = Random.Range(-spread, spread);
        float randomAngleY = Random.Range(-spread, spread);
        
        Quaternion spreadRotation = Quaternion.Euler(randomAngleX, randomAngleY, 0f);
        return spreadRotation * direction;
    }
    
    private void ResetShotCounters()
    {
        consecutiveMisses = 0;
        consecutiveBlockedShots = 0;
        totalShotsFired = 0;
    }
    
    private IEnumerator ShowBloodEffect()
    {
        GameObject effect = Instantiate(bloodScreenEffect);
        yield return new WaitForSeconds(bloodEffectDuration);
        if (effect != null)
        {
            Destroy(effect);
        }
    }
    
    public void TakeDamage(int damage)
    {
        if (isDead) return;
        
        currentHealth -= damage;
        UpdateHealthUI();
        
        // Track consecutive hits
        if (Time.time - lastHitTime < hitTimeWindow)
        {
            consecutiveHitsTaken++;
        }
        else
        {
            consecutiveHitsTaken = 1;
        }
        lastHitTime = Time.time;
        
        // Hit sound
        if (hitTakenSound != null && audioSource != null)
        {
            StartCoroutine(PlayDelayedSound(hitTakenSound, 0.35f, 1.2f));
        }
        
        if (showDebugLogs) Debug.Log($"Took {damage} damage. Health: {currentHealth}/{maxHealth}");
        
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
            return;
        }
        
        // Force cover after consecutive hits
        if (consecutiveHitsTaken >= hitsBeforeCover)
        {
            if (showDebugLogs) Debug.Log($"Took {consecutiveHitsTaken} hits - forcing cover!");
            shouldSeekCover = true;
            consecutiveHitsTaken = 0;
        }
    }
    
    public void OnNearMiss(Vector3 missPosition)
    {
        float distance = Vector3.Distance(transform.position, missPosition);
        
        if (distance < nearMissDistance)
        {
            if (nearMissSound != null && audioSource != null)
            {
                StartCoroutine(PlayDelayedSound(nearMissSound, 0.4f, 1.0f));
            }
            
            // Alert enemy
            if (currentState == EnemyState.Idle || currentState == EnemyState.Wander)
            {
                if (Target != null)
                {
                    float distanceToTarget = Vector3.Distance(transform.position, Target.position);
                    if (distanceToTarget <= DetectionRange)
                    {
                        hasLOS = CheckLineOfSight();
                    }
                }
            }
        }
    }


    // HEARING SYSTEM    
    public void OnHearNoise(Vector3 noisePosition, float noiseLevel)
    {
        float effectiveRange = hearingRange * noiseLevel;
        float distance = Vector3.Distance(transform.position, noisePosition);
        
        if (distance > effectiveRange) return;
        
        // Only react if not already engaged
        if (currentState == EnemyState.Wander || currentState == EnemyState.Idle)
        {
            lastHeardPosition = noisePosition;
            investigationEndTime = Time.time + investigationTime;
            hasHeardNoise = true;
            
            if (showDebugLogs) Debug.Log($"Heard noise at {noisePosition}, investigating...");
        }
    }
    
    private IEnumerator PlayDelayedSound(AudioClip clip, float delay, float volume)
    {
        yield return new WaitForSeconds(delay);
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }
    
    private void Die()
    {
        if (isDead) return;
        
        isDead = true;
        
        if (animator != null)
        {
            animator.SetBool("Dead", true);
        }
        
        agent.isStopped = true;
        agent.ResetPath();
        agent.enabled = false;
        
        if (showDebugLogs) Debug.Log("Enemy died");
        
        Destroy(gameObject, 5f);
    }

    // COVER SYSTEM
    private Transform FindNearestCover()
    {
        if (coverPoints == null || coverPoints.Length == 0) return null;
        
        Transform bestCover = null;
        float bestScore = float.MinValue;
        
        foreach (Transform cover in coverPoints)
        {
            if (cover == null) continue;
            
            float distanceToCover = Vector3.Distance(transform.position, cover.position);
            
            if (distanceToCover > coverSearchRadius) continue;
            
            NavMeshHit navHit;
            if (!NavMesh.SamplePosition(cover.position, out navHit, navMeshSampleRange, NavMesh.AllAreas)) continue;
            
            // Score: closer is better
            float score = -distanceToCover;
            
            // Bonus for tactical cover with LOS
            if (Target != null)
            {
                Vector3 coverToPlayer = Target.position - cover.position;
                RaycastHit losHit;
                bool coverHasLOS = !Physics.Raycast(
                    cover.position + Vector3.up * eyeHeight,
                    coverToPlayer.normalized,
                    out losHit,
                    coverToPlayer.magnitude,
                    visionObstacleMask
                );
                
                if (coverHasLOS)
                {
                    score += 50f;
                }
            }
            
            if (score > bestScore)
            {
                bestScore = score;
                bestCover = cover;
            }
        }
        
        return bestCover;
    }

    // WANDER & NAVIGATION
    private void SetNewWanderPoint()
    {
        float radius = wanderRadius;
        if (currentState == EnemyState.Seek)
        {
            radius = wanderRadius * 2f;
        }
        
        Vector2 randomCircle = Random.insideUnitCircle * radius;
        Vector3 randomDirection = new Vector3(randomCircle.x, 0f, randomCircle.y);
        Vector3 wanderPosition = transform.position + randomDirection;
        
        NavMeshHit navHit;
        if (NavMesh.SamplePosition(wanderPosition, out navHit, wanderRadius, NavMesh.AllAreas))
        {
            wanderTarget = navHit.position;
            agent.isStopped = false;
            agent.speed = wanderSpeed;
            agent.SetDestination(wanderTarget);
        }
    }
    
    private Vector3 FindPositionWithLOS()
    {
        if (Target == null) return Vector3.zero;
        
        Vector3 directionToTarget = (Target.position - transform.position).normalized;
        float distance = OptimalAttackDistance;
        
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f;
            Quaternion rotation = Quaternion.Euler(0, angle, 0);
            Vector3 offset = rotation * -directionToTarget * distance;
            Vector3 testPosition = Target.position + offset;
            
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(testPosition, out navHit, navMeshSampleRange, NavMesh.AllAreas))
            {
                if (CheckLOSFromPosition(navHit.position, Target.position))
                {
                    return navHit.position;
                }
            }
        }
        
        return Vector3.zero;
    }

    private bool CheckLOSFromPosition(Vector3 fromPosition, Vector3 toPosition)
    {
        Vector3 origin = fromPosition + Vector3.up * eyeHeight;
        Vector3 targetPos = toPosition + Vector3.up * 1f;
        Vector3 direction = targetPos - origin;
        float distance = direction.magnitude;
        
        RaycastHit hit;
        if (Physics.Raycast(origin, direction.normalized, out hit, distance, visionObstacleMask))
        {
            return false;
        }
        
        return true;
    }

    // UTILITY

    
    private void FaceTarget()
    {
        if (Target == null) return;
        
        Vector3 direction = (Target.position - transform.position).normalized;
        direction.y = 0f;
        
        if (direction.sqrMagnitude < 0.001f) return;
        
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }
    
    private bool CheckLineOfSight()
    {
        if (Target == null) return false;
        
        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        Vector3 targetPos = Target.position + Vector3.up * 1f;
        Vector3 direction = targetPos - origin;
        float distance = direction.magnitude;
        
        RaycastHit hit;
        if (Physics.Raycast(origin, direction.normalized, out hit, distance, visionObstacleMask))
        {
            if (showDebugLogs) Debug.Log($"LOS blocked by: {hit.collider.name}");
            Debug.DrawLine(origin, hit.point, Color.red, losCheckInterval);
            return false;
        }
        
        Debug.DrawLine(origin, targetPos, Color.green, losCheckInterval);
        return true;
    }
    
    private void UpdateAnimator()
    {
        if (animator == null) return;
        
        float currentSpeed = actualMovementSpeed;
        
        float velocityX = 0f;
        float velocityZ = 0f;
        
        bool stateJustChanged = (currentState != lastAnimatedState);
        lastAnimatedState = currentState;
        
        if (currentSpeed < 0.1f)
        {
            velocityX = 0f;
            velocityZ = 0f;
        }
        else
        {
            Vector3 worldDirection = agent.desiredVelocity.normalized;
            Vector3 localDirection = transform.InverseTransformDirection(worldDirection);
            
            float targetSpeed = 0f;
            
            switch (currentState)
            {
                case EnemyState.Wander:
                case EnemyState.Investigating:
                    targetSpeed = 0.5f;
                    break;
                    
                case EnemyState.Seek:
                    targetSpeed = 1.0f;
                    break;
                    
                case EnemyState.TakingCover:
                case EnemyState.Flanking:
                    targetSpeed = 2.0f;
                    break;
                    
                case EnemyState.Fire:
                    if (agent.velocity.magnitude > 0.1f)
                        targetSpeed = 0.5f;
                    else
                        targetSpeed = 0f;
                    break;
                    
                default:
                    targetSpeed = 1.0f;
                    break;
            }
            
            if (Mathf.Abs(localDirection.z) > 0.8f)
            {
                velocityX = 0f;
                velocityZ = targetSpeed * Mathf.Sign(localDirection.z);
            }
            else if (Mathf.Abs(localDirection.x) > 0.8f)
            {
                velocityX = 1.5f * Mathf.Sign(localDirection.x);
                velocityZ = 0f;
            }
            else
            {
                velocityX = localDirection.x * 1.5f;
                velocityZ = localDirection.z * targetSpeed;
            }
        }
        
        velocityX = Mathf.Clamp(velocityX, -2f, 2f);
        velocityZ = Mathf.Clamp(velocityZ, -2f, 2f);
        
        float deadZone = 0.05f;
        if (Mathf.Abs(velocityX) < deadZone) velocityX = 0f;
        if (Mathf.Abs(velocityZ) < deadZone) velocityZ = 0f;
        
        float speed = Mathf.Sqrt(velocityX * velocityX + velocityZ * velocityZ);
        
        animator.SetFloat("VelocityX", velocityX);
        animator.SetFloat("VelocityZ", velocityZ);
        animator.SetFloat("Speed", speed);
    }
    
    private void UpdateHealthUI()
    {
        if (healthBarFill != null)
        {
            float value = Mathf.InverseLerp(0, maxHealth, currentHealth);
            healthBarFill.fillAmount = value;
        }
    }
}