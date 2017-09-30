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

namespace SEEW.Core {
	/// <summary>
	/// Hooks into the SE session
	/// </summary>
	[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
	public class CoreComponent : MySessionComponentBase {

		#region Instance Members
		private Logger logger;

		private bool initialized = false;

		private List<IMyTerminalControl> phasedRadarControls 
			= new List<IMyTerminalControl>();
		#endregion

		#region Lifecycle
		public CoreComponent() {
			// Empty
		}

		public override void Init(MyObjectBuilder_SessionComponent sessionComponent) {
			base.Init(sessionComponent);

			logger = new Logger("EWar", "CoreComponent");
		}

		public override void UpdateBeforeSimulation() {
			// Initialize on the first frame with Session is not null
			if(!initialized) {
				if(MyAPIGateway.Session != null) {

					MakeControls();

					// Hook into the UI terminal controls so we can remove antenna
					// controls we don't like from the radar
					MyAPIGateway.TerminalControls.CustomControlGetter += ControlGetter;

					//MyAPIGateway.Multiplayer.RegisterMessageHandler(Constants.MIDRadarSettings, HandleRadarSystemSettings);

					logger.debugLog("Initialized", "UpdateBeforeSimulation");
					logger.debugLog("IsServer = " + Helpers.IsServer, "UpdateBeforeSimulation");

					initialized = true;
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

			logger.debugLog($"Hit for block {def.TypeId} {def.SubtypeId}", "ControlGetter");

			if (def.TypeId == typeof(MyObjectBuilder_UpgradeModule)
				&& def.SubtypeId == "EWRadarSearchPhased") {

				controls.AddRange(phasedRadarControls);

			}
		}

		/// <summary>
		/// Creates all of the custom controls for the blocks
		/// </summary>
		private void MakeControls() {

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
