using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SEEW.Records {

	/// <summary>
	/// Refers to an octant around a grid
	/// </summary>
	public enum Sector {
		NONE = 0,
		TOP_FRONT_RIGHT = 1,
		TOP_FRONT_LEFT = 2,
		TOP_BACK_RIGHT = 4,
		TOP_BACK_LEFT = 8,
		BOTTOM_FRONT_RIGHT = 16,
		BOTTOM_FRONT_LEFT = 32,
		BOTTOM_BACK_RIGHT = 64,
		BOTTOM_BACK_LEFT = 128
	}

	public class SectorExtensions {

		/// <summary>
		/// Determines which sector a vector points into.
		/// Biases towards top, front, and right (if they lie on the axis)
		/// </summary>
		/// <param name="vec"></param>
		/// <returns></returns>
		public static Sector ClassifyVector(VRageMath.Vector3D vec) {

			if(vec.Y >= 0.0f) {
				if (vec.X >= 0.0f) {
					if (vec.Z >= 0.0f)
						return Sector.TOP_FRONT_RIGHT;
					else
						return Sector.TOP_FRONT_LEFT;
				} else {
					if (vec.Z >= 0.0f)
						return Sector.TOP_BACK_RIGHT;
					else
						return Sector.TOP_BACK_LEFT;
				}
			} else {
				if (vec.X >= 0.0f) {
					if (vec.Z >= 0.0f)
						return Sector.BOTTOM_FRONT_RIGHT;
					else
						return Sector.BOTTOM_FRONT_LEFT;
				} else {
					if (vec.Z >= 0.0f)
						return Sector.BOTTOM_BACK_RIGHT;
					else
						return Sector.BOTTOM_BACK_LEFT;
				}
			}

		}

	}
}
