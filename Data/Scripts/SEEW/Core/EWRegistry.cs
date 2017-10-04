using SEEW.Blocks;
using SEEW.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SEEW.Core {

	/// <summary>
	/// Used for registering all EW systems of a given type in the world
	/// </summary>
	public class EWRegistry<T> {

		private Logger _logger;

		#region Singleton
		private static EWRegistry<T> _instance = null;
		public static EWRegistry<T> Instance {
			get {
				if (_instance == null)
					_instance = new EWRegistry<T>();
				return _instance;
			}
		}

		private EWRegistry() {
			_logger = new Logger(typeof(T).ToString(), "EWRegistry");
			_systems = new Dictionary<long, Dictionary<long, T>>();
		}
		#endregion

		private Dictionary<long, Dictionary<long, T>> _systems;

		public void Register(long grid, long block, T add) {
			Dictionary<long, T> forGrid;
			if(!_systems.TryGetValue(grid, out forGrid)) {
				_logger.debugLog($"Creating dictionary for grid {grid}", "Register");
				forGrid = new Dictionary<long, T>();
				_systems.Add(grid, forGrid);
			}

			if (!forGrid.ContainsKey(block)) {
				forGrid.Add(block, add);
				_logger.debugLog($"New system registered for grid {grid}.", "Register");
				DebugDump();
			}
		}

		public void Unregister(long grid, long block) {
			Dictionary<long, T> forGrid;
			if (!_systems.TryGetValue(grid, out forGrid)) {
				_logger.debugLog($"Could not unregister system {block} on grid {grid} because it is not registered", "Unregister");
				return;
			}

			if (forGrid.ContainsKey(block)) {
				forGrid.Remove(block);
				_logger.debugLog($"System {block} unregistered for grid {grid}", "Unregister");
				DebugDump();
			}
		}

		public T Get(BlockAddress addr) {
			return Get(addr.grid, addr.block);
		}

		public T Get(long grid, long block) {
			Dictionary<long, T> forGrid;
			if(!_systems.TryGetValue(grid, out forGrid)) {
				_logger.debugLog($"Grid {grid} not found", "Get");
				return default(T);
			}

			T system = default(T);
			if (forGrid.TryGetValue(block, out system)) {
				return system;
			} else {
				_logger.debugLog($"System {block} not found on grid {grid}", "Get");
				return default(T);
			}
		}

		private void DebugDump() {
			string list = "Registry now contains:\n";
			foreach (KeyValuePair<long, Dictionary<long, T>> grid in _systems) {
				list += $"Grid {grid.Key} => ";
				foreach (KeyValuePair<long, T> system in grid.Value) {
					list += system.Key + " ";
				}
				list += "\n";
			}
			_logger.debugLog(list, "DebugDump");
		}

	}
}
