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

namespace SEEW.Core {
	/// <summary>
	/// Hooks into the SE session
	/// </summary>
	[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
	public class CoreComponent : MySessionComponentBase {

		private Logger logger;

		private bool initialized = false;

		public override void Init(MyObjectBuilder_SessionComponent sessionComponent) {
			base.Init(sessionComponent);

			logger = new Logger("EWar", "CoreComponent");
		}

		public override void UpdateBeforeSimulation() {
			// Initialize on the first frame with Session is not null
			if(!initialized)
				{
				if(MyAPIGateway.Session != null) {
					// Hook into the UI terminal controls so we can remove antenna
					// controls we don't like from the radar
					MyAPIGateway.TerminalControls.CustomControlGetter += ControlGetter;

					logger.debugLog("Initialized", "UpdateBeforeSimulation");

					initialized = true;
				}
			}
		}

		/// <summary>
		/// Customizes the control panels for certain blocks
		/// </summary>
		/// <param name="block"></param>
		/// <param name="controls"></param>
		private void ControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls) {
			

			SerializableDefinitionId def = block.SlimBlock.FatBlock.BlockDefinition;

			logger.debugLog($"Hit for block {def.TypeId} {def.SubtypeId}", "ControlGetter");

			if (def.TypeId == typeof(MyObjectBuilder_RadioAntenna)
				&& def.SubtypeId.Equals("EWPhasedRadar")) {

				/* Default controls for an Antenna
				 * 
				 *  0 OnOff
					1 Divider
					2 ShowInTerminal
					3 ShowInToolbarConfig
					4 CustomData
					5 CustomName
					6 Divider
					7 PBList
					8 Divider
					9 Radius
					10 EnableBroadCast
					11 ShowShipName
					12 Divider
					13 IgnoreAlliedBroadcast
					14 IgnoreOtherBroadcast
				*
				*/

				// TODO: Figure out a less fragile way to do this
				/*List<IMyTerminalControl> savedControls = new List<IMyTerminalControl>(controls);
				controls.Clear();

				controls.Add(savedControls[0]);
				controls.Add(savedControls[1]);
				controls.Add(savedControls[2]);
				controls.Add(savedControls[3]);
				controls.Add(savedControls[4]);
				controls.Add(savedControls[5]);
				controls.Add(savedControls[6]);
				controls.Add(savedControls[9]);
				controls.Add(savedControls[10]);*/

				// TODO: Change the title of the range slider
				//(savedControls[9] as IMyTerminalControlSlider).Title.
			}
		}

	}
}
