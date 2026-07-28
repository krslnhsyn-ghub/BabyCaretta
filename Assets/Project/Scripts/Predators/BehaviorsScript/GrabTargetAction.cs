using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using Game.Predators;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Grab Target", story: "Grab [Target]", category: "Action", id: "f77c5b5fd243de2304c3e7ca4014e8dd")]
public partial class GrabTargetAction : Action
{
    [SerializeReference] public BlackboardVariable<Transform> Target;

    protected override Status OnStart()
    {
        // Bu node'un bağlı olduğu (yengecin kendisi) GameObject'te PredatorController
        // olmalı - Grab Target'ı çağırıp anında sonucu (başarılı mı, reddedildi mi -
        // örn. stackable olmayan bir predator zaten tutuyorsa) döndürüyoruz.
        var predatorController = GameObject.GetComponent<PredatorController>();
        if (predatorController == null) return Status.Failure;

        predatorController.TryGrabTurtle();
        bool success = predatorController.IsRestrainingTurtle;

        if (success)
        {
            var animator = GameObject.GetComponent<Animator>();
            animator?.SetBool("IsMoving", false);
            animator?.SetTrigger("Grab");
        }

        return success ? Status.Success : Status.Failure;
    }

    protected override Status OnUpdate()
    {
        return Status.Success;
    }

    protected override void OnEnd()
    {
    }
}

