using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Ashkatchap.Shared.Collections {
	/// <summary>
	/// Implementation of the Disruptor pattern
	/// </summary>
	/// <typeparam name="T">the type of item to be stored</typeparam>
	public class ThreadSafeRingBuffer_MultiProducer_SingleConsumer<T> where T : class {
		private readonly T[] _entries;
		private int _consumerCursor = 0;
		private Volatile.PaddedInt _producerCursor = new Volatile.PaddedInt();
		
		public ThreadSafeRingBuffer_MultiProducer_SingleConsumer(ushort capacity) {
			_entries = new T[capacity];
		}
		
		public void Enqueue(T obj) {
			if (obj == null) {
				Logger.Warn("Trying to Enqueue null. It is not supported because internally null represents a free place in the buffer for this algorithm.");
				return;
			}

			int indexToWriteOn;
			do {
				Thread.MemoryBarrier(); // obtain a fresh _producerCursor
				indexToWriteOn = _producerCursor.value;
			} while (null == (object) Interlocked.CompareExchange(ref _entries[indexToWriteOn], obj, null));
			Thread.MemoryBarrier(); // _entries must be filled before _producerCursor advances
			_producerCursor.value = _producerCursor.value == _entries.Length - 1 ? 0 : _producerCursor.value + 1;
			Thread.MemoryBarrier(); // _producerCursor must be written eventually now
		}
		
		public bool TryDequeue(out T item) {
			item = _entries[_consumerCursor];
			if (item != null) {
				_entries[_consumerCursor] = null; // Interlocked? Memory Barrier?
				_consumerCursor = _consumerCursor == _entries.Length - 1 ? 0 : _consumerCursor + 1;
				return true;
			} else {
				return false;
			}
		}
	}

	public static class Volatile {
		private const int CacheLineSize = 64;

		[StructLayout(LayoutKind.Explicit, Size = CacheLineSize * 2)]
		public struct PaddedInt {
			[FieldOffset(CacheLineSize)]
			public int value;

			/// <summary>
			/// Create a new <see cref="PaddedLong"/> with the given initial value.
			/// </summary>
			/// <param name="value">Initial value</param>
			public PaddedInt(int value) {
				this.value = value;
			}

			public int InterlockedIncrement() {
				return Interlocked.Increment(ref value);
			}
		}
	}
}
