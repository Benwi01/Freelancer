using System;
using Unity.Netcode;

namespace Freelancer
{
    /// <summary>What a roulette bet is on.</summary>
    public enum RouletteBetKind : byte
    {
        Red,
        Black,
        Odd,
        Even,
        Low,    // 1-18
        High,   // 19-36
        Straight, // one number
    }

    /// <summary>One chip stack on the table, synced to everyone.</summary>
    public struct RouletteBet : INetworkSerializable, IEquatable<RouletteBet>
    {
        public ulong ClientId;
        public RouletteBetKind Kind;
        public byte Number; // Straight only
        public int Amount;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Kind);
            serializer.SerializeValue(ref Number);
            serializer.SerializeValue(ref Amount);
        }

        public bool Equals(RouletteBet o) => ClientId == o.ClientId && Kind == o.Kind && Number == o.Number && Amount == o.Amount;
        public override int GetHashCode() => HashCode.Combine(ClientId, Kind, Number, Amount);
    }

    /// <summary>European roulette: one zero, 37 pockets. Even-money bets pay 1:1, a single number 35:1.</summary>
    public static class RouletteRules
    {
        /// <summary>Pocket order around the wheel, clockwise from the zero.</summary>
        public static readonly int[] WheelOrder =
        {
            0, 32, 15, 19, 4, 21, 2, 25, 17, 34, 6, 27, 13, 36, 11, 30, 8, 23, 10,
            5, 24, 16, 33, 1, 20, 14, 31, 9, 22, 18, 29, 7, 28, 12, 35, 3, 26,
        };

        static readonly bool[] k_Red = BuildReds();

        static bool[] BuildReds()
        {
            var red = new bool[37];
            foreach (int n in new[] { 1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36 }) red[n] = true;
            return red;
        }

        public static bool IsRed(int n) => n > 0 && n <= 36 && k_Red[n];
        public static bool IsBlack(int n) => n > 0 && n <= 36 && !k_Red[n];

        /// <summary>Angle (degrees) of a number's pocket on the wheel.</summary>
        public static float PocketAngle(int number) => Array.IndexOf(WheelOrder, number) * 360f / WheelOrder.Length;

        /// <summary>What comes back for a bet (stake included), 0 if it lost.</summary>
        public static int Returned(RouletteBet bet, int result)
        {
            bool win = bet.Kind switch
            {
                RouletteBetKind.Red => IsRed(result),
                RouletteBetKind.Black => IsBlack(result),
                RouletteBetKind.Odd => result > 0 && result % 2 == 1,
                RouletteBetKind.Even => result > 0 && result % 2 == 0,
                RouletteBetKind.Low => result >= 1 && result <= 18,
                RouletteBetKind.High => result >= 19 && result <= 36,
                RouletteBetKind.Straight => result == bet.Number,
                _ => false,
            };
            if (!win) return 0;
            return bet.Kind == RouletteBetKind.Straight ? bet.Amount * 36 : bet.Amount * 2;
        }

        public static string Name(RouletteBetKind kind, int number) => kind switch
        {
            RouletteBetKind.Red => "RED",
            RouletteBetKind.Black => "BLACK",
            RouletteBetKind.Odd => "ODD",
            RouletteBetKind.Even => "EVEN",
            RouletteBetKind.Low => "1-18",
            RouletteBetKind.High => "19-36",
            _ => "#" + number,
        };

        /// <summary>"17 BLACK", "0 GREEN".</summary>
        public static string Describe(int n) => n == 0 ? "0 GREEN" : $"{n} {(IsRed(n) ? "RED" : "BLACK")}";

        /// <summary>Rich-text colour for a number.</summary>
        public static string Hex(int n) => n == 0 ? "#3DBE6A" : IsRed(n) ? "#FF4D4D" : "#E8E8E8";
    }
}
