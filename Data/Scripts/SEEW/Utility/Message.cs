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
}
