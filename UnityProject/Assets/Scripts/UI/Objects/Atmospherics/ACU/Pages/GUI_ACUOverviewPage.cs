using System.Collections.Generic;
using UnityEngine;
using Systems.Atmospherics;
using Objects.Atmospherics;

namespace UI.Objects.Atmospherics.ACU
{
	/// <summary>
	/// Allows the peeper to view the local air quality as reported by the <see cref="AirController"/>.
	/// </summary>
	public class GUI_ACUOverviewPage : GUI_ACUPage
	{
		[SerializeField]
		private NetLabel modeLabel = default;
		[SerializeField]
		private NetLabel pressureLabel = default;
		[SerializeField]
		private NetLabel temperatureLabel = default;
		[SerializeField]
		private NetLabel compositionLabel = default;

		[SerializeField]
		private EmptyItemList metricsContainer = default;

		public override void OnPageDeactivated()
		{
			metricsContainer.Clear();
		}

		public override void OnPeriodicUpdate()
		{
			modeLabel.SetValueServer($"Mode: {Acu.DesiredMode}");
			UpdateLabels();

			List<Gas> gasesToDisplay = new List<Gas>();
			foreach (var gas in Gas.All)
			{
				if (Acu.AverageGasMix.Gases[gas.Index].Approx(0)) continue;
				gasesToDisplay.Add(gas);
			}
			gasesToDisplay.Sort((gasA, gasB) => Acu.AverageGasMix.GasRatio(gasB).CompareTo(Acu.AverageGasMix.GasRatio(gasA)));

			if (metricsContainer.Entries.Length != gasesToDisplay.Count)
			{
				metricsContainer.SetItems(gasesToDisplay.Count);
			}

			for (int i = 0; i < metricsContainer.Entries.Length; i++)
			{
				Gas gas = gasesToDisplay[i];
				float ratio = Acu.AverageGasMix.GasRatio(gas);
				float moles = Acu.AverageGasMix.Gases[gas];
				UpdateGasEntry(i, gas.Name, ratio, moles, Acu.GasLevelStatus[gas.Index]);
			}
		}

		private void UpdateLabels()
		{
			string pressureText = $"{Acu.AverageGasMix.Pressure, 0:N3} kPa";
			string temperatureText = $"{TemperatureUtils.FromKelvin(Acu.AverageGasMix.Temperature, TemeratureUnits.C), 0:N1} °C";

			pressureLabel.SetValueServer(
					$"Pressure:    {GUI_ACU.ColorStringByStatus(pressureText, Acu.PressureStatus)}");
			temperatureLabel.SetValueServer(
					$"Temperature: {GUI_ACU.ColorStringByStatus(temperatureText, Acu.TemperatureStatus)}");
			compositionLabel.SetValueServer(
					$"Composition: {GUI_ACU.ColorStringByStatus(Acu.CompositionStatus.ToString(), Acu.CompositionStatus)}");
		}

		private void UpdateGasEntry(int index, string name, float ratio, float moles, ACUStatus molStatus)
		{
			DynamicEntry dynamicEntry = metricsContainer.Entries[index];
			var entry = dynamicEntry.GetComponent<GUI_ACUGasEntry>();
			entry.SetValues(name, ratio, moles, molStatus);
		}
	}
}
