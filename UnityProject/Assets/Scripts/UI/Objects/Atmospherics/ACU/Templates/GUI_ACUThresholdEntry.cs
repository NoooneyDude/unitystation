using System.Globalization;
using Systems.Atmospherics;
using UnityEngine;
using Objects.Atmospherics;

namespace UI.Objects.Atmospherics.ACU
{
	/// <summary>
	/// An entry for the <see cref="GUI_ACUThresholdsPage"/>.
	/// Allows the peeper to view and configure the thresholds
	/// the <see cref="AirController"/> uses to determine local air quality status.
	/// </summary>
	public class GUI_ACUThresholdEntry : DynamicEntry
	{
		[SerializeField]
		private NetLabel label = default;

		private GUI_ACUThresholdsPage thresholdsPage;

		public int Index { get; private set; }
		public ThresholdType Type { get; private set; }
		public string Name { get; private set; }
		public float[] Values { get; private set; }
		public Gas Gas { get; private set; }

		public void SetValues(
				GUI_ACUThresholdsPage thresholdsPage, int index, ThresholdType type,
				string thresholdName, float[] values, Gas gas = default)
		{
			this.thresholdsPage = thresholdsPage;
			Index = index;
			Type = type;
			Name = thresholdName;
			Values = values;
			Gas = gas;

			label.SetValueServer(type == ThresholdType.Linebreak
					? string.Empty
					: $"{thresholdName, -14} | {values[0], -7} | {values[1], -7} | {values[2], -7} | {values[3], -7}");
		}

		#region Buttons

		public void BtnSetAlert(int thresholdIndex)
		{
			if (Type == ThresholdType.Linebreak) return;

			thresholdsPage.AcuUi.PlayTap();
			thresholdsPage.SetThreshold(this, thresholdIndex);
		}

		#endregion
	}
}
