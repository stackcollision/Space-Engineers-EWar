using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using VRage;
using VRage.Game.Components;
using Sandbox.Engine;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Library.Utils;
using Interfaces = Sandbox.ModAPI.Interfaces;
using InGame = Sandbox.ModAPI.Ingame;
using VRage.Game;
using VRage.ObjectBuilders;
using VRage.Game.ModAPI;
using Sandbox.Game.Entities;
using VRage.ModAPI;

using SEEW.Records;
using SEEW.Core;
using Sandbox.Game.World;
using VRageMath;
using Sandbox.Common.ObjectBuilders;

namespace SEEW.Grids {

	/// <summary>
	/// Keeps track of all radar blocks on the ship, how they are oriented,
	/// the resultant radar coverage, and initiates sweeps and returns contacts
	/// </summary>
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_CubeGrid), true)]
	class RadarSupervisor : MyGameLogicComponent {

		#region Structs and Enums
		/// <summary>
		/// One contact that the ship's radar system is tracking.
		/// </summary>
		private class Track {
			public bool lost;
			public IMyEntity ent;
			public IMyGps gps;
			public string trackId;
		}
		#endregion

		#region Instance Members

		private Logger logger = null;

		private IMyCubeGrid grid = null;

		private RadarSystem shortRange;
		
		private Dictionary<long, Track> allTracks = new Dictionary<long, Track>();

		#endregion

		#region Lifecycle
		public RadarSupervisor() {
			// Empty
		}

		/// <summary>
		/// Initializes the radar supervisor
		/// </summary>
		/// <param name="objectBuilder"></param>
		public override void Init(MyObjectBuilder_EntityBase objectBuilder) {
			base.Init(objectBuilder);
			grid = Entity as IMyCubeGrid;

			logger = new Logger(grid.CustomName, "RadarSupervisor");

			shortRange = new RadarSystem(grid);

			// Add hooks
			Entity.NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.EACH_100TH_FRAME;
			grid.OnBlockAdded += BlockAdded;
			grid.OnBlockRemoved += BlockRemoved;
			
		}

		public override void Close() {
			grid.OnBlockAdded -= BlockAdded;
			grid.OnBlockRemoved -= BlockRemoved;

			base.Close();
		}
		#endregion

		#region SE Hooks - Simulation
		public override void UpdateBeforeSimulation100() {
			if(shortRange.HasRadars())
				DoSweep();
		}
		#endregion

		#region SE Hooks - Block Added
		/// <summary>
		/// Hook for when blocks are added to the grid.
		/// Used to keep track of how many radar blocks are present on the grid
		/// so we can measure coverage.
		/// </summary>
		/// <param name="added"></param>
		private void BlockAdded(IMySlimBlock added) {
			if (IsBlockRadar(added)) {
				shortRange.AddRadar(added);
			}
		}
		#endregion

		#region SE Hooks - Block Removed
		/// <summary>
		/// Hook for when blocks are removed from the grid.
		/// Used to keep track of radar coverage
		/// </summary>
		/// <param name="removed"></param>
		private void BlockRemoved(IMySlimBlock removed) {
			if(IsBlockRadar(removed)) {
				shortRange.RemoveRadar(removed);
			}
		}
		#endregion

		#region Sweep
		/// <summary>
		/// Runs a radar sweep for the attached grid.
		/// </summary>
		private void DoSweep() {
			//logger.debugLog("Beginning sweep", "UpdateBeforeSimulation100");

			shortRange.RecalculateRadarRange();

			Vector3D position = grid.GetPosition();

			// new, maintained, lost
			int n = 0, m = 0, l = 0;

			// Invalidate all current tracks.  If these are still invalid
			// at the end of the sweep, we will know which contacts are lost.
			foreach (KeyValuePair<long, Track> t in allTracks) {
				t.Value.lost = true;
			}

			// Find all entities within the range
			// TODO: Make range configurable via antenna properties
			VRageMath.BoundingSphereD sphere 
				= new VRageMath.BoundingSphereD(position, GetMaxRadarRange());
			List<IMyEntity> ents 
				= MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);

			foreach (IMyEntity e in ents) {
				if (e == grid)
					continue;

				// Radar will only pick up grids
				if (e is IMyCubeGrid) {
					Vector3D epos = e.WorldAABB.Center;

					// Transform the coordinates into grid space so we
					// can compare it against our radar coverage
					VRageMath.Vector3D relative 
						= VRageMath.Vector3D.Transform(
							epos, grid.WorldMatrixNormalizedInv);


					// Let the radar systems decide if they are able to
					// track this contact
					double xsec;
					if(shortRange.CanTrackTarget(e as IMyCubeGrid, relative, out xsec)) {
						// If we can track it
						// Check if this contact is already in the tracks dictionary
						Track oldTrack = null;
						if (allTracks.TryGetValue(e.EntityId, out oldTrack)) {
							m++;
							oldTrack.gps.Coords = epos;
							oldTrack.gps.Name
								= $"~Track {oldTrack.trackId} ({(int)xsec}m²)~";
							oldTrack.lost = false;
						} else {
							n++;

							string id = e.EntityId.ToString();
							id = id.Substring(Math.Max(0, id.Length - 5));

							Track newTrack = new Track() {
								lost = false,
								ent = e,
								gps = MyAPIGateway.Session.GPS.Create(
									$"~Track {id} ({(int)xsec}m²)~", "Radar Track",
									epos, true, true),
								trackId = id
							};

							MyAPIGateway.Session.GPS.AddLocalGps(newTrack.gps);

							MyVisualScriptLogicProvider.SetGPSColor(
								newTrack.gps.Name,
								Constants.Color_RadarContact);

							allTracks.Add(e.EntityId, newTrack);
						}
					}
					
				}
			}

			// Check which tracks were not marked valid during this sweep
			// and prune them
			List<long> remove = new List<long>();
			foreach (KeyValuePair<long, Track> t in allTracks) {
				if (t.Value.lost) {
					// Remove the GPS
					MyAPIGateway.Session.GPS.RemoveLocalGps(t.Value.gps);

					// Add to prune list
					remove.Add(t.Key);
					l++;
				}
			}
			foreach(long r in remove) {
				allTracks.Remove(r);
			}

			//logger.debugLog(
				//$"Track Summary: {n} new, {m} maintained, {l} lost", "DoSweep");
		}
		#endregion

		#region Helpers
		/// <summary>
		/// Returns true if the block is a radar emitter
		/// </summary>
		/// <param name="block"></param>
		/// <returns></returns>
		private bool IsBlockRadar(IMySlimBlock block) {
			return block.FatBlock != null &&
				block.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_UpgradeModule) &&
				block.FatBlock.BlockDefinition.SubtypeId.StartsWith(
					"EWRadar");
		}

		/// <summary>
		/// Finds the maximum range of all the radar systems on this grid
		/// </summary>
		/// <returns></returns>
		private double GetMaxRadarRange() {
			return shortRange.GetRange();
		}
		#endregion
	}
}
