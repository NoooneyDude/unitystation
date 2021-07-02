using Systems.Atmospherics;
using UnityEngine;

namespace Objects.Atmospherics
{
	/// <summary>Allows an <seealso cref="ACUDevice"/> to be used for air quality sampling and control.</summary>
	public interface IACUControllable
	{
		/// <summary>The <c>GasMix</c> the device reports for sampling. Typically is for the device's tile.</summary>
		GasMix AmbientGasMix { get; }

		/// <summary>
		/// The operating mode the controlling <see cref="AirController"/> has indicated the device should operate with.
		/// <para>Each device type is responsible for interpreting their own behaviour from the <c>ACU</c>'s operating mode.</para>
		/// </summary>
		void SetOperatingMode(ACUMode mode);
	}

	/// <summary>
	/// Allows an object with this component to be controlled by an <seealso cref="AirController"/>.
	/// <para>When the object is initialised by the server, the referenced (mapped) <c>ACU</c> connects to the device.</para>
	/// <para>See also <seealso cref="CustomInspectors.ACUDeviceInspector"/>.</para>
	/// </summary>
	public class ACUDevice : MonoBehaviour, IServerLifecycle, ISetMultitoolSlave
	{
		/// <summary>The controller this device should link with at server initialisation.</summary>
		public AirController Controller;

		public MultitoolConnectionType ConType => MultitoolConnectionType.ACU;

		private IACUControllable device;

		private void Awake()
		{
			device = GetComponent<IACUControllable>();
			if (device == null)
			{
				Logger.LogError($"{this} has no component that implements {nameof(IACUControllable)}!");
			}
		}

		public void OnSpawnServer(SpawnInfo info)
		{
			if (Controller == null) return;
			Controller.AddSlave(device);
		}

		public void OnDespawnServer(DespawnInfo info)
		{
			if (Controller == null) return;
			Controller.RemoveSlave(device);
		}

		public void SetMaster(ISetMultitoolMaster master)
		{
			if (Controller != null)
			{
				Controller.RemoveSlave(device);
			}

			Controller = (AirController)master;
			Controller.AddSlave(device);
		}
	}
}
