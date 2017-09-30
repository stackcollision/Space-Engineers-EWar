using Sandbox.Common.ObjectBuilders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using Ingame = VRage.Game.ModAPI.Ingame;
using VRage.ObjectBuilders;
using VRage.ModAPI;
using SEEW.Records;
using VRageMath;
using Sandbox.ModAPI;
using SEEW.Utility;
using Sandbox.Game;
using Sandbox.Game.World;
using Sandbox.Game.EntityComponents;

namespace SEEW.Blocks {

	/// <summary>
	/// The central control block which manages the radar system
	/// </summary>
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, "EWControllerRadar")]
	class RadarController : MyGameLogicComponent {

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

		/// <summary>
		/// Represents a radar block on the ship
		/// </summary>
		public class RadarBlock {
			public IMySlimBlock block;
			public Sector sector;
			public RadarType type;
		}

		public enum RadarType {
			NONE,
			SHORT_RANGE,
			LONG_RANGE
		}
		#endregion

		#region Instance Members
		private Logger _logger;
		private IMyCubeGrid _grid;

		private bool _initialized = false;

		private Dictionary<long, Track> _allTracks = new Dictionary<long, Track>();

		private List<RadarBlock> _allRadars = new List<RadarBlock>();
		private List<RadarBlock> _assignedRadars = new List<RadarBlock>();
		private RadarType _assignedType = RadarType.NONE;
		private Sector _coverage = Sector.NONE;

		private RadarSettings _settings = new RadarSettings();
		#endregion

		#region Lifecycle
		public RadarController() {
			// Empty
		}

		public override void Init(MyObjectBuilder_EntityBase objectBuilder) {
			base.Init(objectBuilder);

			_grid = (Entity as IMyCubeBlock).CubeGrid;
			_logger = new Logger(_grid.EntityId.ToString(), "RadarController");

			this.NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.EACH_100TH_FRAME;
			this.NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
			_grid.OnBlockAdded += BlockAdded;
			_grid.OnBlockRemoved += BlockRemoved;
		}

		/// <summary>
		/// Blocks may be added during scene load in any order
		/// When this block is added it needs to check the grid for any
		/// radars which were added before it was
		/// </summary>
		public override void OnAddedToScene() {
			List<IMySlimBlock> blocks = new List<IMySlimBlock>();
			_grid.GetBlocks(blocks, (block) => {
				return IsBlockRadar(block);
			});

			foreach(IMySlimBlock b in blocks) {
				BlockAdded(b);
			}
		}

		public override void Close() {
			_grid.OnBlockAdded -= BlockAdded;
			_grid.OnBlockRemoved -= BlockRemoved;

			base.Close();
		}
		#endregion

		#region SE Hooks - Simulation
		public override void UpdateOnceBeforeFrame() {
			if (!_initialized) {
				if (Entity.Storage == null)
					Entity.Storage = new MyModStorageComponent();
				LoadSavedData();

				_initialized = true;
			}
		}

		public override void UpdateBeforeSimulation100() {
			if(_assignedRadars.Count > 0) {
				DoSweep();
			}
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
				_logger.debugLog("New radar block added", "BlockAdded");

				RadarBlock radar = new RadarBlock() {
					block = added,
					sector = DetermineAntennaSector(added),
					type = DetermineRadarType(added)
				};
				_allRadars.Add(radar);

				radar.block.FatBlock.IsWorkingChanged += WorkingChanged;

				_logger.debugLog("New radar block faces sector " + radar.sector,
					"BlockAdded");
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
			if (IsBlockRadar(removed)) {
				// Remove from the all list
				RadarBlock found = null;
				foreach (RadarBlock r in _allRadars) {
					if (r.block == removed) {
						found = r;
						break;
					}
				}

				if (found != null) {
					found.block.FatBlock.IsWorkingChanged -= WorkingChanged;
					_allRadars.Remove(found);

					UnassignedRadar(found);

					_logger.debugLog("Radar block removed", "BlockRemoved");
				} else {
					_logger.log(Logger.severity.ERROR, "BlockRemoved",
						"Radar block removed but was not found in list.");
				}

				RecalculateSectorCoverage();
				ReclassifySystem();
			}
		}
		#endregion

		#region SE Hooks - Working Changed
		/// <summary>
		/// Hook for when a block's working status changes.  I.e. it loses power,
		/// is damaged, or is turned off.
		/// </summary>
		/// <param name="block"></param>
		private void WorkingChanged(IMyCubeBlock block) {
			_logger.debugLog("A radar's IsWorking has changed.", "WorkingChanged");
			RecalculateSectorCoverage();
		}
		#endregion

		#region Sweep 
		/// <summary> 
		/// Runs a radar sweep for the attached grid. 
		/// </summary> 
		private void DoSweep() {
			//logger.debugLog("Beginning sweep", "UpdateBeforeSimulation100"); 

			Vector3D position = _grid.GetPosition();

			// new, maintained, lost 
			int n = 0, m = 0, l = 0;

			// Invalidate all current tracks.  If these are still invalid 
			// at the end of the sweep, we will know which contacts are lost. 
			foreach (KeyValuePair<long, Track> t in _allTracks) {
				t.Value.lost = true;
			}

			// Find all entities within the range 
			// TODO: Make range configurable via antenna properties 
			VRageMath.BoundingSphereD sphere
			  = new VRageMath.BoundingSphereD(position, _settings.range);
			List<IMyEntity> ents
			  = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);

			foreach (IMyEntity e in ents) {
				if (e == _grid)
					continue;

				// Radar will only pick up grids 
				if (e is IMyCubeGrid) {
					Vector3D epos = e.WorldAABB.Center;

					// Transform the coordinates into grid space so we 
					// can compare it against our radar coverage 
					VRageMath.Vector3D relative
					  = VRageMath.Vector3D.Transform(
						epos, _grid.WorldMatrixNormalizedInv);

					//Check that the sector is covered by our radars
					Sector sec = SectorExtensions.ClassifyVector(relative);
					if (IsSectorBlind(sec))
						continue;

					Vector3D targetPos = e.GetPosition();
					Vector3D myPos = _grid.GetPosition();

					// Check that the contact is large enough for us to see
					// Do this by comparing the radar cross-section to the
					// minimum cross-section the radar is capable of seeing at
					// this range
					Vector3D vecTo = targetPos - myPos;
					double xsec = EWMath.DetermineXSection(e as IMyCubeGrid, vecTo);
					double range = vecTo.Length();
					double minxsec
						= EWMath.MinimumXSection(Constants.radarBeamWidth, range);
					//logger.debugLog($"Minimum xsec at range {range} is {minxsec}", "DoSweep");
					//logger.debugLog($"Contact xsec is {xsec} and minimum is {minxsec}", "DoSweep");
					if (xsec < minxsec)
						continue;

					// Check if there is something between the radar
					// and the contact
					vecTo.Normalize();
					Vector3D castPosition = myPos + (vecTo * _grid.LocalAABB.Size * 1.2);
					// TODO: CastRay inefficient over long distances
					//if (range <= 100) {
					IHitInfo hit;
					if (MyAPIGateway.Physics.CastRay(castPosition, targetPos, out hit)) {
						if (hit.HitEntity != e) {
							//logger.debugLog($"Contact {e.EntityId} obscured by {hit.HitEntity.EntityId}", "DoSweep");
							continue;
						}
					}
					/*} else {

					}*/

					// If all previous checks passed, we can track it 
					// Check if this contact is already in the tracks dictionary 
					Track oldTrack = null;
					if (_allTracks.TryGetValue(e.EntityId, out oldTrack)) {
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

						_allTracks.Add(e.EntityId, newTrack);
					}
				}
			}

			// Check which tracks were not marked valid during this sweep 
			// and prune them 
			List<long> remove = new List<long>();
			foreach (KeyValuePair<long, Track> t in _allTracks) {
				if (t.Value.lost) {
					// Remove the GPS 
					MyAPIGateway.Session.GPS.RemoveLocalGps(t.Value.gps);

					// Add to prune list 
					remove.Add(t.Key);
					l++;
				}
			}
			foreach (long r in remove) {
				_allTracks.Remove(r);
			}

			//logger.debugLog( 
			//$"Track Summary: {n} new, {m} maintained, {l} lost", "DoSweep"); 
		}
		#endregion

		#region Interface
		public List<RadarBlock> GetAvailableRadars() {
			return _allRadars;
		}

		public List<RadarBlock> GetAssignedRadars() {
			return _assignedRadars;
		}

		public void AssignRadar(RadarBlock radar) {
			if (IsRadarCompatible(radar)) {
				_assignedRadars.Add(radar);
				_settings.assignedIds.Add(radar.block.FatBlock.EntityId);
				SaveRadarSettings();

				RecalculateSectorCoverage();
				ReclassifySystem();
			}
		}

		public void UnassignedRadar(RadarBlock radar) {
			if (_assignedRadars.Contains(radar)) {
				_assignedRadars.Remove(radar);

				RecalculateSectorCoverage();
				ReclassifySystem();
			}
		}

		public int GetRange() {
			return _settings.range;
		}

		public void SetRange(int range) {
			_settings.range = range;
			SaveRadarSettings();
		}

		public float GetFreq() {
			return _settings.frequency;
		}

		public void SetFreq(float freq) {
			_settings.frequency = (float)Math.Round(freq, 1);
			SaveRadarSettings();
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
		/// Returns the class of radar
		/// </summary>
		/// <param name="block"></param>
		/// <returns></returns>
		private RadarType DetermineRadarType(IMySlimBlock block) {
			if (block.FatBlock == null ||
				block.FatBlock.BlockDefinition.TypeId != typeof(MyObjectBuilder_UpgradeModule))
				return RadarType.NONE;

			switch(block.FatBlock.BlockDefinition.SubtypeId) {
				case "EWRadarSearchPhased":
					return RadarType.SHORT_RANGE;

				case "EWRadarSearchRotating":
					return RadarType.LONG_RANGE;

				default:
					return RadarType.NONE;
			}
		}

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

			_logger.debugLog("New radar's vector is " + rotated.ToString(),
				"DetermineAntennaSector");

			return SectorExtensions.ClassifyVector(rotated);
		}

		/// <summary>
		/// Runs through the list of radars on this grid and determines
		/// which sectors have coverage
		/// </summary>
		private void RecalculateSectorCoverage() {
			// Reset coverage to zero
			_coverage = Sector.NONE;

			foreach (RadarBlock r in _assignedRadars) {
				if (r.block.FatBlock.IsWorking)
					_coverage |= r.sector;
			}

			_logger.debugLog(
				$"Recomputed sector coverage of {_assignedRadars.Count} radars to be "
				+ String.Format("0x{0:X}", _coverage) + " with range "
				+ _settings.range,
				"RecalculateSectorCoverage");
		}

		/// <summary>
		/// Looks at the assigned radars and determines the type of radar
		/// system this is.
		/// The first block placed will classify the system.
		/// Once the system is classified, radar blocks not of the correct
		/// type cannot be added.
		/// </summary>
		private void ReclassifySystem() {
			if(_assignedRadars.Count == 0) {
				// The system has become unclassified
				_assignedType = RadarType.NONE;
			} else {
				_assignedType = _assignedRadars[0].type;
			}
		}

		/// <summary>
		/// A radar is only compatible (and thus can be assigned)
		/// to this system if it has the same type as the radars already
		/// assigned, or if the system is unclassified.
		/// </summary>
		/// <param name="radar"></param>
		/// <returns></returns>
		private bool IsRadarCompatible(RadarBlock radar) {
			return _assignedType == RadarType.NONE ||
				_assignedType == radar.type;
		}

		/// <summary>
		/// Returns true if a sector is covered by the attached radars
		/// </summary>
		/// <param name="s"></param>
		/// <returns></returns>
		private bool IsSectorCovered(Sector s) {
			return (_coverage & s) != 0;
		}

		/// <summary>
		/// Returns true if the sector has no radar covering it
		/// </summary>
		/// <param name="s"></param>
		/// <returns></returns>
		private bool IsSectorBlind(Sector s) {
			return (_coverage & s) == 0;
		}

		/// <summary>
		/// Saves radar assignments to the Entity Storage
		/// </summary>
		private void SaveRadarSettings() {
			Entity.Storage[Constants.GUIDRadarSettings]
				= MyAPIGateway.Utilities.SerializeToXML<RadarSettings>(_settings);
			_logger.debugLog("Saved", "SaveRadarSettings");
		}

		/// <summary>
		/// Deserializes data saved in the world file
		/// </summary>
		private void LoadSavedData() {
			_logger.debugLog("Starting load", "LoadSavedData");

			if(Entity.Storage.ContainsKey(Constants.GUIDRadarSettings)) {

				_logger.debugLog("Loading saved radar settings: " + Entity.Storage[Constants.GUIDRadarSettings], "LoadSavedData");
				_settings = MyAPIGateway.Utilities.SerializeFromXML<RadarSettings>(Entity.Storage[Constants.GUIDRadarSettings]);

				// Process assignments
				foreach(RadarBlock r in _allRadars) {
					if (_settings.assignedIds.Contains(r.block.FatBlock.EntityId))
						_assignedRadars.Add(r);
				}
				RecalculateSectorCoverage();
				ReclassifySystem();

			}
		}
		#endregion

	}
}
