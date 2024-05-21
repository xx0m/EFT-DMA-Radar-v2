using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace eft_dma_radar
{
	public class Transform
	{
		#region Xmm
		private static readonly Vector128<float> Xmm300 = Vector128.Create(-2f, -2f, 2f, 0f);
		private static readonly Vector128<float> Xmm320 = Vector128.Create(-2f, 2f, -2f, 0f);
		private static readonly Vector128<float> Xmm330 = Vector128.Create(2f, -2f, -2f, 0f);
		private static readonly Vector128<float> Xmm610 = Vector128.Create(0f, 0f, 0f, 0f);
		private static readonly Vector128<float> Xmm520 = Vector128.Create(-1f, -1f, -1f, 0f);
		private static readonly Vector128<float> Xmm690 = Vector128.Create(0f, 0f, 0f, 0f);
		private static readonly Vector128<float> XmmDe0 = Vector128.Create(1f, 1f, 1f, 1f);
		#endregion

		private readonly bool _isPlayerTransform;

		public Transform(ulong transformInternal, bool isPlayerTransform = false)
		{
			var hierarchy = Memory.ReadValue<ulong>(transformInternal + Offsets.TransformInternal.Hierarchy);

			IndicesAddr = Memory.ReadValue<ulong>(hierarchy + Offsets.TransformHierarchy.Indices);

			VerticesAddr = Memory.ReadValue<ulong>(hierarchy + Offsets.TransformHierarchy.Vertices); 

			_isPlayerTransform = isPlayerTransform;

			if (isPlayerTransform)
				HierarchyIndex = 1;
			else
				HierarchyIndex = Memory.ReadValue<int>(transformInternal + Offsets.TransformInternal.HierarchyIndex);
		}

		public ulong IndicesAddr { get; }
		public ulong VerticesAddr { get; }
		private int HierarchyIndex { get; }

        #region GetPos
        public Vector3 GetPosition(object[] obj = null)
		{
			try
			{
                List<int> indices;
                List<Vector128<float>> vertices;
                if (obj is null) // standalone constructor
                {
                    indices = ReadIndices(IndicesAddr, HierarchyIndex + 1); // address, count
                    vertices = ReadVertices128(VerticesAddr, 3 * HierarchyIndex + 3); // Reference v9/v10 in IDA
                }
                else // construct via scatter read
                {
                    indices = (List<int>)obj[0];
                    vertices = (List<Vector128<float>>)obj[1];
                }

				if (indices is null || vertices is null)
					throw new Exception("Invalid Position!");

                var index = indices[HierarchyIndex]; // Indices + 4 * capacity   (was index_relation)
                if (_isPlayerTransform) if (index != 0) throw new Exception("Invalid index!");

                var result = vertices[3 * HierarchyIndex]; // Vertices + 0x30 * capacity

                int iterations = 0;
                while (index >= 0)
                {
                    // perform validations....
                    if (_isPlayerTransform) if (index > 1) throw new Exception("Invalid index!");
                        else
                        {
                            if (index >= vertices.Count / 3) break;
                        }
                    if (iterations++ >= 100) throw new Exception("Max SIMD Iterations! Invalid state.");

                    // begin iteration...
                    var v9 = vertices[3 * index + 1].AsInt32(); // Vertices + 0x30 * index + 0x10

                    var v10 = Sse.Multiply(vertices[3 * index + 2], result); // Vertices + 0x30 * index + 0x20

                    var v11 = Sse2.Shuffle(v9, 0).AsSingle();
                    var v12 = Sse2.Shuffle(v9, 0x71).AsSingle();
                    var v13 = Sse2.Shuffle(v9, 0x8E).AsSingle();
                    var v14 = Sse2.Shuffle(v9, 0x55).AsSingle();
                    var v15 = Sse2.Shuffle(v9, 0xAA).AsSingle();
                    var v16 = Sse2.Shuffle(v9, 0xDB).AsSingle();

                    result = Sse.Add(
                                Sse.Add(
                                    Sse.Add(
                                        Sse.Multiply(
                                            Sse.Subtract(
                                                Sse.Multiply(Sse.Multiply(v11, Xmm330), v13),
                                                Sse.Multiply(Sse.Multiply(v14, Xmm300), v16)),
                                            Sse2.Shuffle(v10.AsInt32(), 0xAA).AsSingle()),
                                        Sse.Multiply(
                                            Sse.Subtract(
                                                Sse.Multiply(Sse.Multiply(v15, Xmm300), v16),
                                                Sse.Multiply(Sse.Multiply(v11, Xmm320), v12)),
                                            Sse2.Shuffle(v10.AsInt32(), 0x55).AsSingle())),
                                    Sse.Add(
                                        Sse.Multiply(
                                            Sse.Subtract(
                                                Sse.Multiply(Sse.Multiply(v14, Xmm320), v12),
                                                Sse.Multiply(Sse.Multiply(v15, Xmm330), v13)),
                                            Sse2.Shuffle(v10.AsInt32(), 0).AsSingle()),
                                    v10)),
                                vertices[3 * index]); // Vertices + 0x30 * index

                    index = indices[index]; // Indices + 4 * index
                }
                // return result
                var pos = result.AsVector3();
                if (pos.X == 0 && pos.Y == 0 && pos.Z == 0) throw new Exception("Invalid Position!");
                return new Vector3(pos.X, pos.Z, pos.Y); // Z & Y flipped
            }
			catch (Exception ex)
			{
                throw new Exception("Invalid Position!");
            }
		}
		#endregion

		#region ReadMem
		/// <summary>
		/// IndicesAddress -> IndicesSize -> VerticesAddress -> VerticesSize
		/// </summary>
		/// <returns></returns>
		public Tuple<ulong, int, ulong, int> GetScatterReadParameters()
		{
			return new Tuple<ulong, int, ulong, int>(IndicesAddr,
				HierarchyIndex + 1,
				VerticesAddr,
				3 * HierarchyIndex + 3);
		}
		/// <summary>
		/// Standalone Indices Read.
		/// </summary>
		/// <returns></returns>
		private static List<int> ReadIndices(ulong address, int count)
		{
			if (count <= 0)
			{
				return new List<int>();
			}

			var result = Memory.ReadBuffer(address, count * 4);

			var ret = new List<int>();

			for (var index = 0; index < result.Length; index += 4)
			{
				ret.Add(MemoryMarshal.Read<int>(result.Slice(index, 4)));
			}

			return ret;
		}

		/// <summary>
		/// Standalone Vertices Read.
		/// </summary>
		private static List<Vector128<float>> ReadVertices128(ulong address, int count)
		{
			var ret = new List<Vector128<float>>();

			var buffer = Memory.ReadBuffer(address, count * 16);

			for (var i = 0; i < count * 16; i += 16)
			{
				var result = Vector128.Create(
					buffer[i], buffer[i + 1], buffer[i + 2], buffer[i + 3], buffer[i + 4], buffer[i + 5],
					buffer[i + 6], buffer[i + 7], buffer[i + 8], buffer[i + 9], buffer[i + 10], buffer[i + 11],
					buffer[i + 12], buffer[i + 13], buffer[i + 14], buffer[i + 15])
					.AsSingle();

				ret.Add(result);
			}

			return ret;
		}
        #endregion
    }
}
