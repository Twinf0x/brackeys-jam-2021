﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public enum PlayerActionState
{
    NoAction,
    Throwing,
    CallingBack
}

public class PlayerController : MonoBehaviour
{
    #region Inspector
    [Header("Pause Menu")]
    [SerializeField] private PauseMenuController pauseController;
    [Header("Movement")]
    [SerializeField] private CharacterController controller;
    [SerializeField] private float speed = 3f;
    [SerializeField] private float gravity = -9.81f;
    [Header("Targeted Actions")]
    [SerializeField] private GameObject targetIndicator;
    [SerializeField] private LayerMask targetLayerMask;
    [SerializeField] private float minThrowDistance = 4f;
    [SerializeField] private float maxThrowDistance = 15f;
    [Header("Throwing")]
    [SerializeField] private float timeBetweenThrows = 0.5f;
    [Header("Calling back")]
    [SerializeField] private GameObject callIndicator;
    [SerializeField] private float maxCallRange = 5f;
    [SerializeField] private float rangeGrowthPerSeconds = 5f;
    [Header("Blobs")]
    [SerializeField] private LayerMask blobLayerMask;
    [SerializeField] private Transform followerTarget;
    [SerializeField] private int maxFollowers = 20;
    [Header("Visuals")]
    [SerializeField] private SpriteRenderer playerSprite;
    [SerializeField] private Animator playerAnimator;
    #endregion

    // Current player state
    private PlayerActionState state;
    private Coroutine currentActionRoutine;

    // Variables for determining where the user clicked/tapped
    private Camera playerCamera;
    private Ray targetRay;
    private RaycastHit targetHit;

    // Movement
    private Vector3 movementDirection;

    // Calling blobs back
    RaycastHit[] nearbyBlobs;

    // Throwing blobs
    private Vector3 throwTargetPosition;
    public List<BlobBase> followingBlobs;
    private BlobType currentBlobType = BlobType.Rock;

    public BlobType CurrentBlobType
    {
        get { return currentBlobType; }
        set
        {
            if (currentBlobType == value)
            {
                return;
            }

            currentBlobType = value;
            // TODO send back all current blobs
        }
    }

    public int MaxFollowers { get { return maxFollowers; } }
    public int CurrentFollowerAmount { get { return followingBlobs.Count; } }

    private bool isInMenu;
    public bool IsInMenu 
    {
        get
        {
            return isInMenu;
        }

        set
        {
            if (value)
            {
                movementDirection = Vector3.zero;
            }

            isInMenu = value;
        }
    }

    #region Unity Methods
    private void Awake()
    {
        Initialize();
    }

    private void Update()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            pauseController.Pause();
        }
        Move();
        UpdateTargetIndicator();
    }
    #endregion

    #region Input Events
    public void OnMovement(InputAction.CallbackContext context)
    {
        if (IsInMenu)
        {
            return;
        }

        Vector2 inputMovement = context.ReadValue<Vector2>();
        movementDirection = new Vector3(inputMovement.x, 0f, inputMovement.y);
    }

    public void OnTargetedAction(InputAction.CallbackContext context)
    {
        if (IsInMenu)
        {
            return;
        }

        // TODO: Here we should also somehow determine the screen position for a touch...
        if (context.action.phase == InputActionPhase.Started)
        {
            HandleTargetedActionStart(Mouse.current.position.ReadValue());
        }
        else if (context.action.phase == InputActionPhase.Canceled)
        {
            StopCurrentAction();
        }
        
    }
    #endregion

    private void Initialize()
    {
        playerCamera = Camera.main;
        state = PlayerActionState.NoAction;
        followingBlobs = new List<BlobBase>();
    }

    private void Move()
    {
        if (state != PlayerActionState.NoAction || movementDirection.magnitude < 0.1f)
        {
            playerAnimator.SetBool("isWalking", false);
            return;
        }

        playerAnimator.SetBool("isWalking", true);
        Vector3 movement = movementDirection * speed * Time.deltaTime;
        movement.y = gravity;
        controller.Move(movement);

        if(movementDirection.x < 0) {
            playerSprite.flipX = true;
        } else {
            playerSprite.flipX = false;
        }

        float targetAngle = Mathf.Atan2(movementDirection.x, movementDirection.z) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, targetAngle, 0f);
    }

    private void UpdateTargetIndicator()
    {
        targetRay = playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(targetRay, out targetHit, 1000f, targetLayerMask))
        {
            throwTargetPosition = Vector3.negativeInfinity;
            targetIndicator.SetActive(false);
            return;
        }

        throwTargetPosition = targetHit.point;
        float targetDistance = Vector3.Distance(transform.position, throwTargetPosition);
        if (targetDistance < minThrowDistance)
        {
            throwTargetPosition = Vector3.negativeInfinity;
            // TODO Maybe change indicator to show that you can call blobs back from that mouse position?
            targetIndicator.SetActive(false);
            return;
        }

        if (targetDistance > maxThrowDistance)
        {
            Vector3 throwDirection = (throwTargetPosition - transform.position).normalized;
            throwTargetPosition = transform.position + (throwDirection * maxThrowDistance);
        }

        targetIndicator.SetActive(true);
        targetIndicator.transform.position = throwTargetPosition;
    }

    /// <summary>
    /// Determines the target of the action and calls either the throw- or callback-method
    /// </summary>
    private void HandleTargetedActionStart(Vector2 screenTarget)
    {
        targetRay = playerCamera.ScreenPointToRay(screenTarget);
        if (!Physics.Raycast(targetRay, out targetHit, 1000f, targetLayerMask))
        {
            return;
        }

        float targetDistance = Vector3.Distance(transform.position, targetHit.point);
        if (targetDistance < minThrowDistance)
        {
            StartCallingBlobsBack();
            return;
        }

        StartThrowingBlobs();
    }

    private void StopCurrentAction()
    {
        if (currentActionRoutine != null)
        {
            StopCoroutine(currentActionRoutine);
            currentActionRoutine = null;
        }

        switch (state)
        {
            case PlayerActionState.CallingBack:
                callIndicator.SetActive(false);
                break;
        }
        state = PlayerActionState.NoAction;
    }

    private void StartCallingBlobsBack()
    {
        StopCurrentAction();
        state = PlayerActionState.CallingBack;
        callIndicator.SetActive(true);
        playerAnimator.SetTrigger("Recall");
        currentActionRoutine = StartCoroutine(CallBlobsBack());
    }

    private IEnumerator CallBlobsBack()
    {
        float callRange = 0f;

        while (true)
        {
            callRange += rangeGrowthPerSeconds * Time.deltaTime;
            callRange = Mathf.Clamp(callRange, 0f, maxCallRange);

            nearbyBlobs = Physics.SphereCastAll(transform.position, callRange, Vector3.up, 0f, blobLayerMask);
            foreach(var blobHit in nearbyBlobs)
            {
                BlobBase blob = blobHit.collider.GetComponent<BlobBase>();
                if (blob == null || !blob.CanBeCalled())
                {
                    continue;
                }

                if (followingBlobs.Count < maxFollowers)
                {
                    AddBlobToFollowers(blob);
                }
            }

            callIndicator.transform.localScale = new Vector3(callRange * 2, callRange * 2, callRange * 2);
            yield return null;
        }
    }

    public void AddBlobToFollowers(BlobBase blob)
    {
        blob.StartFollowing(followerTarget);
        followingBlobs.Add(blob);
        blob.controller = this;
    }

    public void RemoveBlobFromFollowers(BlobBase blob)
    {
        followingBlobs.Remove(blob);
    }

    public void SendAllBlobsToTube(PneumaticTube tube)
    {
        while(CurrentFollowerAmount > 0)
        {
            SendBlobToTube(tube);
        }
    }

    public void SendBlobToTube(PneumaticTube tube)
    {
        if (followingBlobs.Count <= 0)
        {
            return;
        }

        BlobBase blob = followingBlobs.First();
        followingBlobs.Remove(blob);

        blob.StartFollowing(tube.transform);
        blob.State = BlobState.GoingToTube;
    }

    private void StartThrowingBlobs()
    {
        StopCurrentAction();
        state = PlayerActionState.Throwing;
        currentActionRoutine = StartCoroutine(ThrowBlobs());
    }

    private IEnumerator ThrowBlobs()
    {
        while(followingBlobs.Count > 0)
        {
            if (throwTargetPosition == Vector3.negativeInfinity)
            {
                yield return null;
            }

            playerAnimator.SetTrigger("Fire");

            BlobBase blob = followingBlobs.First();
            followingBlobs.Remove(blob);

            blob.GetThrown(transform.position, throwTargetPosition);

            yield return new WaitForSeconds(timeBetweenThrows);
        }

        StopCurrentAction();
    }
}
