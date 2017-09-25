using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Sandbox.Common;
using Sandbox.Definitions;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Engine;
using Sandbox.Game;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage.Game.Components;
using VRage.Game;
using VRage.ObjectBuilders;

namespace SEEW.Blocks {

	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_RadioAntenna), true, "PhasedRadar")]
    public class PhasedRadar : MyGameLogicComponent
    {
		public override void Init(MyObjectBuilder_EntityBase objectBuilder) {
			base.Init(objectBuilder);
			
			MyAPIGateway.Utilities.ShowNotification("Phased Radar Created", 1000);
			
		}
	}

}
