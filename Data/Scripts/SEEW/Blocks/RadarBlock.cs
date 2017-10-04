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

	/// <summary>
	/// Attached to all radar blocks.  Contains some simple data
	/// about their use.
	/// </summary>
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, new string[] { "EWRadarSearchPhased", "EWRadarSearchRotating" })]
    public class RadarBlock : MyGameLogicComponent
    {

		/// <summary>
		/// Set to true if any radar system is using this block
		/// </summary>
		public bool isAssigned { get; set; }

		public override void Init(MyObjectBuilder_EntityBase objectBuilder) {
			base.Init(objectBuilder);
		}

	}

}
