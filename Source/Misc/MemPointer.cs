using System.Runtime.CompilerServices;

namespace eft_dma_radar
{
    /// <summary>
    /// Represents a 64-Bit Unsigned Pointer Address.
    /// </summary>
    public readonly struct MemPointer
    {
        public static implicit operator MemPointer(ulong x) => x;
        public static implicit operator ulong(MemPointer x) => x.Va;
        /// <summary>
        /// Virtual Address of this Pointer.
        /// </summary>
        public readonly ulong Va;

        /// <summary>
        /// Validates the Pointer.
        /// </summary>
        /// <exception cref="NullPtrException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Validate()
        {
            if (Va == 0x0)
                throw new NullPtrException();
        }

        /// <summary>
        /// Convert to string format.
        /// </summary>
        /// <returns>Pointer Address represented in Upper-Case Hex.</returns>
        public readonly override string ToString() => Va.ToString("X");
    }
}
