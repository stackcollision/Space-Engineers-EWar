using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SEEW.Records {

	/// <summary>
	/// Refers to an octant around a grid
	/// </summary>
	public enum Octant {
		TOP_FRONT = 1,
		TOP_RIGHT = 2,
		TOP_BACK = 4,
		TOP_LEFT = 8,
		BOTTOM_FRONT = 16,
		BOTTOM_RIGHT = 32,
		BOTTOM_BACK = 64,
		BOTTOM_LEFT = 128
	}
}
