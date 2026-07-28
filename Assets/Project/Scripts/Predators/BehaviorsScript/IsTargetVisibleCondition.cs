using System;
using Unity.Behavior;
using UnityEngine;
using Game.Predators;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "Is Target Visible", story: "[Target] is not hidden", category: "Conditions", id: "c09aeadced1602a655309a55789dab49")]
public partial class IsTargetVisibleCondition : Condition
{
    [SerializeReference] public BlackboardVariable<Transform> Target;

    public override bool IsTrue()
    {
        if (Target.Value == null) return false;

        var predatorController = GameObject.GetComponent<PredatorController>();
        if (predatorController == null || predatorController.Target == null) return true;

        // Burrow'dayken (IsHidden) predator hedefi göremez - koşul false döner, Guard
        // geçmez, Hunt dalı başarısız olur, Try In Order Give Up dalına düşer.
        return !predatorController.Target.IsHidden;
    }

    public override void OnStart()
    {
    }

    public override void OnEnd()
    {
    }
}
