using SEEW.Blocks;
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
			_systems = new Dictionary<long, T>();
		}
		#endregion

		private Dictionary<long, T> _systems;

		public void Register(long grid, T add) {
			if (!_systems.ContainsKey(grid)) {
				_systems.Add(grid, add);
				_logger.debugLog($"New system registered for grid {grid}.", "Register");
				DebugDump();
			}
		}

		public void Unregister(long grid) {
			if (_systems.ContainsKey(grid)) {
				_systems.Remove(grid);
				_logger.debugLog($"System unregistered for grid {grid}", "Unregister");
				DebugDump();
			}
		}

		public T Get(long grid) {
			T system = default(T);
			if (_systems.TryGetValue(grid, out system))
				return system;
			else
				return default(T);
		}

		private void DebugDump() {
			string list = "Registry now contains:\n";
			foreach (KeyValuePair<long, T> entry in _systems) {
				list += entry.ToString() + "\n";
			}
			_logger.debugLog(list, "DebugDump");
		}

	}
}
