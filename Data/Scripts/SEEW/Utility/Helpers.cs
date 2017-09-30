using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SEEW.Utility {
	public static class Helpers {

		public static bool IsServer {
			get {
				if (MyAPIGateway.Session == null || 
					MyAPIGateway.Multiplayer == null)
					return false;

				if (MyAPIGateway.Session.OnlineMode == VRage.Game.MyOnlineModeEnum.OFFLINE ||
					MyAPIGateway.Multiplayer.IsServer)
					return true;

				return false;
			}
		}

	}
}
