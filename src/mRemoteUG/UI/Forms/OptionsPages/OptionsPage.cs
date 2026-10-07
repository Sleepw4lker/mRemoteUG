using System.ComponentModel;
using System.Windows.Forms;

namespace mRemoteUG.UI.Forms.OptionsPages
{
	public class OptionsPage : UserControl
	{
	    protected OptionsPage()
		{
        }
			
        #region Public Properties
	    // ReSharper disable once UnusedAutoPropertyAccessor.Global
	    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	    [Browsable(false)]public virtual string PageName {get; set;}

	    #endregion

        #region Public Methods
        public virtual void ApplyLanguage()
		{
				
		}
			
		public virtual void LoadSettings()
		{
				
		}
			
		public virtual void SaveSettings()
		{
				
		}
			
        #endregion


    }
}
