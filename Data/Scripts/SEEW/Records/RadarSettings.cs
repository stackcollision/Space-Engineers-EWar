using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SEEW.Records {

	/// <summary>
	/// Settings for a radar system
	/// </summary>
	[Serializable]
	public class RadarSettings {
		public List<long> assignedIds;
		public int range;
		public float frequency;

		public RadarSettings() {
			assignedIds = new List<long>();
			range = 15000;
			frequency = 8.0f;
		}
	}

}
