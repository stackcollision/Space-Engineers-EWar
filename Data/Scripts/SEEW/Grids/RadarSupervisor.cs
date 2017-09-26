﻿using System;
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
using Sandbox.Game.World;

namespace SEEW.Grids {

	/// <summary>
	/// Keeps track of all radar blocks on the ship, how they are oriented,
	/// the resultant radar coverage, and initiates sweeps and returns contacts
	/// </summary>
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_CubeGrid), true)]
	class RadarSupervisor : MyGameLogicComponent {

		#region Structs and Enums
		/// <summary>
		/// Represents a radar block on the ship
		/// </summary>
		private class RadarBlock {
			public IMyRadioAntenna block;
			public Sector sector;
		}

		/// <summary>
		/// One contact that the ship's radar system is tracking.
		/// </summary>
		private class Track {
			public bool lost;
			public IMyEntity ent;
			public IMyGps gps;
		}
		#endregion

		#region Instance Members

		private Logger logger = null;

		private IMyCubeGrid grid = null;
		
		private List<RadarBlock> allRadars = new List<RadarBlock>();
		private Dictionary<long, Track> allTracks = new Dictionary<long, Track>();
		private Sector radarCoverage = Sector.NONE;

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
			logger.debugLog("Radar supervisor created for grid", "Init");

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
			// Only sweep if the ship has radars
			if(allRadars.Count > 0)
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
				logger.debugLog("New phased radar block added", "BlockAdded");

				RadarBlock radar = new RadarBlock() {
					block = added as IMyRadioAntenna,
					sector = DetermineAntennaSector(added)
				};
				allRadars.Add(radar);

				logger.debugLog("New radar block faces sector " + radar.sector, "BlockAdded");

				RecalculateSectorCoverage();
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

			// TODO: Figure out why these blocks are never being found
			if(IsBlockRadar(removed)) {
				RadarBlock found = null;
				foreach(RadarBlock r in allRadars) {
					if(r.block == removed.FatBlock) {
						found = r;
						break;
					}
				}

				if(found != null) {
					allRadars.Remove(found);
					logger.debugLog("Phased radar block removed", "BlockRemoved");
				} else {
					logger.log(Logger.severity.ERROR, "BlockRemoved", "Phased radar block removed but was not found in list.");
				}

				RecalculateSectorCoverage();
			}
		}
		#endregion

		#region Sweep
		/// <summary>
		/// Runs a radar sweep for the attached grid.
		/// </summary>
		private void DoSweep() {
			//logger.debugLog("Beginning sweep", "UpdateBeforeSimulation100");

			// new, maintained, lost
			int n = 0, m = 0, l = 0;

			// Invalidate all current tracks.  If these are still invalid
			// at the end of the sweep, we will know which contacts are lost.
			foreach (KeyValuePair<long, Track> t in allTracks) {
				t.Value.lost = true;
			}

			VRageMath.BoundingSphereD sphere =
				new VRageMath.BoundingSphereD(grid.GetPosition(), 10000);
			List<IMyEntity> ents =
				MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);

			foreach (IMyEntity e in ents) {
				if (e == grid)
					continue;

				if (e is IMyCubeGrid) { 
					// Fuck you KSH
					/*Sandbox.Game.Gui.MyHud.LocationMarkers.RegisterMarker(
					 *	e.EntityId, new VRage.Game.Gui.MyHudEntityParams() {
							FlagsEnum = VRage.Game.Gui.MyHudIndicatorFlagsEnum.SHOW_ALL,
							Text = new StringBuilder("Test Icon")
					});*/

					// Check if this contact is already in the tracks dictionary
					Track oldTrack = null;
					if(allTracks.TryGetValue(e.EntityId, out oldTrack)) {
						m++;
						oldTrack.gps.Coords = e.GetPosition();
						oldTrack.lost = false;
					} else {
						n++;
						Track newTrack = new Track() {
							lost = false,
							ent = e,
							gps = MyAPIGateway.Session.GPS.Create(
								"~Track " + e.EntityId.ToString(), "~", 
								e.GetPosition(), true, true)
						};

						MyAPIGateway.Session.GPS.AddLocalGps(newTrack.gps);

						MyVisualScriptLogicProvider.SetGPSColor(
							newTrack.gps.Name, 
							Constants.Color_RadarContact);

						allTracks.Add(e.EntityId, newTrack);
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
				block.FatBlock.BlockDefinition.SubtypeId.StartsWith("EWPhasedRadar");
		}

		private Sector DetermineAntennaSector(IMySlimBlock antenna) {
			// Start with the default direction
			// The model antenna points in the +X, +Y, and -Z directions
			VRageMath.Vector3D direction = new VRageMath.Vector3D(1.0f, 1.0f, -1.0f);

			// Rotate this vector by the orientation of the block
			VRageMath.Vector3D rotated = VRageMath.Vector3D.Rotate(direction, antenna.FatBlock.LocalMatrix);

			logger.debugLog("New radar's vector is " + rotated.ToString(), "DetermineAntennaSector");

			return SectorExtensions.ClassifyVector(rotated);
		}

		/// <summary>
		/// Runs through the list of radars on this grid and determines
		/// which sectors have coverage
		/// </summary>
		private void RecalculateSectorCoverage() {
			// Reset coverage to zero
			radarCoverage = Sector.NONE;

			foreach(RadarBlock r in allRadars) {
				radarCoverage |= r.sector;
			}

			logger.debugLog($"Recomputed sector coverage of {allRadars.Count} radars to be " + String.Format("0x{0:X}", radarCoverage), "RecalculateSectorCoverage");
		}
		#endregion
	}
}