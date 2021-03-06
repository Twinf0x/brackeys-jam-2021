using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

public class CarryObject : Interactable
{
    [SerializeField] internal PneumaticTube carryTo;
    [SerializeField] internal UnityEvent onSuckedIntoTube;
    [SerializeField] internal NavMeshAgent agent;
    [SerializeField] internal Collider interactionCollider;
    [SerializeField] internal Transform meshTransform;
    [SerializeField] internal float carrySpeed = 4f;
    [SerializeField] internal Vector3 carryOffset;

    internal override void StartInteraction()
    {
        base.StartInteraction();
        meshTransform.position += carryOffset;
        agent.SetDestination(carryTo.transform.position);
        display.gameObject.SetActive(false);
    }

    internal override void StopInteraction()
    {
        base.StopInteraction();
        meshTransform.position -= carryOffset;
        agent.SetDestination(transform.position);
        display.gameObject.SetActive(true);
    }

    public override BlobState AssignBlob(BlobBase blob)
    {
        var state = base.AssignBlob(blob);
        blob.transform.SetParent(transform);
        blob.transform.position = transform.position + GetBlobOffset(blob);
        UpdateCarrySpeed();
        return state;
    }

    public override void RemoveBlob(BlobBase blob)
    {
        if (blob == null)
        {
            return;
        }
        base.RemoveBlob(blob);
        blob.transform.SetParent(null);
    }

    internal void UpdateCarrySpeed()
    {
        int numberOfAssigendBlobs = assignedBlobs.Count;
        if (numberOfAssigendBlobs < blobsNeeded)
        {
            agent.speed = 0f;
            return;
        }

        agent.speed = carrySpeed + ((numberOfAssigendBlobs - blobsNeeded) * 0.2f * carrySpeed);
    }

    public virtual void OnSuckedIntoTube()
    {
        interactionCollider.enabled = false;
        foreach(var blob in assignedBlobs.ToArray())
        {
            RemoveBlob(blob);
        }
        display.gameObject.SetActive(false);
        onSuckedIntoTube?.Invoke();
    }
}
