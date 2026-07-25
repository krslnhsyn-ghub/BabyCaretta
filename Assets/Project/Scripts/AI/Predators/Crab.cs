using UnityEngine;
using Game.Interaction;

public class Crab : MonoBehaviour, IStunnable
{
    public void Stun(float duration)
    {
        Debug.Log($"{gameObject.name}: {duration} saniye sersemledi.");
        // Gerçek tepki (donma, kaçma, animasyon) sonra eklenecek
    }
}