using System;
using Unity.Netcode;
using UnityEngine;

namespace Freelancer
{
    /// <summary>One player's grip on a GrabbableObject. Replicated so everyone can draw the beams.</summary>
    public struct GrabHolder : INetworkSerializable, IEquatable<GrabHolder>
    {
        public ulong ClientId;
        public Vector3 LocalAnchor;
        public float Distance;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref LocalAnchor);
            serializer.SerializeValue(ref Distance);
        }

        public bool Equals(GrabHolder other) =>
            ClientId == other.ClientId && LocalAnchor == other.LocalAnchor && Distance.Equals(other.Distance);

        public override bool Equals(object obj) => obj is GrabHolder other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(ClientId, LocalAnchor, Distance);
    }
}
