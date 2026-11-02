using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Freelancer
{
    /// <summary>A full spare keg. Drag it to the keg spot under the beer tap to refill it.</summary>
    [RequireComponent(typeof(GrabbableObject))]
    public class BeerKeg : NetworkBehaviour
    {
        public static readonly List<BeerKeg> All = new();

        GrabbableObject m_Grab;

        public bool IsHeld => m_Grab.IsHeld;

        void Awake() => m_Grab = GetComponent<GrabbableObject>();
        public override void OnNetworkSpawn() => All.Add(this);
        public override void OnNetworkDespawn() => All.Remove(this);
    }
}
