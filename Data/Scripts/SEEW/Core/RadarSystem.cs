using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.ModAPI;

using SEEW.Records;
using VRageMath;
using Sandbox.ModAPI;

namespace SEEW.Core {

	/// <summary>
	/// Controls a group of a certain type of radars on a grid
	/// </summary>
	class RadarSystem {

		#region Structs and Enums
		/// <summary>
		/// Represents a radar block on the ship
		/// </summary>
		private class RadarBlock {
			public IMySlimBlock block;
			public Sector sector;
		}
		#endregion

		#region Instance Members
		private Logger logger;

		private IMyCubeGrid grid;

		private List<RadarBlock> allRadars = new List<RadarBlock>();
		private Sector radarCoverage = Sector.NONE;
		private float radarRange = 0;

		#endregion

		#region Lifecycle
		public RadarSystem(IMyCubeGrid grid) {
			this.grid = grid;

			logger = new Logger(grid.EntityId.ToString(), "RadarSystem");
		}
		#endregion

		#region SE Hooks - Working Changed
		/// <summary>
		/// Hook for when a block's working status changes.  I.e. it loses power,
		/// is damaged, or is turned off.
		/// </summary>
		/// <param name="block"></param>
		private void WorkingChanged(IMyCubeBlock block) {
			logger.debugLog("A radar's IsWorking has changed.", "WorkingChanged");
			RecalculateSectorCoverage();
		}
		#endregion

		#region Interface

		/// <summary>
		/// Returns true if this system has any emitters at all, regardless
		/// of condition.
		/// </summary>
		/// <returns></returns>
		public bool HasRadars() {
			return allRadars.Count > 0;
		}

		/// <summary>
		/// Returns the range of this system
		/// </summary>
		/// <returns></returns>
		public double GetRange() {
			return radarRange;
		}

		/// <summary>
		/// Adds a new radar to this system
		/// </summary>
		/// <param name="added"></param>
		public void AddRadar(IMySlimBlock added) {
			// TODO: Verify this radar is the same type as the rest of
			// the system

			logger.debugLog("New radar block added", "BlockAdded");

			RadarBlock radar = new RadarBlock() {
				block = added,
				sector = DetermineAntennaSector(added)
			};
			allRadars.Add(radar);

			radar.block.FatBlock.IsWorkingChanged += WorkingChanged;

			logger.debugLog("New radar block faces sector " + radar.sector,
				"BlockAdded");

			RecalculateSectorCoverage();
		}

		/// <summary>
		/// Removes a radar from this system
		/// </summary>
		/// <param name="removed"></param>
		public void RemoveRadar(IMySlimBlock removed) {
			RadarBlock found = null;
			foreach (RadarBlock r in allRadars) {
				if (r.block == removed) {
					found = r;
					break;
				}
			}

			if (found != null) {
				found.block.FatBlock.IsWorkingChanged -= WorkingChanged;
				allRadars.Remove(found);
				logger.debugLog("Radar block removed", "BlockRemoved");
			} else {
				logger.log(Logger.severity.ERROR, "BlockRemoved",
					"Radar block removed but was not found in list.");
			}

			RecalculateSectorCoverage();
		}

		/// <summary>
		/// Sets the overall radar range to the greatest setting of all
		/// the attached radar blocks
		/// </summary>
		public void RecalculateRadarRange() {
			radarRange = 0;

			// TODO: change for new block base type
			foreach (RadarBlock r in allRadars) {
				radarRange = 50000;//Math.Max(radarRange,
								   //(r.block.FatBlock as IMyRadioAntenna).Radius);
			}
		}

		/// <summary>
		/// Allows the radar system to determine whether or not it can track a
		/// certain target given its coverage, size, distance, etc.
		/// </summary>
		/// <param name="target">The target to test</param>
		/// <param name="gridspacePos">The target's position vector in GRID SPACE.  
		/// This is passed in because it is expensive to calculate, 
		/// and multiple systems have to use it.</param>
		/// <param name="data">Outbound extra data about the track</param>
		/// <returns></returns>
		public bool CanTrackTarget(IMyCubeGrid target, Vector3D gridspacePos, out double xsec) {
			xsec = 0;

			// Easy out: we have no radars
			if (!HasRadars())
				return false;

			//Check that the sector is covered by our radars
			Sector sec = SectorExtensions.ClassifyVector(gridspacePos);
			if (IsSectorBlind(sec))
				return false;

			Vector3D targetPos = target.GetPosition();
			Vector3D myPos = grid.GetPosition();

			// Check that the contact is large enough for us to see
			// Do this by comparing the radar cross-section to the
			// minimum cross-section the radar is capable of seeing at
			// this range
			Vector3D vecTo = targetPos - myPos;
			xsec = EWMath.DetermineXSection(target, vecTo);
			double range = vecTo.Length();
			double minxsec
				= EWMath.MinimumXSection(Constants.radarBeamWidth, range);
			//logger.debugLog($"Minimum xsec at range {range} is {minxsec}", "DoSweep");
			//logger.debugLog($"Contact xsec is {xsec} and minimum is {minxsec}", "DoSweep");
			if (xsec < minxsec)
				return false;

			// Check if there is something between the radar
			// and the contact
			vecTo.Normalize();
			Vector3D castPosition = myPos + (vecTo * grid.LocalAABB.Size * 1.2);
			// TODO: CastRay inefficient over long distances
			//if (range <= 100) {
			IHitInfo hit;
			if (MyAPIGateway.Physics.CastRay(castPosition, targetPos, out hit)) {
				if (hit.HitEntity != target) {
					//logger.debugLog($"Contact {e.EntityId} obscured by {hit.HitEntity.EntityId}", "DoSweep");
					return false;
				}
			}
			/*} else {

			}*/

			return true;
		}
		#endregion

		#region Helpers
		/// <summary>
		/// Determines which sector the emitting face of a radar is pointing at
		/// </summary>
		/// <param name="antenna"></param>
		/// <returns></returns>
		private Sector DetermineAntennaSector(IMySlimBlock antenna) {
			// Start with the default direction
			// The model antenna points in the +X, +Y, and -Z directions
			VRageMath.Vector3D direction
				= new VRageMath.Vector3D(1.0f, 1.0f, -1.0f);

			// Rotate this vector by the orientation of the block
			VRageMath.Vector3D rotated = VRageMath.Vector3D.Rotate(
				direction, antenna.FatBlock.LocalMatrix);

			logger.debugLog("New radar's vector is " + rotated.ToString(),
				"DetermineAntennaSector");

			return SectorExtensions.ClassifyVector(rotated);
		}

		/// <summary>
		/// Runs through the list of radars on this grid and determines
		/// which sectors have coverage
		/// </summary>
		private void RecalculateSectorCoverage() {
			// Reset coverage to zero
			radarCoverage = Sector.NONE;
			radarRange = 0;

			foreach (RadarBlock r in allRadars) {
				if (r.block.FatBlock.IsWorking)
					radarCoverage |= r.sector;
			}

			logger.debugLog(
				$"Recomputed sector coverage of {allRadars.Count} radars to be "
				+ String.Format("0x{0:X}", radarCoverage) + " with range "
				+ radarRange,
				"RecalculateSectorCoverage");
		}

		/// <summary>
		/// Returns true if a sector is covered by the attached radars
		/// </summary>
		/// <param name="s"></param>
		/// <returns></returns>
		private bool IsSectorCovered(Sector s) {
			return (radarCoverage & s) != 0;
		}

		/// <summary>
		/// Returns true if the sector has no radar covering it
		/// </summary>
		/// <param name="s"></param>
		/// <returns></returns>
		private bool IsSectorBlind(Sector s) {
			return (radarCoverage & s) == 0;
		}
		#endregion
	}
}
