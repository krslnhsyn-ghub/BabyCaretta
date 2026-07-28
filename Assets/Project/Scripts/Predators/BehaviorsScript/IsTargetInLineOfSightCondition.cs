using System;
using Unity.Behavior;
using UnityEngine;
using Game.Predators;
using Game.Character;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "Is Target In Line Of Sight", story: "[Target] visible from [EyeHeight]m height (obstacles: [ObstructionLayers], self-ignore <[SelfIgnoreDistance]m) - not hidden and LOS clear", category: "Conditions", id: "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6")]
public partial class IsTargetInLineOfSightCondition : Condition
{
    [SerializeReference] public BlackboardVariable<Transform> Target;

    [Tooltip("How high above the predator's base to cast the ray from (eye level)")]
    [SerializeReference] public BlackboardVariable<float> EyeHeight = new BlackboardVariable<float> { Value = 0.5f };

    [Tooltip("Layers that block vision (walls, obstacles, etc.)")]
    [SerializeReference] public BlackboardVariable<int> ObstructionLayers = new BlackboardVariable<int> { Value = -1 }; // -1 = Everything

    [Tooltip("Additional offset to avoid hitting the predator's own collider")]
    [SerializeReference] public BlackboardVariable<float> SelfIgnoreDistance = new BlackboardVariable<float> { Value = 0.1f };

    [Tooltip("Enable debug visualization of line of sight checks in the scene view")]
    [SerializeField] private bool DebugVisualization = false;

    public override bool IsTrue()
    {
        if (Target.Value == null) return false;

        var predatorController = GameObject.GetComponent<PredatorController>();
        if (predatorController == null || predatorController.Target == null)
        {
            // If we can't get the predator controller or target data, fall back to basic check
            return true;
        }

        // First check if target is hidden (burrowed/in shell) - if so, definitely not visible
        if (predatorController.Target.IsHidden)
        {
            return false;
        }

        // Perform line-of-sight check using raycast
        return HasLineOfSight(predatorController, Target.Value);
    }

    private bool HasLineOfSight(PredatorController predatorController, Transform targetTransform)
    {
        // Get predator's position (add eye height to avoid ground level obstructions)
        Vector3 predatorPos = predatorController.transform.position;
        Vector3 predatorEyePos = predatorPos + Vector3.up * EyeHeight.Value;

        // Get target position - try to get a more precise point if possible
        Vector3 targetPos;

        // If target is a TurtlePredatorTarget, try to get a specific body part
        if (predatorController.Target != null)
        {
            // Try to get the TurtleController component to access the turtle's transform
            TurtleController turtleController = predatorController.Target.GetComponent<TurtleController>();
            if (turtleController != null)
            {
                // Aim for mid-body height
                targetPos = turtleController.transform.position + Vector3.up * 0.5f;
            }
            else
            {
                // Fallback to target's transform position
                targetPos = targetTransform.position;
            }
        }
        else
        {
            targetPos = targetTransform.position;
        }

        // Calculate direction and distance
        Vector3 direction = targetPos - predatorEyePos;
        float distance = direction.magnitude;

        // Avoid checking if target is too close (would hit own collider)
        if (distance < SelfIgnoreDistance.Value)
        {
            // Still draw debug line for very close targets
            if (DebugVisualization)
            {
                Debug.DrawLine(predatorEyePos, targetPos, Color.green);
                DrawDebugPoint(predatorEyePos, Color.green);
            }
            return true;
        }

        // Normalize direction for raycast
        direction.Normalize();

        // Perform raycast to check for obstructions
        // We use a sphere cast to account for character size and avoid hitting thin objects
        RaycastHit hit;
        bool isBlocked = Physics.SphereCast(predatorEyePos, 0.1f, direction, out hit, distance, (LayerMask)ObstructionLayers.Value);

        // Debug visualization
        if (DebugVisualization)
        {
            if (isBlocked)
            {
                // Draw red line showing blocked path
                Debug.DrawLine(predatorEyePos, hit.point, Color.red);
                Debug.DrawRay(hit.point, hit.normal * 0.2f, Color.red); // Show normal at hit point
                DrawDebugPoint(predatorEyePos, Color.red);

                // Draw line from hit point to target (showing what was blocked)
                if (hit.transform != targetTransform && !IsPartOfTarget(hit.transform, targetTransform))
                {
                    Debug.DrawLine(hit.point, targetPos, Color.yellow);
                }
            }
            else
            {
                // Draw green line showing clear path
                Debug.DrawLine(predatorEyePos, targetPos, Color.green);
                DrawDebugPoint(predatorEyePos, Color.green);
                DrawDebugPoint(targetPos, Color.green);
            }
        }

        // Check if we hit something that's not the target itself
        if (isBlocked)
        {
            if (hit.transform != targetTransform && !IsPartOfTarget(hit.transform, targetTransform))
            {
                // Something is blocking the view
                return false;
            }
            // If we hit the target itself or part of it, it's still visible
        }

        // Line of sight is clear
        return true;
    }

    private void DrawDebugPoint(Vector3 position, Color color)
    {
        // Draw a simple cross to represent a point
        float size = 0.05f;
        Debug.DrawLine(position + Vector3.left * size, position + Vector3.right * size, color);
        Debug.DrawLine(position + Vector3.up * size, position + Vector3.down * size, color);
        Debug.DrawLine(position + Vector3.forward * size, position + Vector3.back * size, color);
    }

    private bool IsPartOfTarget(Transform hitTransform, Transform targetTransform)
    {
        // Check if the hit object is part of the target (e.g., turtle's limb, shell)
        Transform current = hitTransform;
        while (current != null)
        {
            if (current == targetTransform)
            {
                return true;
            }
            current = current.parent;
        }
        return false;
    }

    public override void OnStart()
    {
    }

    public override void OnEnd()
    {
    }
}