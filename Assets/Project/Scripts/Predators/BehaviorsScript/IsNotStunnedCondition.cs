using System;
using Unity.Behavior;
using UnityEngine;
using Game.Predators;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "Is Not Stunned", story: "[Target] is not stunned", category: "Conditions", id: "f97e6073c4f9adcdcdb45f769d1bd6a6")]
public partial class IsNotStunnedCondition : Condition
{
    [SerializeReference] public BlackboardVariable<GameObject> Target;

    public override bool IsTrue()
    {
        if (Target.Value == null) return true;

        var predatorController = Target.Value.GetComponent<PredatorController>();
        if (predatorController == null) return true;

        return !predatorController.IsStunned;
    }

    public override void OnStart()
    {
    }

    public override void OnEnd()
    {
    }
}
