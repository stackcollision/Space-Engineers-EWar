using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.ModAPI;
using VRageMath;

namespace SEEW {

	/// <summary>
	/// E-war related math functions
	/// </summary>
	public static class EWMath {

		private static Logger logger = new Logger("EWar", "EWMath");

		/// <summary>
		/// Returns the angle between two vectors in degrees
		/// </summary>
		/// <param name="one"></param>
		/// <param name="two"></param>
		/// <returns></returns>
		public static double AngleBetween(Vector3D one, Vector3D two) {
			double dot = Vector3D.Dot(one, two);
			dot = dot / (one.Length() * two.Length());

			double angle = Math.Acos(dot);
			if (angle == Double.NaN)
				return 0;
			else
				return (angle * 180.0) / Math.PI;
		}

		/// <summary>
		/// Returns the how close to parallel or perpendicular two vectors are.
		/// 0 is completely perpendicular
		/// 1 is completely parallel
		/// </summary>
		/// <param name="one"></param>
		/// <param name="two"></param>
		/// <returns></returns>
		public static double PercentDeviation(Vector3D one, Vector3D two) {
			Vector3D oneN = new Vector3D(one);
			Vector3D twoN = new Vector3D(two);

			oneN.Normalize();
			twoN.Normalize();

			return Math.Abs(Vector3D.Dot(oneN, twoN));
		}

		/// <summary>
		/// Returns the minimum cross section size a radar can detect at a range
		/// with a given beam width
		/// </summary>
		/// <param name="beamWidth"></param>
		/// <param name="range"></param>
		/// <returns></returns>
		public static double MinimumXSection(double beamWidth, double range) {
			double width = range * Math.Tan(beamWidth);
			return width * width;
		}

		/// <summary>
		/// Returns the cross section in m^2 for a grid from a given
		/// viewing angle.
		/// 
		/// Currently takes the areas of the faces of the AABB and scales them
		/// based on the incoming angle.
		/// TODO: Take an actual cross section of the AABB
		/// </summary>
		/// <returns></returns>
		public static double DetermineXSection(IMyCubeGrid grid, Vector3D dir) {
			Vector3D extents = grid.LocalAABB.Extents;

			double frontFace = extents.X * extents.Y;
			double topFace = extents.X * extents.Z;
			double sideFace = extents.Y * extents.Z;

			double devForward = PercentDeviation(dir, grid.WorldMatrix.Forward);
			double devUp = PercentDeviation(dir, grid.WorldMatrix.Up);
			double devRight = PercentDeviation(dir, grid.WorldMatrix.Right);

			//logger.debugLog($"Forward: {devForward} Up: {devUp} Right: {devRight}", "DetermineXSection");

			return (frontFace * devForward) +
				(topFace * devUp) +
				(sideFace * devRight);
		}

	}
}
