using Sandbox.Game.Entities.Character.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.Game.Components;

namespace SEEW.Grids {

	/// <summary>
	/// Component attached to the player
	/// </summary>
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_Character), true)]
	public class Player : MyCharacterComponent {
		private Logger logger;

		public Player() {

		}

		public override void Init(MyComponentDefinitionBase definition) {
			base.Init(definition);

			logger = new Logger("Player", "Player");

			Entity.NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.EACH_100TH_FRAME;
			
		}

		public override void UpdateBeforeSimulation100() {
			logger.debugLog("Character Update", "UpdateBeforeSimulation100");
		}
	}
}
