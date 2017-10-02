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

		#region Singleton
		private EWRegistry<T> _instance = null;
		public EWRegistry<T> Instance {
			get {
				if (_instance == null)
					_instance = new EWRegistry<T>();
				return _instance;
			}
		}

		private EWRegistry() {
			// Empty
		}
		#endregion

		private Dictionary<long, T> _systems;

		public void Register(long grid, T add) {
			if (!_systems.ContainsKey(grid))
				_systems.Add(grid, add);
		}

		public void Unregister(long grid) {
			if (_systems.ContainsKey(grid))
				_systems.Remove(grid);
		}

		public T Get(long grid) {
			T system = default(T);
			if (_systems.TryGetValue(grid, out system))
				return system;
			else
				return default(T);
		}

	}
}
