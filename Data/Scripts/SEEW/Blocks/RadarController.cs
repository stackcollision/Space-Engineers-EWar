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
using Sandbox.Game.Entities;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Library.Utils;
using VRage.Utils;
using VRage;

namespace SEEW.Blocks {

	/// <summary>
	/// The central control block which manages the radar system
	/// </summary>
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, "EWControllerRadar")]
	public class RadarController : MyGameLogicComponent {

		#region Structs and Enums
		/// <summary>
		/// One contact that the ship's radar system is tracking.
		/// </summary>
		private class Track {
			public bool lost;

			public IMyEntity ent;
			public string trackId;
			public MyAreaMarker marker;

			public Vector3D position;
			public double xsec;
		}

		/// <summary>
		/// A contact sent by the server, contains only basic information
		/// </summary>
		public class RemoteContact {
			public long entId;
			public Vector3D pos;
			public double xsec;
		}

		/// <summary>
		/// Represents a radar block on the ship
		/// </summary>
		public class Radar {
			public IMySlimBlock block;
			public Sector sector;
			public RadarType type;

			public RadarBlock radarBlock { get {
					return block.FatBlock.GameLogic.GetAs<RadarBlock>();
				} }

			public long entID { get {
					return block.FatBlock.EntityId;
				} }
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

		private List<Radar> _allRadars = new List<Radar>();
		private List<Radar> _assignedRadars = new List<Radar>();
		private RadarType _assignedType = RadarType.NONE;
		private Sector _coverage = Sector.NONE;

		private RadarSettings _settings = new RadarSettings();

		private MyTimer _remoteSweepTimer;
		private MyTimer _trackSweepTimer;
		#endregion

		#region Lifecycle
		public RadarController() {
			// Empty
		}

		public override void Init(MyObjectBuilder_EntityBase objectBuilder) {
			base.Init(objectBuilder);

			_grid = (Entity as IMyCubeBlock).CubeGrid;
			_logger = new Logger(Entity.EntityId.ToString(), "RadarController");
			
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
			if (_remoteSweepTimer != null)
				_remoteSweepTimer.Stop();
			if (_trackSweepTimer != null)
				_trackSweepTimer.Stop();

			_grid.OnBlockAdded -= BlockAdded;
			_grid.OnBlockRemoved -= BlockRemoved;

			base.Close();
		}

		public static RadarController GetForBlock(long entityId) {
			IMyEntity ent = MyAPIGateway.Entities.GetEntityById(entityId);
			if (ent == null)
				return null;

			return ent.GameLogic.GetAs<RadarController>();
		}
		#endregion

		#region SE Hooks - Simulation
		public override void UpdateOnceBeforeFrame() {
			if (!_initialized) {
				if (Entity.Storage == null)
					Entity.Storage = new MyModStorageComponent();
				LoadSavedData();

				
				if(Helpers.IsServer) {
					// The server will do an acquisition sweep and push results
					// to the clients
					_remoteSweepTimer = new MyTimer(5000, DoAcquisitionSweep);
					_remoteSweepTimer.Start();
				} 

				if(!Helpers.IsServer || !Helpers.IsDedicated) {
					// Determines how often collected tracks are updated
					_trackSweepTimer = new MyTimer(1000, () => { this.NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME; });
					_trackSweepTimer.Start();
				}

				/*MyAreaMarker marker = new MyAreaMarker(
					new MyPositionAndOrientation(Entity.WorldMatrix),
					new MyAreaMarkerDefinition() {
						DisplayNameEnum = MyStringId.GetOrCompute("Test Marker"),
						ColorHSV = Color.Red.ColorToHSV()
					});
				marker.AddHudMarker();*/

				_initialized = true;

				_logger.debugLog("Initialized", "UpdateOnceBeforeFrame");
			} else {
				DoTrackingSweep();
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
				// Check that the block is not already in the array
				// (can happen when loading)
				foreach(Radar r in _allRadars) {
					if (r.block == added)
						return;
				}

				_logger.debugLog("New radar block added", "BlockAdded");

				Radar radar = new Radar() {
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
				Radar found = null;
				foreach (Radar r in _allRadars) {
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

		#region SE Hooks - Entity OnClose
		/// <summary>
		/// Called when an entity we are tracking goes outside the streaming
		/// range.  It will continue to be tracked via the values sent by the
		/// server.
		/// </summary>
		/// <param name="ent"></param>
		private void TrackedEntityUnloaded(IMyEntity ent) {
			// Find the entity in the tracks list
			Track track;
			if(_allTracks.TryGetValue(ent.EntityId, out track)) {
				_logger.debugLog($"Entity associated with Track {track.trackId} has been lost", "TrackedEntityUnloaded");
				if (track.marker != null) {
					track.marker.Close();
					track.marker = null;
				}
				track.ent = null;
			}
		}
		#endregion

		#region Sweep 
		/// <summary>
		/// 
		/// SERVER ONLY FUNCTION
		/// 
		/// Server will periodically determine which contacts are near a grid
		/// with radar and send them out in a message.  This is a workaround
		/// for the fact that not all entities within radar range may be 
		/// streamed to the client.
		/// </summary>
		private void DoAcquisitionSweep() {
			_logger.debugLog("Running acquisition sweep", "DoAcquisitionSweep");

			Vector3D pos = _grid.WorldAABB.Center;

			VRageMath.BoundingSphereD sphere
			  = new VRageMath.BoundingSphereD(pos, _settings.range);
			List<IMyEntity> ents
			  = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);

			List<RemoteContact> contacts = new List<RemoteContact>();

			foreach(IMyEntity e in ents) {
				if (e == _grid)
					continue;

				if(e is IMyCubeGrid) {
					Vector3D vecTo = e.WorldAABB.Center - pos;

					contacts.Add(new RemoteContact() {
						entId = e.EntityId,
						pos = e.WorldAABB.Center,
						xsec = EWMath.DetermineXSection(e as IMyCubeGrid, vecTo)
					});
				}
			}

			// Send the contact list, even if it is blank.  This is how the
			// client will know when distant contacts have gone out of range
			// or disappeared.
			_logger.debugLog($"List<RemoteContact> -> Clients with {contacts.Count} entities", "DoAcquisitionSweep");
			Message<long, List<RemoteContact>> msg
				= new Message<long, List<RemoteContact>>(Entity.EntityId, contacts);
			MyAPIGateway.Multiplayer.SendMessageToOthers(
				Constants.MIDAcquisitionSweep,
				msg.ToXML());
		}

		/// <summary>
		/// Goes through all tracks in the list and determines whether or not
		/// they can currently be seen.  This function does not add or remove
		/// contacts from the list.  That can only be done in ProcessAcquiredContacts.
		/// </summary>
		private void DoTrackingSweep() {
			//_logger.debugLog("Beginning sweep", "DoTrackingSweep");

			Vector3D myPos = _grid.WorldAABB.Center;

			// Go through all current tracks and update their makers
			foreach(KeyValuePair<long, Track> t in _allTracks) { 

				Track track = t.Value;

				//_logger.debugLog($"For Track {track.trackId}", "DoTrackingSweep");

				// If the entity is null, this track is only available on the
				// server so use the stored value.  Otherwise get the most
				// up to date value
				if (track.ent != null) {
					//_logger.debugLog("Entity is not null", "DoTrackingSweep");
					track.position = track.ent.WorldAABB.Center;
				}

				// Transform the coordinates into grid space so we 
				// can compare it against our radar coverage 
				VRageMath.Vector3D relative
				  = VRageMath.Vector3D.Transform(
					track.position, _grid.WorldMatrixNormalizedInv);

				//Check that the sector is covered by our radars
				Sector sec = SectorExtensions.ClassifyVector(relative);
				if (IsSectorBlind(sec)) {
					//_logger.debugLog("Sector is blind", "DoTrackingSweep");
					// If a contact is not trackable, clear its GPS marker
					ClearTrackMarker(track);
					continue;
				}

				// Vector to target
				Vector3D vecTo = track.position - myPos;

				// If the entity is available, calculate the cross-section
				// Otherwise we will use the stored value from the server
				if (track.ent != null) {
					track.xsec 
						= EWMath.DetermineXSection(track.ent as IMyCubeGrid, vecTo);
				}

				double range = vecTo.Length();
				double minxsec
					= EWMath.MinimumXSection(
						Constants.radarBeamWidths[(int)_assignedType], range);
				if (track.xsec < minxsec) {
					//_logger.debugLog("Cross-section not large enough", "DoTrackingSweep");
					ClearTrackMarker(track);
					continue;
				}

				// TODO: raycast

				// If all of the previous checks passed, this contact should
				// be visible with a marker
				AddUpdateTrackMarker(track);
			}

		}
		#endregion

		#region Interface
		/// <summary>
		/// Returns all of the radars on this grid which are not assigned
		/// to a radar system
		/// </summary>
		/// <returns></returns>
		public List<Radar> GetAvailableRadars() {
			_logger.log($"There are {_allRadars.Count} radars on this grid", "GetAvailableRadars");
			return _allRadars.Where((radar) => {
				RadarBlock r = radar.radarBlock;
				return !r.isAssigned;
			}).ToList();
		}

		/// <summary>
		/// Returns all of the radars assigned to this system
		/// </summary>
		/// <returns></returns>
		public List<Radar> GetAssignedRadars() {
			return _assignedRadars;
		}

		/// <summary>
		/// Assigns a radar to this system
		/// </summary>
		/// <param name="radar"></param>
		public void AssignRadar(Radar radar) {
			if (IsRadarCompatible(radar)) {
				_assignedRadars.Add(radar);
				radar.radarBlock.isAssigned = true;

				_settings.assignedIds.Add(radar.entID);
				SaveRadarSettings();
				
				RecalculateSectorCoverage();
				ReclassifySystem();
			}
		}

		/// <summary>
		/// Removes a radar from this system
		/// </summary>
		/// <param name="radar"></param>
		public void UnassignedRadar(Radar radar) {
			if (_assignedRadars.Contains(radar)) {
				_assignedRadars.Remove(radar);
				radar.radarBlock.isAssigned = false;

				_settings.assignedIds.Remove(radar.entID);
				SaveRadarSettings();

				RecalculateSectorCoverage();
				ReclassifySystem();
			}
		}

		public RadarType GetRadarType() {
			return _assignedType;
		}

		/// <summary>
		/// Returns the max range for this system in meters
		/// </summary>
		/// <returns></returns>
		public int GetRange() {
			return _settings.range;
		}

		/// <summary>
		/// Sets the range for this system in meters
		/// </summary>
		/// <param name="range"></param>
		public void SetRange(int range) {
			_settings.range = range;
			SaveRadarSettings();
		}

		/// <summary>
		/// Gets the operating frequency of this radar
		/// </summary>
		/// <returns></returns>
		public float GetFreq() {
			return _settings.frequency;
		}

		/// <summary>
		/// Sets the operating frequency, rounded to the nearest tenth of a GHz
		/// </summary>
		/// <param name="freq"></param>
		public void SetFreq(float freq) {
			_settings.frequency = (float)Math.Round(freq, 1);
			SaveRadarSettings();
		}

		public RadarSettings GetRadarSettings() {
			return _settings;
		}

		public void UpdateRadarSettings(RadarSettings updated) {
			_logger.debugLog("Updating settings", "UpdateRadarSettings");

			// Process assginments
			List<long> unassign = _settings.assignedIds.Except(updated.assignedIds).ToList();
			List<long> assign = updated.assignedIds.Except(_settings.assignedIds).ToList();
			foreach(Radar r in _allRadars) {
				if(unassign.Contains(r.entID)) {
					UnassignedRadar(r);
				} else if(assign.Contains(r.entID)) {
					AssignRadar(r);
				}
			}

			_settings.range = updated.range;
			_settings.frequency = updated.frequency;

			RecalculateSectorCoverage();
			ReclassifySystem();
		}

		public void ProcessAcquiredContacts(List<RemoteContact> contacts) {
			try {
				int n = 0, m = 0, l = 0;
				 
				// Invalidate all current tracks.  If these are still invalid 
				// at the end of the sweep, we will know which contacts are lost. 
				foreach (KeyValuePair<long, Track> t in _allTracks) {
					t.Value.lost = true;
				}

				foreach(RemoteContact c in contacts) {
					
					Track oldTrack = null;
					if (_allTracks.TryGetValue(c.entId, out oldTrack)) {
						m++;

						// This is an object we are already tracking
						// Update its values
						oldTrack.position = c.pos;
						oldTrack.xsec = c.xsec;
						oldTrack.lost = false;
						
						// If the object wasn't loaded on the client, check
						// if it is loaded now
						if(oldTrack.ent == null) {
							oldTrack.ent = MyAPIGateway.Entities.GetEntityById(c.entId);
							if(oldTrack.ent != null) {
								oldTrack.ent.OnClose += TrackedEntityUnloaded;
								if (oldTrack.marker != null)
									oldTrack.ent.Hierarchy.AddChild(oldTrack.marker);
								_logger.debugLog($"Entity associated with Track {oldTrack.trackId} is now available on the client", "ProcessAcquiredContacts");
							}
						}
					} else {
						// This is a new object to track
						n++;

						string id = c.entId.ToString();
						id = id.Substring(Math.Max(0, id.Length - 5));

						Track newTrack = new Track() {
							lost = false,
							ent = MyAPIGateway.Entities.GetEntityById(c.entId),
							marker = null,
							trackId = id,
							position = c.pos,
							xsec = c.xsec
						};

						if (newTrack.ent != null)
							newTrack.ent.OnClose += TrackedEntityUnloaded;

						_logger.debugLog($"Picked up new Track {id}, associated entity is " + 
							(newTrack.ent == null ? "null" : "not null") , "ProcessAcquiredContacts");
						_allTracks.Add(c.entId, newTrack);
					}

				}

				// Check which tracks were not marked valid during this sweep 
				// and prune them 
				List<long> remove = new List<long>();
				foreach (KeyValuePair<long, Track> t in _allTracks) {
					if (t.Value.lost) {
						l++;

						// Remove the marker
						ClearTrackMarker(t.Value);

						// Add to prune list 
						remove.Add(t.Key);
					}
				}
				foreach (long r in remove) {
					_allTracks.Remove(r);
				}

				_logger.debugLog(
					$"Track Summary: {n} new, {m} maintained, {l} lost", "ProcessAcquiredContacts");

			} catch (Exception e) {
				_logger.log(Logger.severity.ERROR, "ProcessAcquiredContacts",
					"Exception caught: " + e.ToString());
			}
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
		/// Updates (or creates a new, if the contact was previously not
		/// visible) the marker on a track
		/// </summary>
		/// <param name="t"></param>
		private void AddUpdateTrackMarker(Track t) {
			string title = $"~Track {t.trackId} ({(int)t.xsec}m²)~";

			if (t.marker == null) {
				// If the entity is not null, attach the marker directly to it
				// Otherwise create one in space and we'll update it manually
				if (t.ent != null) {
					t.marker = new MyAreaMarker(
						new MyPositionAndOrientation(t.ent.LocalAABB.Center, Vector3.Forward, Vector3.Up), 
						new MyAreaMarkerDefinition() {
							DisplayNameEnum = MyStringId.GetOrCompute(title),	
						});
					t.marker.AddHudMarker();
					t.ent.Hierarchy.AddChild(t.marker);
				} else {
					t.marker = new MyAreaMarker(
						new MyPositionAndOrientation(MatrixD.CreateFromTransformScale(
							Quaternion.Identity,
							t.position,
							new Vector3D(1,1,1))),
						new MyAreaMarkerDefinition() {
							DisplayNameEnum = MyStringId.GetOrCompute(title),
						});
					t.marker.AddHudMarker();
				}
				
			} else {
				//_logger.debugLog("Updating existing GPS marker", "AddUpdateTrackMarker");
				if (t.ent == null) {
					t.marker.PositionComp.SetPosition(t.position);
					t.marker.DisplayName = title;
				}
			}
		}

		/// <summary>
		/// Removes the marker from a track when it is not visible
		/// </summary>
		/// <param name="t"></param>
		private void ClearTrackMarker(Track t) {
			if (t.marker != null) {
				//MyAPIGateway.Session.GPS.RemoveLocalGps(t.marker);
				if (t.ent != null)
					t.ent.Hierarchy.RemoveChild(t.marker);
				t.marker.Close();
				t.marker = null;
			}
		}

		/// <summary>
		/// Runs through the list of radars on this grid and determines
		/// which sectors have coverage
		/// </summary>
		private void RecalculateSectorCoverage() {
			// Reset coverage to zero
			_coverage = Sector.NONE;

			foreach (Radar r in _assignedRadars) {
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

				_settings.range = Constants.radarMinimumRanges[(int)_assignedType];
				_settings.frequency = Constants.radarMinimumFreqs[(int)_assignedType];
			} else if(_assignedRadars.Count == 1 && _assignedType == RadarType.NONE) {
				_assignedType = _assignedRadars[0].type;

				_settings.range = Constants.radarMinimumRanges[(int)_assignedType];
				_settings.frequency = Constants.radarMinimumFreqs[(int)_assignedType];
			}
		}

		/// <summary>
		/// A radar is only compatible (and thus can be assigned)
		/// to this system if it has the same type as the radars already
		/// assigned, or if the system is unclassified.
		/// </summary>
		/// <param name="radar"></param>
		/// <returns></returns>
		private bool IsRadarCompatible(Radar radar) {
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
			if (!Helpers.IsServer)
				return;

			Entity.Storage[Constants.GUIDRadarSettings]
				= MyAPIGateway.Utilities.SerializeToXML<RadarSettings>(_settings);
			_logger.debugLog("Saved", "SaveRadarSettings");
		}

		/// <summary>
		/// Deserializes data saved in the world file
		/// </summary>
		private void LoadSavedData() {
			if (Helpers.IsServer) {
				_logger.debugLog("Starting load", "LoadSavedData");

				if (Entity.Storage.ContainsKey(Constants.GUIDRadarSettings)) {

					_logger.debugLog("Loading saved radar settings: " + Entity.Storage[Constants.GUIDRadarSettings], "LoadSavedData");
					RadarSettings settings = MyAPIGateway.Utilities.SerializeFromXML<RadarSettings>(Entity.Storage[Constants.GUIDRadarSettings]);

					UpdateRadarSettings(settings);

				}
			} else {
				_logger.debugLog("Getting radar settings from server", "LoadSavedData");
				Message<long, long> request
					= new Message<long, long>(Entity.EntityId, 0);
				MyAPIGateway.Multiplayer.SendMessageToServer(
					Constants.MIDGetRadarSettingsServer,
					request.ToXML());
			}
		}
		#endregion

	}
}
