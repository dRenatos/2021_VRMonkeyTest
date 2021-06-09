using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StealthPlayerController : Character
{

    Camera cam;
    public Rigidbody rb;

    public GameObject normalModel;
    public GameObject cloakedModel;

    Renderer normalRenderer;
    Renderer cloakedRenderer;

    public bool hidden;

    [Header("Movement Speed")] Vector3 inputVector;
    public bool infiniteFriction;
    public float friction = 3.0f;

    public bool infiniteAcceleration;
    public float acceleration = 3.0f;
    public float maxSpeed = 5.0f;
    Vector3 speed = Vector3.zero;
    float maxSpeedFraction = 0.0f;

    public RobotThreadController threadController;
    public float threadWalkSpeed = 0.5f;
    public float threadRunSpeed = 0.75f;

    [Header("Rotation")] public float rotateSpeed = 5.0f;
    public bool instanteRotate = true;
    bool rotating = false;
    public bool lockMovementRotation = false;
    Quaternion rotationDestination;
    public bool lockMovementToAngle = false;
    public float maxAngleDiffToMove = 10.0f;
    float angleDiff;

    [Header("Energy")] bool moving = false;
    public bool enableEnergyDrain = false;
    public float maxEnergy = 50;
    public float energy = 50;
    public float lowEnergyFraction = 0.2f;
    public float lowEnergyMultiplier = 0.5f;
    public float energyDrainSpeed = 5;
    public float standingEnergyMultiplier = 0.5f;
    public ProgressBar energyBar;
    [Header("Skills")] public bool cloaked = false;
    public bool running = false;
    public float runningSpeedMultiplier = 1.5f;
    public float runningEnergyMultiplier = 1.5f;
    public float cloakingEnergyMultiplier = 3.5f;
    public float floatingEnergyMultiplier = 3.5f;
    public float drainingEnergyMultiplier = 4.0f;
    public float shockDelay = 0.3f;
    public float shockCost = 10;
    public float drainSpeed = 0;
    public GameObject shockObject;

    public GameObject drainObject;
    public float drainRange = 2;
    public LayerMask enemyLayerMask;

    public AIAgent drainTarget;

    [Header("Effects")] public ParticleSystem walkParticles;
    public ParticleSystem runParticles;

    [Header("Upgrades")] public bool canShock = false;
    public bool canCloak = false;
    public bool canDrain = false;
    public bool canShoot;
    public bool canFloat;

    public ParticleSystem warpParticles;
    private PlayerShootData _playerShootData;

    static StealthPlayerController _instance;
    private Coroutine floatRobotCoroutine;
    private float initialYPosition;
    
    [Header("Float Properties")]
    public float floatYAmount = 2.0f;
    public float floatAnimationTime = 1.0f;
    public ParticleSystem floatingParticles;
    public float initialParticleSize;
    public float finalParticleSize;
    public float initialParticleSpeed;
    public float finalParticleSpeed;
    public float initialSoundPitch;
    public float finalSoundPitch;
    public AudioClip floatingSFX;
    
    private bool floated;
    
    
    public static StealthPlayerController getInstance()
    {
        if (_instance == null)
        {
            _instance = GameObject.FindObjectOfType<StealthPlayerController>();
        }

        return _instance;
    }

    // Use this for initialization
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        cam = Camera.main;
        rb = GetComponent<Rigidbody>();

        normalRenderer = normalModel.GetComponent<Renderer>();
        cloakedRenderer = cloakedModel.GetComponent<Renderer>();

        if (energyBar != null)
        {
            energyBar.SetInitialValues(maxEnergy, 0, energy);
        }

        _playerShootData = (PlayerShootData) shootData;
        initialYPosition = transform.position.y;
    }

    public void SetEnergy(float val)
    {
        energy = val;
        energyBar.UpdateBar(energy);
    }

    public void ResetEnergy()
    {
        energy = maxEnergy;
        energyBar.UpdateBar(energy);
        UpdateEnergyColor(energy / maxEnergy);
    }

    public void AddEnergy(float val)
    {
        energy += val;
        energy = Mathf.Min(maxEnergy, energy);
        energyBar.UpdateBar(energy);
        UpdateEnergyColor(energy / maxEnergy);
    }

    public void SpendEnergy(float val)
    {
        energy -= val;
        energy = Mathf.Min(maxEnergy, energy);
        energyBar.UpdateBar(energy);

        UpdateEnergyColor(energy / maxEnergy);
        if (energy <= 0)
        {
            GameLogic.instance.GameOver();
        }
    }

    void UpdateEnergyColor(float fraction)
    {
        normalRenderer.material.SetColor("_EmissionColor", Color.white * fraction);
        cloakedRenderer.material.SetColor("_EmissionColor", Color.white * fraction);
    }

    IEnumerator shockRoutine()
    {
        yield return new WaitForSeconds(shockDelay);
        SetState(States.idle);
    }

    public void DrainOver()
    {
        SetState(States.idle);
        drainObject.SetActive(false);
    }

    public void StopDrain()
    {
        if (drainTarget != null)
        {
            drainTarget.OnDrainEnd();
        }

        SetState(States.idle);
        drainObject.SetActive(false);
    }

    public void StopMovement()
    {
        rb.velocity = Vector3.zero;
        if (runParticles.isPlaying)
        {
            runParticles.Stop();
        }

        if (walkParticles.isPlaying)
        {
            walkParticles.Stop();
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (GameLogic.instance.gameState != GameLogic.GameStates.gameplay)
        {
            return;
        }

        if (state == States.attacking)
        {
            if (!Input.GetButton("Drain"))
            {
                StopDrain();
            }
        }

        if (canDrain && Input.GetButtonDown("Drain") && (state == States.idle || state == States.moving))
        {
            StopMovement();
            drainObject.SetActive(true);
            SetState(States.attacking);
            RaycastHit hit;
            if (Physics.Raycast(transform.position, transform.forward, out hit, drainRange, enemyLayerMask))
            {
                Debug.Log("Hit " + hit.transform.gameObject.name);
                AIAgent agent = hit.transform.gameObject.GetComponent<AIAgent>();
                if (agent != null && agent.aiSight.sightState != AISight.SightStates.seeingEnemy)
                {
                    agent.OnDrainStart();
                    Debug.Log("Draining ");
                    // drainObject.SetActive(true);
                    // SetState(States.attacking);
                    drainTarget = agent;
                }

            }
            else
            {
                Debug.Log("No hit");
            }
        }

        if (canShock && energy >= shockCost && Input.GetButtonDown("Shock") &&
            (state == States.idle || state == States.moving))
        {

            SpendEnergy(shockCost);
            threadController.moving = false;
            SetState(States.shocking);
            StopMovement();
            GameObject thisShockObject = GameObject.Instantiate(shockObject);
            thisShockObject.transform.position = transform.position;
            StartCoroutine(shockRoutine());

        }

        if (canShoot && energy >= _playerShootData.BulletEnergyCost && Input.GetButtonDown(_playerShootData.BtnName) &&
            (state == States.idle || state == States.moving))
        {
            SpendEnergy(_playerShootData.BulletEnergyCost);
            Fire();
        }

        if (canFloat && Input.GetButtonDown("Float") && (state == States.idle || state == States.moving || state == States.floating))
        {
            if (floatRobotCoroutine != null)
            {
                StopCoroutine(floatRobotCoroutine);
            }
            
            SetState(States.floating);
            threadController.moving = false;
            rb.velocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
            floatRobotCoroutine = StartCoroutine(FloatRobot(floatYAmount + initialYPosition, OnUpdate: (value) =>
            {
                OnFloatApplySoundPitch(value, finalSoundPitch, initialSoundPitch);
                OnFloatApplyParticleFX(value, finalParticleSpeed, initialParticleSpeed,finalParticleSize, initialParticleSize);
            }));
            audioSource.clip = floatingSFX;
            audioSource.loop = true; 
            audioSource.Play();
            floatingParticles.Play();
            floated = true;
        }
        else
        {
            if (floated && !Input.GetButton("Float"))
            {
                if (floatRobotCoroutine != null)
                {
                    StopCoroutine(floatRobotCoroutine);
                }
                
                floatRobotCoroutine = StartCoroutine(FloatRobot(initialYPosition, () =>
                {
                    SetState(States.idle);
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    floatingParticles.Stop();
                    audioSource.Stop();
                    audioSource.loop = false;
                    audioSource.clip = null;
                }, value =>
                {
                    OnFloatApplySoundPitch(value, initialSoundPitch, finalSoundPitch);
                    OnFloatApplyParticleFX(value, initialParticleSpeed, finalParticleSpeed, initialParticleSize, finalParticleSize);
                }));
                floated = false;
            }
        }

        if (canCloak && Input.GetButtonDown("Cloak") && (state == States.idle || state == States.moving))
        {
            normalModel.SetActive(false);
            cloakedModel.SetActive(true);
            cloaked = true;
            rb.velocity = Vector3.zero;
            threadController.moving = false;
        }
        else
        {
            if (cloaked && !Input.GetButton("Cloak"))
            {
                cloaked = false;
                normalModel.SetActive(true);
                cloakedModel.SetActive(false);
            }
        }

        if (state == States.idle && !cloaked)
        {
            moving = false;
            inputVector = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
            inputVector = Vector3.ClampMagnitude(inputVector, 1.0f);
            float camRotation = cam.transform.rotation.eulerAngles.y;
            inputVector = getMovementDirection(inputVector, camRotation);

            //rb.velocity = new Vector3(Mathf.Max(0.0f,rb.velocity.x * (1.0f - Time.deltaTime)), rb.velocity.y, Mathf.Max(0.0f, rb.velocity.z * (1.0f - Time.deltaTime)));
            rb.velocity = new Vector3(0, rb.velocity.y, 0);


            if (Input.GetButton("Run"))
            {
                running = true;
                threadController.speed = threadRunSpeed;
                threadController.audioSource.pitch = 1.25f;
            }
            else
            {
                running = false;
                threadController.speed = threadWalkSpeed;
                threadController.audioSource.pitch = 1.0f;
            }

            if (inputVector.magnitude > 0.1f)
            {
                threadController.moving = true;

                if (!running)
                {
                    if (runParticles.isPlaying)
                    {
                        runParticles.Stop();
                    }

                    if (!walkParticles.isPlaying)
                    {
                        walkParticles.Play();
                    }

                }
                else
                {
                    if (walkParticles.isPlaying)
                    {
                        walkParticles.Stop();
                    }

                    if (!runParticles.isPlaying)
                    {
                        runParticles.Play();
                    }

                }


                moving = true;
                applyMovement(inputVector);
                rotate(inputVector);
            }
            else
            {
                if (walkParticles.isPlaying)
                {
                    walkParticles.Stop();
                }

                if (runParticles.isPlaying)
                {
                    runParticles.Stop();
                }

                threadController.moving = false;
            }
        }

        if (enableEnergyDrain)
        {
            float energyDrain = energyDrainSpeed * Time.deltaTime;

            if (energy <= maxEnergy * lowEnergyFraction)
            {
                energyDrain = energyDrain * lowEnergyMultiplier;
            }

            if (running)
            {
                energyDrain = energyDrain * runningEnergyMultiplier;
            }

            if (cloaked)
            {
                energyDrain = energyDrain * cloakingEnergyMultiplier;
            }
            
            if(floated)
            {
                energyDrain = energyDrain * cloakingEnergyMultiplier;
            }
            
            if (!moving)
            {
                energyDrain = energyDrain * standingEnergyMultiplier;
            }

            if (state == States.attacking)
            {
                if (drainTarget == null)
                {
                    energyDrain = energyDrain * drainingEnergyMultiplier;
                }
                else
                {
                    energyDrain = 0;
                }

            }

            energy -= energyDrain;
            energyBar.UpdateBar(energy);
            UpdateEnergyColor(energy / maxEnergy);
            if (energy <= 0)
            {
                GameLogic.instance.GameOver();
            }
        }

    }

    private bool IsUnderFloor()
    {
        Ray ray = new Ray(transform.position, Vector3.down);
        Physics.SphereCast(ray, 0.72f, out RaycastHit hitInfo, floatYAmount + 1, gameObject.layer);
        Debug.Log(hitInfo.collider.gameObject.name);
        return true;
    }

    private void OnFloatApplySoundPitch(float lerpValue, float sizeUpperBound, float sizeLowerBound)
    {
        var lerpedPitch = Mathf.Lerp(sizeLowerBound, sizeUpperBound, lerpValue);
        audioSource.pitch = lerpedPitch;
    }

    private void OnFloatApplyParticleFX(float lerpvalue, float speedUpperBound, float speedLowerBound, float sizeUpperBound, float sizeLowerBound)
    {
        var lerpedSpeed = Mathf.Lerp(speedLowerBound, speedUpperBound, lerpvalue);
        var lerpedSize = Mathf.Lerp(sizeLowerBound, sizeUpperBound, lerpvalue);
        var floatingParticlesMain = floatingParticles.main;
        floatingParticlesMain.simulationSpeed = lerpedSpeed;
        floatingParticlesMain.startSize = lerpedSize;
    }
    
    private IEnumerator FloatRobot(float yDestination, Action onFinish = null, Action<float> OnUpdate = null)
    {
        var initialY = transform.position.y;
        var finalY = yDestination;
        var currentPosition = transform.position;
        var animationTime = GetAnimationTime(yDestination != initialYPosition);
        var currentAnimationTime = 0.0f;
        while (animationTime > currentAnimationTime)
        {
            var inversedLerpTime = Mathf.InverseLerp(0, animationTime, currentAnimationTime);
            var lerpedY = Mathf.Lerp(initialY, finalY, inversedLerpTime);
            currentPosition.y = lerpedY;
            transform.position = currentPosition;
            currentAnimationTime += Time.deltaTime;
            OnUpdate?.Invoke(inversedLerpTime);
            yield return new WaitForEndOfFrame();
        }
        onFinish?.Invoke();
        floatRobotCoroutine = null;
    }

    private float GetAnimationTime(bool direction)
    {
        var finalPoint = direction ? initialYPosition + floatYAmount : initialYPosition;
        var yDistance = Mathf.Abs(transform.position.y - finalPoint);
        var inversedLerpDistance = Mathf.InverseLerp(0, floatYAmount, yDistance);
        return Mathf.Lerp(0, floatAnimationTime, inversedLerpDistance);
    }

    public override void DealDamage(float val)
    {
        DealDamage(val, AudioManager.getInstance().playerHit);
        SpendEnergy(val);
    }

    public Vector3 getMovementDirection(Vector3 direction, float cameraAngle = 0)
    {
        float sin = Mathf.Sin((Mathf.PI / 180.0f) * cameraAngle);
        float cos = Mathf.Cos((Mathf.PI / 180.0f) * cameraAngle);

        float resultX = cos * direction.x + sin * direction.z;
        float resultZ = cos * direction.z - sin * direction.x;

        Vector3 result = new Vector3(resultX,0, resultZ);
        return result;
    }

    public void applyMovement(Vector3 direction)
    {
        float speedToUse = maxSpeed;

        if (running)
        {
            speedToUse = speedToUse * runningSpeedMultiplier;
        }

        Vector3 newSpeed = Vector3.zero;
        if (infiniteAcceleration && direction.magnitude > 0.1f)
        {
            newSpeed = speedToUse * new Vector3(direction.x, 0, direction.z);
        }
        else {
            newSpeed = Vector3.Lerp(speed, speedToUse * new Vector3(direction.x, 0, direction.z), acceleration * Time.deltaTime);
        }

        maxSpeedFraction = newSpeed.magnitude / maxSpeed;

        newSpeed = new Vector3(newSpeed.x, rb.velocity.y, newSpeed.z);

        speed = newSpeed;

        


        if (rb == null)
        {
            transform.position += Time.deltaTime * newSpeed;
        }
        else {
            rb.velocity = newSpeed;

        }
    }

    public void rotate(Vector3 direction3d)
    {
        Vector2 direction = new Vector2(direction3d.x, direction3d.z);
        if (Vector3.Cross(new Vector3(direction.x, direction.y, 0), new Vector3(0, 1, 0)).z > 0)
        {
            if (instanteRotate)
            {
                transform.rotation = Quaternion.AngleAxis(Vector2.Angle(Vector2.up, direction), Vector3.up);

            }
            else {
                rotationDestination = Quaternion.AngleAxis(Vector2.Angle(Vector2.up, direction), Vector3.up);

                angleDiff = transform.rotation.eulerAngles.y - rotationDestination.eulerAngles.y;

                if (angleDiff > 190)
                {
                    angleDiff -= 360;
                }

                transform.rotation = Quaternion.Lerp(transform.rotation, rotationDestination, rotateSpeed * Time.deltaTime);
            }
        }
        else {

            if (instanteRotate)
            {
                transform.rotation = Quaternion.AngleAxis(-Vector2.Angle(Vector2.up, direction), Vector3.up);
            }
            else {
                rotationDestination = Quaternion.AngleAxis(-Vector2.Angle(Vector2.up, direction), Vector3.up);

                angleDiff = transform.rotation.eulerAngles.y - rotationDestination.eulerAngles.y;

                if (angleDiff > 190)
                {
                    angleDiff -= 360;
                }
                transform.rotation = Quaternion.Lerp(transform.rotation, rotationDestination, rotateSpeed * Time.deltaTime);
            }
        }
    }

    public void Kill()
    {

    }
}
