using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.Game.Components;
using VRage.ObjectBuilders;

using SEEW.Grids;
using SEEW.Utility;
using VRage.Utils;
using SEEW.Records;
using VRage.Game.ModAPI.Ingame;
using SEEW.Blocks;
using VRage.ModAPI;

namespace SEEW.Core {
	/// <summary>
	/// Hooks into the SE session
	/// </summary>
	[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
	public class CoreComponent : MySessionComponentBase {

		#region Instance Members
		private Logger _logger;

		private bool _initialized = false;

		private List<IMyTerminalControl> _radarControls 
			= new List<IMyTerminalControl>();

		// Used for listboxes
		private List<RadarController.Radar> _selectedUnassigned 
			= new List<RadarController.Radar>();
		private List<RadarController.Radar> _selectedAssigned
			= new List<RadarController.Radar>();
		#endregion

		#region Lifecycle
		public CoreComponent() {
			// Empty
		}

		public override void Init(MyObjectBuilder_SessionComponent sessionComponent) {
			base.Init(sessionComponent);

			_logger = new Logger("EWar", "CoreComponent");
		}

		public override void UpdateBeforeSimulation() {
			// Initialize on the first frame with Session is not null
			if(!_initialized) {
				if(MyAPIGateway.Session != null) {

					MakeControls();

					// Hook into the UI terminal controls so we can remove antenna
					// controls we don't like from the radar
					MyAPIGateway.TerminalControls.CustomControlGetter += ControlGetter;

					//MyAPIGateway.Multiplayer.RegisterMessageHandler(Constants.MIDRadarSettings, HandleRadarSystemSettings);

					_logger.debugLog("Initialized", "UpdateBeforeSimulation");
					_logger.debugLog("IsServer = " + Helpers.IsServer, "UpdateBeforeSimulation");

					_initialized = true;
				}
			}
		}
		#endregion

		#region Custom Controls
		/// <summary>
		/// Customizes the control panels for certain blocks
		/// </summary>
		/// <param name="block"></param>
		/// <param name="controls"></param>
		private void ControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls) {
			

			SerializableDefinitionId def = block.SlimBlock.FatBlock.BlockDefinition;

			_logger.debugLog($"Hit for block {def.TypeId} {def.SubtypeId}", "ControlGetter");

			if (def.TypeId == typeof(MyObjectBuilder_UpgradeModule)
				&& def.SubtypeId == "EWControllerRadar") {

				controls.AddRange(_radarControls);
			}
		}

		/// <summary>
		/// Creates all of the custom controls for the blocks
		/// </summary>
		private void MakeControls() {

			IMyTerminalControlSeparator sep1
				= MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyUpgradeModule>("Sep1");
			_radarControls.Add(sep1);

			IMyTerminalControlSlider rangeSlider
				= MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyUpgradeModule>("RangeSlider");
			rangeSlider.Title = MyStringId.GetOrCompute("Range");
			rangeSlider.Tooltip = MyStringId.GetOrCompute("Maximum range of this radar system");
			rangeSlider.SetLimits(100, 15000);
			rangeSlider.Getter = (block) => {
				RadarController controller = block.GameLogic.GetAs<RadarController>();
				return controller.GetRange();
			};
			rangeSlider.Setter = (block, value) => {
				RadarController controller = block.GameLogic.GetAs<RadarController>();
				controller.SetRange((int)value);
				//SendRadarSystemSettings(block.CubeGrid.EntityId);
			};
			rangeSlider.Writer = (block, str) => {
				RadarController controller = block.GameLogic.GetAs<RadarController>();
				str.Append(controller.GetRange() + "m");
			};
			_radarControls.Add(rangeSlider);

			IMyTerminalControlSlider freqSlider
				= MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyUpgradeModule>("FreqSlider");
			freqSlider.Title = MyStringId.GetOrCompute("Frequency");
			freqSlider.Tooltip = MyStringId.GetOrCompute("Operating frequency of this system");
			freqSlider.SetLimits(8.0f,12.0f);
			freqSlider.Getter = (block) => {
				RadarController controller = block.GameLogic.GetAs<RadarController>();
				return controller.GetFreq();
			};
			freqSlider.Setter = (block, value) => {
				RadarController controller = block.GameLogic.GetAs<RadarController>();
				controller.SetFreq(value);
				//SendRadarSystemSettings(block.CubeGrid.EntityId);
			};
			freqSlider.Writer = (block, str) => {
				RadarController controller = block.GameLogic.GetAs<RadarController>();
				str.Append(controller.GetFreq() + "GHz");
			};
			_radarControls.Add(freqSlider);

			IMyTerminalControlSeparator sep2
				= MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyUpgradeModule>("Sep2");
			_radarControls.Add(sep2);

			IMyTerminalControlListbox unassignedList
					= MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, IMyUpgradeModule>("UnassignedList");
			unassignedList.Title = MyStringId.GetOrCompute("Available");
			unassignedList.Tooltip = MyStringId.GetOrCompute("Radar blocks which are able to be assigned to this system.");
			unassignedList.Multiselect = true;
			unassignedList.VisibleRowsCount = 6;
			unassignedList.ListContent = (block, items, selected) => {
				RadarController controller 
					= block.GameLogic.GetAs<RadarController>();
				IEnumerable<RadarController.Radar> available 
					= controller.GetAvailableRadars();

				foreach (RadarController.Radar r in available) {
					MyTerminalControlListBoxItem item
						= new MyTerminalControlListBoxItem(
								MyStringId.GetOrCompute(r.block.FatBlock.DisplayNameText),
								MyStringId.GetOrCompute(r.type.ToString()),
								r
							);
					items.Add(item);
				}
			};
			unassignedList.ItemSelected = (block, items) => {
				_selectedUnassigned.Clear();
				
				foreach(MyTerminalControlListBoxItem item in items) {
					_selectedUnassigned.Add(item.UserData as RadarController.Radar);
				}
			};
			_radarControls.Add(unassignedList);

			IMyTerminalControlButton addButton
				= MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyUpgradeModule>("AddButton");
			addButton.Title = MyStringId.GetOrCompute("Assign");
			addButton.Tooltip = MyStringId.GetOrCompute("Assign the selected radar to this system.");
			_radarControls.Add(addButton);

			IMyTerminalControlListbox assignedList
					= MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, IMyUpgradeModule>("AssignedList");
			assignedList.Title = MyStringId.GetOrCompute("Assigned");
			assignedList.Tooltip = MyStringId.GetOrCompute("Radar blocks which are currently assigned to this system.");
			assignedList.Multiselect = true;
			assignedList.VisibleRowsCount = 6;
			assignedList.ListContent = (block, items, selected) => {
				RadarController controller
					= block.GameLogic.GetAs<RadarController>();
				List<RadarController.Radar> assigned
					= controller.GetAssignedRadars();

				foreach (RadarController.Radar r in assigned) {
					MyTerminalControlListBoxItem item
						= new MyTerminalControlListBoxItem(
								MyStringId.GetOrCompute(r.block.FatBlock.DisplayNameText),
								MyStringId.GetOrCompute(r.type.ToString()),
								r
							);
					items.Add(item);
				}
			};
			assignedList.ItemSelected = (block, items) => {
				_selectedAssigned.Clear();

				foreach (MyTerminalControlListBoxItem item in items) {
					_selectedAssigned.Add(item.UserData as RadarController.Radar);
				}
			};
			_radarControls.Add(assignedList);
			
			// Add button action must be after assigned list because it
			// needs the pointer
			addButton.Action = (block) => {
				RadarController controller
					= block.GameLogic.GetAs<RadarController>();

				foreach (RadarController.Radar radar in _selectedUnassigned) {
					controller.AssignRadar(radar);
				}

				unassignedList.UpdateVisual();
				assignedList.UpdateVisual();
			};

			IMyTerminalControlButton removeButton
				= MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyUpgradeModule>("AddButton");
			removeButton.Title = MyStringId.GetOrCompute("Remove");
			removeButton.Tooltip = MyStringId.GetOrCompute("Remove the selected radars from the system.");
			removeButton.Action = (block) => {
				RadarController controller
					= block.GameLogic.GetAs<RadarController>();

				foreach (RadarController.Radar radar in _selectedAssigned) {
					controller.UnassignedRadar(radar);
				}

				unassignedList.UpdateVisual();
				assignedList.UpdateVisual();
			};
			_radarControls.Add(removeButton);


			//
			// Range Slider
			/*IMyTerminalControlSlider rangeSlider
				= MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyUpgradeModule>("RangeSlider");
			rangeSlider.Title = MyStringId.GetOrCompute("Range");
			rangeSlider.Tooltip = MyStringId.GetOrCompute("Maximum range of this radar system (Affects all radars on this grid of the same type)");
			rangeSlider.SetLimits(100, 15000);
			rangeSlider.Getter = (block) => {
				RadarManager radar = block.CubeGrid.GameLogic.GetAs<RadarManager>();
				if (radar != null)
					return radar.GetRadarSettings().range;
				else
					return 100;
			};
			rangeSlider.Setter = (block, value) => {
				RadarManager radar = block.CubeGrid.GameLogic.GetAs<RadarManager>();
				if (radar != null) {
					radar.GetRadarSettings().range = (int)value;
					SendRadarSystemSettings(block.CubeGrid.EntityId);
				}
			};
			rangeSlider.Writer = (block, str) => {
				RadarManager radar = block.CubeGrid.GameLogic.GetAs<RadarManager>();
				if (radar != null)
					str.Append(radar.GetRadarSettings().range + "m");
			};
			phasedRadarControls.Add(rangeSlider);*/

		}
		#endregion

		#region Message Handlers
		/*private void SendRadarSystemSettings(long grid) {
			RadarManager radar = RadarManager.GetForGrid(grid);

			Message<long, RadarSystemSettings> msg
				= new Message<long, RadarSystemSettings>(grid, radar.GetRadarSettings());

			if(Helpers.IsServer) {
				logger.debugLog($"RadarSystemSettings -> Clients for grid {grid}", "SendRadarSystemSettings");
				MyAPIGateway.Multiplayer.SendMessageToOthers(
					Constants.MIDRadarSettings,
					msg.ToXML());
			} else {
				logger.debugLog($"RadarSystemSettings -> Server for grid {grid}", "SendRadarSystemSettings");
				MyAPIGateway.Multiplayer.SendMessageToServer(
					Constants.MIDRadarSettings,
					msg.ToXML());
			}
		}

		private void HandleRadarSystemSettings(byte[] data) {
			try {
				Message<long, RadarSystemSettings> msg
					= Message<long, RadarSystemSettings>.FromXML(data);
				if (msg == null)
					logger.debugLog("Msg is null", "HandleRadarSystemSettings");

				logger.debugLog($"Got radar settings update for grid {msg.Key}", "HandleRadarSystemSettings");

				VRage.ModAPI.IMyEntity ent = MyAPIGateway.Entities.GetEntityById(msg.Key);
				if(ent == null)
					logger.debugLog($"No Entity with ID {msg.Key} found", "HandleRadarSystemSettings");

				RadarManager radar = RadarManager.GetForGrid(msg.Key);
				if(radar == null)
					logger.debugLog("Radar is null", "HandleRadarSystemSettings");
				if (msg.Value == null)
					logger.debugLog("Value is null", "HandleRadarSystemSettings");
				radar.UpdateRadarSettings(msg.Value);

				if (Helpers.IsServer)
					SendRadarSystemSettings(msg.Key);
			} catch(Exception e) {
				logger.log(Logger.severity.ERROR, "HandleRadarSystemSettings",
					"Exception caught: " + e.ToString());
			}
		}*/
		#endregion

	}
}
