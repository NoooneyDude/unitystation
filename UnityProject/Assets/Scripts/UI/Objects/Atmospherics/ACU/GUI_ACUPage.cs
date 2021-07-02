using Objects.Atmospherics;

namespace UI.Objects.Atmospherics.ACU
{
	/// <summary>
	/// Abstract class which all <see cref="GUI_ACU"/> pages should derive from.
	/// </summary>
	public abstract class GUI_ACUPage : NetPage
	{
		public AirController Acu { get; set; }
		public GUI_ACU AcuUi { get; set; }

		/// <summary>Whether this page should only be available if the <see cref="AirController"/> is unlocked.</summary>
		public virtual bool IsProtected => false;

		/// <summary>
		/// Runs just after the page is activated.
		/// </summary>
		public virtual void OnPageActivated() { }

		/// <summary>
		/// Runs just before the new page is activated.
		/// </summary>
		public virtual void OnPageDeactivated() { }

		/// <summary>
		/// Is run periodically, but also instantly after the page has been set active.
		/// </summary>
		public virtual void OnPeriodicUpdate() { }
	}
}
