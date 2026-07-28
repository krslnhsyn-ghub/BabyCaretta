using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using Game.Predators;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Hold Until Released", story: "Hold Until Released", category: "Action", id: "032de04160b7a4776f28e7ee033ca019")]
public partial class HoldUntilReleasedAction : Action
{
    // Serialize edilmiyor, sadece bu çalışma boyunca (OnStart -> OnEnd) geçici önbellek.
    private PredatorController cachedController;

    protected override Status OnStart()
    {
        cachedController = GameObject.GetComponent<PredatorController>();
        GameObject.GetComponent<Animator>()?.SetBool("IsHolding", true);
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        // cachedController null ise (component eksikse) beklemeyi bırakıp başarısız say.
        if (cachedController == null) return Status.Failure;

        // Tutuyorken kendimizi (yengeci) atanan bacağa kilitleyelim ki görsel olarak
        // "yapışık" kalsın - kaplumbağa yürüse bile yengeç o bacağı takip etsin.
        if (cachedController.GrabPoint != null)
        {
            GameObject.transform.position = cachedController.GrabPoint.position;
        }

        // Hâlâ tutuyorsak beklemeye devam (Running). Kaçış başarılı olduğunda ya da
        // capture tetiklendiğinde PredatorController.IsRestrainingTurtle false olur -
        // o an bu node Success döner, Sequence biter, Repeat baştan başlar.
        if (!cachedController.IsRestrainingTurtle)
        {
            GameObject.GetComponent<Animator>()?.SetBool("IsHolding", false);
            return Status.Success;
        }

        return Status.Running;
    }

    protected override void OnEnd()
    {
    }
}

