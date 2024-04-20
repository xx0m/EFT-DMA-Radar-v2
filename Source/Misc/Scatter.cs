using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;

namespace eft_dma_radar
{
    #region ReadScatter
    public interface IScatterEntry
    {
        /// <summary>
        /// Entry Index.
        /// </summary>
        int Index { get; init; }
        /// <summary>
        /// Entry ID.
        /// </summary>
        int Id { get; init; }
        /// <summary>
        /// Can be a ulong or another ScatterReadEntry.
        /// </summary>
        object Addr { get; set; }
        /// <summary>
        /// Offset to the Base Address.
        /// </summary>
        uint Offset { get; init; }
        /// <summary>
        /// Defines the type based on <typeparamref name="T"/>
        /// </summary>
        Type Type { get; }
        /// <summary>
        /// Can be an int32 or another ScatterReadEntry.
        /// </summary>
        object Size { get; set; }
        /// <summary>
        /// True if the Scatter Read has failed.
        /// </summary>
        bool IsFailed { get; set; }

        /// <summary>
        /// Sets the Result for this Scatter Read.
        /// </summary>
        /// <param name="buffer">Raw memory buffer for this read.</param>
        void SetResult(byte[] buffer);

        /// <summary>
        /// Parses the address to read for this Scatter Read.
        /// Sets the Addr property for the object.
        /// </summary>
        /// <returns>Virtual address to read.</returns>
        ulong ParseAddr();

        /// <summary>
        /// Parses the number of bytes to read for this Scatter Read.
        /// Sets the Size property for the object.
        /// </summary>
        /// <returns>Size of read.</returns>
        int ParseSize();

        /// <summary>
        /// Tries to return the Scatter Read Result.
        /// </summary>
        /// <typeparam name="TOut">Type to return.</typeparam>
        /// <param name="result">Result to populate.</param>
        /// <returns>True if successful, otherwise False.</returns>
        bool TryGetResult<TOut>(out TOut result);
    }
    public class ScatterReadMap
    {
        protected List<ScatterReadRound> Rounds { get; } = new();
        protected readonly Dictionary<int, Dictionary<int, IScatterEntry>> _results = new();
        /// <summary>
        /// Contains results from Scatter Read after Execute() is performed. First key is Index, Second Key ID.
        /// </summary>
        public IReadOnlyDictionary<int, Dictionary<int, IScatterEntry>> Results => _results;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="indexCount">Number of indexes in the scatter read loop.</param>
        public ScatterReadMap(int indexCount)
        {
            for (int i = 0; i < indexCount; i++)
            {
                _results.Add(i, new());
            }
        }

        /// <summary>
        /// Executes Scatter Read operation as defined per the map.
        /// </summary>
        public void Execute()
        {
            foreach (var round in Rounds)
            {
                round.Run();
            }
        }

        /// <summary>
        /// (Base)
        /// Add scatter read rounds to the operation. Each round is a successive scatter read, you may need multiple
        /// rounds if you have reads dependent on earlier scatter reads result(s).
        /// </summary>
        /// <param name="pid">Process ID to read from.</param>
        /// <param name="useCache">Use caching for this read (recommended).</param>
        /// <returns></returns>
        public virtual ScatterReadRound AddRound()
        {
            var round = new ScatterReadRound(_results);
            Rounds.Add(round);
            return round;
        }
    }

    /// <summary>
    /// Defines a Scatter Read Round. Each round will execute a single scatter read. If you have reads that
    /// are dependent on previous reads (chained pointers for example), you may need multiple rounds.
    /// </summary>
    public class ScatterReadRound
    {
        protected Dictionary<int, Dictionary<int, IScatterEntry>> Results { get; }
        protected List<IScatterEntry> Entries { get; } = new();

        /// <summary>
        /// Do not use this constructor directly. Call .AddRound() from the ScatterReadMap.
        /// </summary>
        public ScatterReadRound(Dictionary<int, Dictionary<int, IScatterEntry>> results)
        {
            Results = results;
        }

        /// <summary>
        /// (Base)
        /// Adds a single Scatter Read 
        /// </summary>
        /// <param name="index">For loop index this is associated with.</param>
        /// <param name="id">Random ID number to identify the entry's purpose.</param>
        /// <param name="addr">Address to read from (you can pass a ScatterReadEntry from an earlier round, 
        /// and it will use the result).</param>
        /// <param name="size">Size of oject to read (ONLY for reference types, value types get size from
        /// Type). You canc pass a ScatterReadEntry from an earlier round and it will use the Result.</param>
        /// <param name="offset">Optional offset to add to address (usually in the event that you pass a
        /// ScatterReadEntry to the Addr field).</param>
        /// <returns>The newly created ScatterReadEntry.</returns>
        public virtual ScatterReadEntry<T> AddEntry<T>(int index, int id, object addr, object size = null, uint offset = 0x0)
        {
            var entry = new ScatterReadEntry<T>()
            {
                Index = index,
                Id = id,
                Addr = addr,
                Size = size,
                Offset = offset
            };
            Results[index].Add(id, entry);
            Entries.Add(entry);
            return entry;
        }

        /// <summary>
        /// ** Internal API use only do not use **
        /// </summary>
        internal void Run()
        {
            var entriesSpan = CollectionsMarshal.AsSpan(Entries);
            Memory.ReadScatter(entriesSpan);
        }
    }

    public class ScatterReadEntry<T> : IScatterEntry
    {
        #region Properties

        /// <summary>
        /// Entry Index.
        /// </summary>
        public int Index { get; init; }
        /// <summary>
        /// Entry ID.
        /// </summary>
        public int Id { get; init; }
        /// <summary>
        /// Can be a ulong or another ScatterReadEntry.
        /// </summary>
        public object Addr { get; set; }
        /// <summary>
        /// Offset to the Base Address.
        /// </summary>
        public uint Offset { get; init; }
        /// <summary>
        /// Defines the type based on <typeparamref name="T"/>
        /// </summary>
        public Type Type { get; } = typeof(T);
        /// <summary>
        /// Can be an int32 or another ScatterReadEntry.
        /// </summary>
        public object Size { get; set; }
        /// <summary>
        /// True if the Scatter Read has failed.
        /// </summary>
        public bool IsFailed { get; set; }
        /// <summary>
        /// Scatter Read Result.
        /// </summary>
        protected T Result { get; set; }
        #endregion

        #region Read Prep
        /// <summary>
        /// Parses the address to read for this Scatter Read.
        /// Sets the Addr property for the object.
        /// </summary>
        /// <returns>Virtual address to read.</returns>
        public ulong ParseAddr()
        {
            ulong addr = 0x0;
            if (this.Addr is ulong p1)
                addr = p1;
            else if (this.Addr is MemPointer p2)
                addr = p2;
            else if (this.Addr is IScatterEntry ptrObj) // Check if the addr references another ScatterRead Result
            {
                if (ptrObj.TryGetResult<MemPointer>(out var p3))
                    addr = p3;
                else
                    ptrObj.TryGetResult(out addr);
            }
            this.Addr = addr;
            return addr;
        }

        /// <summary>
        /// (Base)
        /// Parses the number of bytes to read for this Scatter Read.
        /// Sets the Size property for the object.
        /// Derived classes should call upon this Base.
        /// </summary>
        /// <returns>Size of read.</returns>
        public virtual int ParseSize()
        {
            int size = 0;
            if (this.Type.IsValueType)
                size = Unsafe.SizeOf<T>();
            else if (this.Size is int sizeInt)
                size = sizeInt;
            else if (this.Size is IScatterEntry sizeObj) // Check if the size references another ScatterRead Result
                sizeObj.TryGetResult(out size);
            this.Size = size;
            return size;
        }
        #endregion

        #region Set Result
        /// <summary>
        /// Sets the Result for this Scatter Read.
        /// </summary>
        /// <param name="buffer">Raw memory buffer for this read.</param>
        public void SetResult(byte[] buffer)
        {
            try
            {
                if (IsFailed)
                    return;
                if (Type.IsValueType) /// Value Type
                    SetValueResult(buffer);
                else /// Ref Type
                    SetClassResult(buffer);
            }
            catch
            {
                IsFailed = true;
            }
        }

        /// <summary>
        /// Set the Result from a Value Type.
        /// </summary>
        /// <param name="buffer">Raw memory buffer for this read.</param>
        private void SetValueResult(byte[] buffer)
        {
            if (buffer.Length != Unsafe.SizeOf<T>()) // Safety Check
                throw new ArgumentOutOfRangeException(nameof(buffer));
            Result = Unsafe.As<byte, T>(ref buffer[0]);
            if (Result is MemPointer memPtrResult)
                memPtrResult.Validate();
        }

        /// <summary>
        /// (Base)
        /// Set the Result from a Class Type.
        /// Derived classes should call upon this Base.
        /// </summary>
        /// <param name="buffer">Raw memory buffer for this read.</param>
        protected virtual void SetClassResult(byte[] buffer)
        {
            if (Type == typeof(string))
            {
                var value = Encoding.Default.GetString(buffer).Split('\0')[0];
                if (value is T result) // We already know the Types match, this is to satisfy the compiler
                    Result = result;
            }
            else if (Type == typeof(List<int>)) // indices
            {
                var spanBuf = new Span<byte>(buffer);
                var list = new List<int>();
                for (var index = 0; index < spanBuf.Length; index += 4)
                {
                    list.Add(MemoryMarshal.Read<int>(spanBuf.Slice(index, 4)));
                }
                Result = (T)(object)list;

            }
            else if (Type == typeof(List<Vector128<float>>)) // vertices
            {
                //Need to get size from current scatter read entry
                var count = 6;
                var list = new List<Vector128<float>>();
                var spanBuf = new Span<byte>(buffer);
                for (var z = 0; z < count * 16; z += 16)
                {
                    var result = Vector128.Create(
                        spanBuf[z], spanBuf[z + 1], spanBuf[z + 2], spanBuf[z + 3], spanBuf[z + 4], spanBuf[z + 5],
                        spanBuf[z + 6], spanBuf[z + 7], spanBuf[z + 8], spanBuf[z + 9], spanBuf[z + 10], spanBuf[z + 11],
                        spanBuf[z + 12], spanBuf[z + 13], spanBuf[z + 14], spanBuf[z + 15])
                        .AsSingle();

                    list.Add(result);
                }
                Result = (T)(object)list;

            }
            else
                throw new NotImplementedException(nameof(Type));
        }
        #endregion

        #region Get Result
        /// <summary>
        /// Tries to return the Scatter Read Result.
        /// </summary>
        /// <typeparam name="TOut">Type to return.</typeparam>
        /// <param name="result">Result to populate.</param>
        /// <returns>True if successful, otherwise False.</returns>
        public bool TryGetResult<TOut>(out TOut result)
        {
            try
            {
                if (!IsFailed && Result is TOut tResult)
                {
                    result = tResult;
                    return true;
                }
                result = default;
                return false;
            }
            catch
            {
                result = default;
                return false;
            }
        }
        #endregion
    }
    #endregion

    #region WriteScatter
    public interface IScatterWriteEntry
    {
        ulong Address { get; }
    }

    public interface IScatterWriteDataEntry<T> : IScatterWriteEntry
        where T : unmanaged
    {
        T Data { get; }
    }

    public class ScatterWriteDataEntry<T> : IScatterWriteDataEntry<T>
        where T : unmanaged
    {
        public ulong Address { get; private set; }
        public T Data { get; private set; }

        public ScatterWriteDataEntry(ulong address, T data)
        {
            Address = address;
            Data = data;
        }
    }

    #endregion
}