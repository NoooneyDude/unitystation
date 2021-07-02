using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;
using Systems.Atmospherics;
using Systems.Electricity;
using Objects.Wallmounts;
using UI.Objects.Atmospherics.ACU;

namespace Objects.Atmospherics
{
	/// <summary>
	/// Represents an <c>ACU</c>'s air quality status.
	/// <remarks>Note: maps to <c>ACU</c> sprite state.</remarks>
	/// </summary>
	public enum ACUStatus
	{
		Off = 0,
		Nominal = 1,
		Caution = 2,
		Alert = 3,
	}

	/// <summary> All operating modes for the <c>ACU</c>.</summary>
	public enum ACUMode
	{
		Off = 0,
		Filtering = 1,
		Contaminated = 2,
		Draught = 3,
		Refill = 4,
		Cycle = 5,
		Siphon = 6,
		PanicSiphon = 7,
	}

	/// <summary>
	/// <para>Main component for the <c>ACU</c> (known as the air alarm in SS13).</para>
	/// <para>Monitors the local air quality and controls connected <c>ACU</c> devices, such as vents and scrubbers.</para>
	/// <remarks>See also related classes:<list type="bullet">
	/// <item><description><seealso cref="GUI_ACU"/> handles the <c>ACU</c>'s GUI</description></item>
	/// <item><description><seealso cref="ACUDevice"/> allows a connection to form between the <c>ACU</c> and devices</description></item>
	/// </list></remarks>
	/// </summary>
	[RequireComponent(typeof(WallmountBehavior))]
	[RequireComponent(typeof(AccessRestrictions))]
	public class AirController : MonoBehaviour, IServerSpawn, IAPCPowerable, ISetMultitoolMaster, ICheckedInteractable<HandApply>
	{
		[InfoBox("Several presets exist for server rooms, cold rooms, etc. Add as desired.")]

		[SerializeField]
		[Tooltip("Initial operating mode for this ACU and its slaved devices.")]
		private ACUMode initialOperatingMode = ACUMode.Filtering;

		[SerializeField]
		[Tooltip("Initial thresholds this ACU should use to determine air quality.")]
		private ACUThresholds initialThresholds = new ACUThresholds();

		private MetaDataNode facingMetaNode;
		private AccessRestrictions accessRestrictions;
		private SpriteHandler spriteHandler;

		/// <summary>Invoked when the air controller's state changes.</summary>
		public Action OnStateChanged;

		public readonly HashSet<IACUControllable> ConnectedDevices = new HashSet<IACUControllable>();

		/// <summary>
		/// The mode this controller and the devices it controls should operate with,
		/// given other conditions, like being powered, are met.
		/// </summary>
		public ACUMode DesiredMode { get; private set; } = ACUMode.Filtering;

		/// <summary>
		/// We piggyback off a <c>GasMix</c> to represent the average <c>GasMix</c> over all devices, including the controller.
		/// <para>For data representation only.</para>
		/// </summary>
		public GasMix AverageGasMix { get; private set; }

		public ACUThresholds Thresholds { get; private set; }

		public bool IsWriteable => IsPowered && IsLocked == false;

		#region Lifecycle

		private void Awake()
		{
			accessRestrictions = GetComponent<AccessRestrictions>();
			spriteHandler = GetComponentInChildren<SpriteHandler>();

			Thresholds = initialThresholds;
			DesiredMode = initialOperatingMode;
		}

		private void OnEnable()
		{
			UpdateManager.Add(PeriodicUpdate, 3);
		}

		private void OnDisable()
		{
			UpdateManager.Remove(CallbackType.PERIODIC_UPDATE, PeriodicUpdate);
		}

		public void OnSpawnServer(SpawnInfo info)
		{
			var registerTile = gameObject.RegisterTile();
			var wallmountBehaviour = GetComponent<WallmountBehavior>();
			facingMetaNode = registerTile.Matrix.MetaDataLayer.Get(wallmountBehaviour.CalculateFacing().CutToInt());
		}

		#endregion

		private void PeriodicUpdate()
		{
			AverageGasMix = GetAverageGasMix();
			UpdateStatusProperties();
			spriteHandler.ChangeSprite((int)OverallStatus);
		}

		private GasMix GetAverageGasMix()
		{
			// Sample the gas at the ACU.
			GasMix avgMix = GasMix.NewGasMix(facingMetaNode.GasMix);
			if (ConnectedDevices.Count < 1) return avgMix;

			// Sample the gas at all connected devices.
			foreach (IACUControllable device in ConnectedDevices)
			{
				avgMix.Pressure += device.AmbientGasMix.Pressure;
				avgMix.Temperature += device.AmbientGasMix.Temperature;
				foreach (Gas gas in Gas.All)
				{
					avgMix.Gases[gas] += device.AmbientGasMix.Gases[gas.Index];
				}
			}

			avgMix.Pressure /= ConnectedDevices.Count;
			avgMix.Temperature /= ConnectedDevices.Count;
			foreach (Gas gas in Gas.All)
			{
				avgMix.Gases[gas] /= ConnectedDevices.Count;
			}

			return avgMix;
		}

		#region Status

		public ACUStatus OverallStatus { get; private set; } = ACUStatus.Off;
		public ACUStatus PressureStatus { get; private set; }
		public ACUStatus TemperatureStatus { get; private set; }
		public ACUStatus[] GasLevelStatus { get; private set; } = new ACUStatus[Gas.Count];
		public ACUStatus CompositionStatus { get; private set; }

		private void UpdateStatusProperties()
		{
			PressureStatus = GetMetricStatus(Thresholds.Pressure, AverageGasMix.Pressure);
			TemperatureStatus = GetMetricStatus(Thresholds.Temperature, AverageGasMix.Temperature);
			CompositionStatus = ACUStatus.Nominal;
			foreach (var gas in Gas.All)
			{
				GasLevelStatus[gas.Index] = Thresholds.GasMoles.ContainsKey(gas)
						? GetMetricStatus(Thresholds.GasMoles[gas], AverageGasMix.Gases[gas])
						: GasLevelStatus[gas.Index] = ACUStatus.Nominal;
				CompositionStatus = GasLevelStatus[gas.Index] > CompositionStatus
						? GasLevelStatus[gas.Index]
						: CompositionStatus;
			}

			OverallStatus = PressureStatus;
			OverallStatus = TemperatureStatus > OverallStatus ? TemperatureStatus : OverallStatus;
			OverallStatus = CompositionStatus > OverallStatus ? CompositionStatus : OverallStatus;
		}

		private ACUStatus GetMetricStatus(float[] thresholds, float value)
		{
			if (value < thresholds[0]) return ACUStatus.Alert;
			if (value > thresholds[3]) return ACUStatus.Alert;
			if (value > thresholds[2]) return ACUStatus.Caution;
			if (value < thresholds[1]) return ACUStatus.Caution;

			return ACUStatus.Nominal;
		}

		#endregion

		#region UI

		public void RequestImmediateUpdate()
		{
			PeriodicUpdate();
		}

		public void SetOperatingMode(ACUMode mode)
		{
			if (IsWriteable == false) return;

			DesiredMode = mode;
			foreach (var device in ConnectedDevices)
			{
				device.SetOperatingMode(mode);
			}

			OnStateChanged?.Invoke();
		}

		public void ResetThresholds()
		{
			if (IsWriteable == false) return;

			Thresholds = initialThresholds;
		}

		#endregion

		#region Interaction-ToggleLocked

		public bool IsLocked { get; private set; } = true;

		public bool WillInteract(HandApply interaction, NetworkSide side)
		{
			if (DefaultWillInteract.Default(interaction, side) == false) return false;

			return Validations.HasUsedItemTrait(interaction, CommonTraits.Instance.Id);
		}

		public void ServerPerformInteraction(HandApply interaction)
		{
			if (accessRestrictions.CheckAccessCard(interaction.HandObject))
			{
				IsLocked = !IsLocked;

				Chat.AddActionMsgToChat(interaction.Performer,
						$"You {(IsLocked ? "lock" : "unlock")} the air controller unit.",
						$"{interaction.PerformerPlayerScript.visibleName} {(IsLocked ? "locks" : "unlocks")} the air controller unit.");

				OnStateChanged?.Invoke();
			}
		}

		#endregion

		#region IAPCPowerable

		public bool IsPowered { get; private set; }

		public void StateUpdate(PowerState state)
		{
			switch (state)
			{
				case PowerState.On:
				case PowerState.LowVoltage:
				case PowerState.OverVoltage:
					IsPowered = true;
					UpdateManager.Add(PeriodicUpdate, 3);
					spriteHandler.ChangeSprite((int)ACUStatus.Nominal);
					break;
				case PowerState.Off:
					OverallStatus = ACUStatus.Off;
					IsPowered = false;
					UpdateManager.Remove(CallbackType.PERIODIC_UPDATE, PeriodicUpdate);
					spriteHandler.ChangeSprite((int)ACUStatus.Off);
					break;
			}

			PeriodicUpdate();
			OnStateChanged?.Invoke();
		}

		public void PowerNetworkUpdate(float voltage) { }

		#endregion

		#region Multitool

		bool ISetMultitoolMaster.MultiMaster => false;

		public MultitoolConnectionType ConType => MultitoolConnectionType.ACU;

		public void AddSlave(IACUControllable device)
		{
			ConnectedDevices.Add(device);
			device.SetOperatingMode(DesiredMode);

			OnStateChanged?.Invoke();
		}

		public void RemoveSlave(IACUControllable device)
		{
			ConnectedDevices.Remove(device);

			OnStateChanged?.Invoke();
		}

		// Let the slave device register itself
		void ISetMultitoolMaster.AddSlave(object device) { }

		#endregion
	}

	// TODO: consider standalone class
	/// <summary>
	/// Stores threshold values by which <seealso cref="AirController"/>s can determine air quality.
	/// <para>Threshold values are in the order of
	/// <c>AlertMin</c>, <c>CautionMin</c>, <c>CautionMax</c>, <c>AlertMax</c>.</para>
	/// </summary>
	[Serializable]
	public class ACUThresholds
	{
		[InfoBox("Threshold values are in the order of AlertMin, CautionMin, CautionMax, AlertMax.")]

		/// <summary> In kPa </summary>
		public float[] Pressure = new float[4]
		{
			AtmosConstants.HAZARD_LOW_PRESSURE,
			AtmosConstants.WARNING_LOW_PRESSURE,
			AtmosConstants.WARNING_HIGH_PRESSURE,
			AtmosConstants.HAZARD_HIGH_PRESSURE,
		};

		/// <summary> In Celsius </summary>
		public float[] Temperature = new float[4] { 273.15f, 283.15f, 313.15f, 339.15f };

		/// <summary> In moles per tile </summary>
		public Dictionary<Gas, float[]> GasMoles = new Dictionary<Gas, float[]>
		{
			{ Gas.Nitrogen, harmlessGas },
			{ Gas.Oxygen, new float[4] { 16, 19, 135, 140 } },
			{ Gas.CarbonDioxide, new float[4] { -1, -1, 5, 10 } },
			{ Gas.NitrousOxide, dangerousGas },
			{ Gas.Hydrogen, dangerousGas },
			{ Gas.WaterVapor, dangerousGas },
			{ Gas.Tritium, dangerousGas },
			{ Gas.Plasma, dangerousGas },
		};

		private static readonly float[] harmlessGas = { -1, -1, 1000, 1000 };
		private static readonly float[] dangerousGas = { -1, -1, 0.2f, 0.5f };
	}
}
