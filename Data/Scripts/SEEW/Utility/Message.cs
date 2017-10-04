using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SEEW.Utility {
	
	/// <summary>
	/// Used for sending messages between the client and server
	/// to synchronize mod-specific data
	/// </summary>
	/// <typeparam name="TKey"></typeparam>
	/// <typeparam name="TValue"></typeparam>
	[Serializable]
	public class Message<TKey,TValue> {
		public TKey Key { get; set; }
		public TValue Value { get; set; }

		public Message() {
			Key = default(TKey);
			Value = default(TValue);
		}

		public Message(TKey k, TValue v) {
			Key = k;
			Value = v;
		}

		public static Message<TKey, TValue> FromXML(byte[] data) {
			return MyAPIGateway.Utilities.SerializeFromXML<Message<TKey, TValue>>(ASCIIEncoding.ASCII.GetString(data));
		}

		public byte[] ToXML() {
			return ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML<Message<TKey, TValue>>(this));
		}
	}

	/// <summary>
	/// Used for addressing a block on a grid with a message
	/// </summary>
	[Serializable]
	public class BlockAddress {
		public long grid;
		public long block;

		public BlockAddress() {
			grid = 0;
			block = 0;
		}

		public BlockAddress(long grid, long block) {
			this.grid = grid;
			this.block = block;
		}

		public override string ToString() {
			return $"{block} @ {grid}";
		}
	}
}
