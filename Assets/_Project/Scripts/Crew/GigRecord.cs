using System;
using Unity.Collections;
using Unity.Netcode;

namespace Freelancer
{
    /// <summary>Best star rating on one gig, synced to everyone in the crew.</summary>
    public struct GigRecord : INetworkSerializable, IEquatable<GigRecord>
    {
        public FixedString64Bytes GigId;
        public byte Stars;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref GigId);
            serializer.SerializeValue(ref Stars);
        }

        public bool Equals(GigRecord other) => GigId.Equals(other.GigId) && Stars == other.Stars;
        public override int GetHashCode() => HashCode.Combine(GigId, Stars);
    }
}
