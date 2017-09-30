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
		public static double[] radarBeamWidths = {
			0.0,	// NONE
			0.005,	// SHORT RANGE
			0.01	// LONG RANGE
		};

		public static int[] radarMinimumRanges = {
			0,
			100,
			1000
		};

		public static int[] radarMaximumRanges = {
			0,
			15000,
			40000
		};

		public static float[] radarMinimumFreqs = {
			0.0f,
			8.0f,
			2.0f
		};

		public static float[] radarMaximumFreqs = {
			0.0f,
			12.0f,
			4.0f,
		};


		/// <summary>
		/// GUIDs for Entity.Storage data
		/// </summary>
		public static Guid GUIDRadarSettings = new Guid("d3b29e41-392d-400c-a46a-5b68e4938f2a");

	}
}
