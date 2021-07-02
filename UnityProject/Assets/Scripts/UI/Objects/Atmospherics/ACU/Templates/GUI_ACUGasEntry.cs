using UnityEngine;
using Objects.Atmospherics;

namespace UI.Objects.Atmospherics.ACU
{
	/// <summary>
	/// An entry for the <see cref="GUI_ACUOverviewPage"/>, displaying the metrics for the associated gas.
	/// </summary>
	public class GUI_ACUGasEntry : DynamicEntry
	{
		[SerializeField]
		private NetLabel label = default;

		public void SetValues(string metricName, float ratio, float moles, ACUStatus molStatus)
		{
			var percentString = $"{ratio, 10:P}";
			var molString = GUI_ACU.ColorStringByStatus($"{moles, 8:N}", molStatus);

			label.SetValueServer($"| {metricName, -18} | {percentString, -13} | {molString, -34} |");
			label.SetValueServer($"| {metricName, -18} | {percentString, -13} | {molString, -34} |");
		}
	}
}
