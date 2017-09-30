using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SEEW {
	/// <summary>
	/// Contains values which I want to be able to change easily
	/// </summary>
	public static class Constants {

		/// <summary>
		/// Message ID numbers
		/// </summary>
		public const ushort MIDRadarSettings = 55000;

		/// <summary>
		/// Color of radar contact beacons
		/// </summary>
		public static VRageMath.Color Color_RadarContact = VRageMath.Color.Gold;

		/// <summary>
		/// Width of long range radar beams
		/// </summary>
		public const double radarBeamWidth = 0.005;

		public static Guid GUIDRadarSettings = new Guid("d3b29e41-392d-400c-a46a-5b68e4938f2a");

	}
}
