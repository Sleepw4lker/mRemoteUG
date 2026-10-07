using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Windows.Forms;
using mRemoteUG.App;
using mRemoteUG.Connection;
using mRemoteUG.Connection.Protocol;
using mRemoteUG.Connection.Protocol.RDP;
using mRemoteUG.Container;
using mRemoteUG.Messages;
using mRemoteUG.Security;
using mRemoteUG.Tools;
using mRemoteUG.Tree.Root;
using mRemoteUG.UI.Controls.FilteredPropertyGrid;
using System.ComponentModel;

namespace mRemoteUG.UI.Window
{
    public class ConfigWindow : BaseWindow
	{
        private bool _originalPropertyGridToolStripItemCountValid;
        private int _originalPropertyGridToolStripItemCount;
        private System.ComponentModel.Container _components;
        private ToolStripButton _btnShowProperties;
        private ToolStripButton _btnShowDefaultProperties;
        private ToolStripButton _btnShowInheritance;
        private ToolStripButton _btnShowDefaultInheritance;
        private ToolStripButton _btnIcon;
        private ToolStripButton _btnHostStatus;
        internal ContextMenuStrip CMenIcons;
        internal ContextMenuStrip PropertyGridContextMenu;
        private ToolStripMenuItem _propertyGridContextMenuShowHelpText;
        private ToolStripMenuItem _propertyGridContextMenuReset;
        private ToolStripSeparator _toolStripSeparator1;
        private FilteredPropertyGrid _pGrid;

        private AbstractConnectionRecord _selectedTreeNode;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AbstractConnectionRecord SelectedTreeNode
        {
            get { return _selectedTreeNode; }
            set
            {
                _selectedTreeNode = value;
                SetPropertyGridObject(_selectedTreeNode);
            }
        }

        private void InitializeComponent()
		{
            _components = new System.ComponentModel.Container();
            Load += Config_Load;
            SystemColorsChanged += Config_SystemColorsChanged;
            _pGrid = new FilteredPropertyGrid();
            _pGrid.PropertyValueChanged += pGrid_PropertyValueChanged;
            _pGrid.PropertySortChanged += pGrid_PropertySortChanged;
            PropertyGridContextMenu = new ContextMenuStrip(_components);
            PropertyGridContextMenu.Opening += propertyGridContextMenu_Opening;
            _propertyGridContextMenuReset = new ToolStripMenuItem();
            _propertyGridContextMenuReset.Click += propertyGridContextMenuReset_Click;
            _toolStripSeparator1 = new ToolStripSeparator();
            _propertyGridContextMenuShowHelpText = new ToolStripMenuItem();
            _propertyGridContextMenuShowHelpText.Click += propertyGridContextMenuShowHelpText_Click;
            _propertyGridContextMenuShowHelpText.CheckedChanged += propertyGridContextMenuShowHelpText_CheckedChanged;
            _btnShowInheritance = new ToolStripButton();
            _btnShowInheritance.Click += btnShowInheritance_Click;
            _btnShowDefaultInheritance = new ToolStripButton();
            _btnShowDefaultInheritance.Click += btnShowDefaultInheritance_Click;
            _btnShowProperties = new ToolStripButton();
            _btnShowProperties.Click += btnShowProperties_Click;
            _btnShowDefaultProperties = new ToolStripButton();
            _btnShowDefaultProperties.Click += btnShowDefaultProperties_Click;
            _btnIcon = new ToolStripButton();
            _btnIcon.MouseUp += btnIcon_Click;
            _btnHostStatus = new ToolStripButton();
            _btnHostStatus.Click += btnHostStatus_Click;
            _hostStatusDebounce.Tick += HostStatusDebounceElapsed;
            CMenIcons = new ContextMenuStrip(_components);
            PropertyGridContextMenu.SuspendLayout();
            SuspendLayout();
            //
            //pGrid
            //
            // Docked, not anchored to a size. This window hosts exactly one control - the six
            // toolbar buttons are injected into the property grid's own ToolStrip at run time -
            // so there is nothing here to lay out, only something to fill.
            //
            // The 226x530 this used to carry was also a hazard: it was assigned after the font,
            // so an explicit size silently masked whatever the auto-scale pass had just decided.
            // Filling cannot drift from anything.
            _pGrid.Dock = DockStyle.Fill;
            _pGrid.BrowsableProperties = null;
            _pGrid.ContextMenuStrip = PropertyGridContextMenu;
            _pGrid.HiddenAttributes = null;
            _pGrid.HiddenProperties = null;
            _pGrid.Name = "_pGrid";
            _pGrid.PropertySort = PropertySort.Categorized;
            _pGrid.TabIndex = 0;
            _pGrid.UseCompatibleTextRendering = true;
            //
            //propertyGridContextMenu
            //
            PropertyGridContextMenu.Items.AddRange(new ToolStripItem[] { _propertyGridContextMenuReset, _toolStripSeparator1, _propertyGridContextMenuShowHelpText });
            PropertyGridContextMenu.Name = "PropertyGridContextMenu";
            PropertyGridContextMenu.Size = new Size(157, 76);
            //
            //propertyGridContextMenuReset
            //
            _propertyGridContextMenuReset.Name = "_propertyGridContextMenuReset";
            _propertyGridContextMenuReset.Size = new Size(156, 22);
            _propertyGridContextMenuReset.Text = @"&Reset";
            //
            //ToolStripSeparator1
            //
            _toolStripSeparator1.Name = "_toolStripSeparator1";
            _toolStripSeparator1.Size = new Size(153, 6);
            //
            //propertyGridContextMenuShowHelpText
            //
            _propertyGridContextMenuShowHelpText.Name = "_propertyGridContextMenuShowHelpText";
            _propertyGridContextMenuShowHelpText.Size = new Size(156, 22);
            _propertyGridContextMenuShowHelpText.Text = @"&Show Help Text";
            //
            //btnShowInheritance
            //
            _btnShowInheritance.DisplayStyle = ToolStripItemDisplayStyle.Image;
            _btnShowInheritance.Image = Resources.Inheritance;
            _btnShowInheritance.ImageTransparentColor = Color.Magenta;
            _btnShowInheritance.Name = "_btnShowInheritance";
            _btnShowInheritance.Size = new Size(23, 22);
            _btnShowInheritance.Text = @"Inheritance";
            //
            //btnShowDefaultInheritance
            //
            _btnShowDefaultInheritance.DisplayStyle = ToolStripItemDisplayStyle.Image;
            _btnShowDefaultInheritance.Image = Resources.Inheritance_Default;
            _btnShowDefaultInheritance.ImageTransparentColor = Color.Magenta;
            _btnShowDefaultInheritance.Name = "_btnShowDefaultInheritance";
            _btnShowDefaultInheritance.Size = new Size(23, 22);
            _btnShowDefaultInheritance.Text = @"Default Inheritance";
            //
            //btnShowProperties
            //
            _btnShowProperties.Checked = true;
            _btnShowProperties.CheckState = CheckState.Checked;
            _btnShowProperties.DisplayStyle = ToolStripItemDisplayStyle.Image;
            _btnShowProperties.Image = Resources.Properties;
            _btnShowProperties.ImageTransparentColor = Color.Magenta;
            _btnShowProperties.Name = "_btnShowProperties";
            _btnShowProperties.Size = new Size(23, 22);
            _btnShowProperties.Text = @"Properties";
            //
            //btnShowDefaultProperties
            //
            _btnShowDefaultProperties.DisplayStyle = ToolStripItemDisplayStyle.Image;
            _btnShowDefaultProperties.Image = Resources.Properties_Default;
            _btnShowDefaultProperties.ImageTransparentColor = Color.Magenta;
            _btnShowDefaultProperties.Name = "_btnShowDefaultProperties";
            _btnShowDefaultProperties.Size = new Size(23, 22);
            _btnShowDefaultProperties.Text = @"Default Properties";
            //
            //btnIcon
            //
            _btnIcon.Alignment = ToolStripItemAlignment.Right;
            _btnIcon.DisplayStyle = ToolStripItemDisplayStyle.Image;
            _btnIcon.ImageTransparentColor = Color.Magenta;
            _btnIcon.Name = "_btnIcon";
            _btnIcon.Size = new Size(23, 22);
            _btnIcon.Text = @"Icon";
            //
            //btnHostStatus
            //
            _btnHostStatus.Alignment = ToolStripItemAlignment.Right;
            _btnHostStatus.DisplayStyle = ToolStripItemDisplayStyle.Image;
            _btnHostStatus.Image = Resources.HostStatus_Check;
            _btnHostStatus.ImageTransparentColor = Color.Magenta;
            _btnHostStatus.Name = "_btnHostStatus";
            _btnHostStatus.Size = new Size(23, 22);
            _btnHostStatus.Tag = "checking";
            _btnHostStatus.Text = @"Status";
            //
            //cMenIcons
            //
            CMenIcons.Name = "CMenIcons";
            CMenIcons.Size = new Size(61, 4);
            //
            //Config
            //
            // Segoe UI 8.25pt, set below, measures 6x13 at 96 DPI.
            AutoScaleDimensions = new SizeF(6F, 13F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(226, 530);
            Controls.Add(_pGrid);
            HideOnClose = true;
            Icon = Resources.Config_Icon;
            Name = "ConfigWindow";
            TabText = @"Config";
            Text = @"Config";
            PropertyGridContextMenu.ResumeLayout(false);
            ResumeLayout(false);
					
		}
		
        #region Public Properties
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool PropertiesVisible
		{
			get
			{
			    return _btnShowProperties.Checked;
			}
			set
			{
                _btnShowProperties.Checked = value;
			    if (!value) return;
			    _btnShowInheritance.Checked = false;
			    _btnShowDefaultInheritance.Checked = false;
			    _btnShowDefaultProperties.Checked = false;
			}
		}
		
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool InheritanceVisible
		{
			get
			{
			    return _btnShowInheritance.Checked;
			}
			set
			{
                _btnShowInheritance.Checked = value;
			    if (!value) return;
			    _btnShowProperties.Checked = false;
			    _btnShowDefaultInheritance.Checked = false;
			    _btnShowDefaultProperties.Checked = false;
			}
		}
		
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool DefaultPropertiesVisible
		{
			get
			{
			    return _btnShowDefaultProperties.Checked;
			}
			set
			{
                _btnShowDefaultProperties.Checked = value;
			    if (!value) return;
			    _btnShowProperties.Checked = false;
			    _btnShowDefaultInheritance.Checked = false;
			    _btnShowInheritance.Checked = false;
			}
		}
		
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool DefaultInheritanceVisible
		{
			get { return _btnShowDefaultInheritance.Checked; }
			set
			{
                _btnShowDefaultInheritance.Checked = value;
			    if (!value) return;
			    _btnShowProperties.Checked = false;
			    _btnShowDefaultProperties.Checked = false;
			    _btnShowInheritance.Checked = false;
			}
		}

        /// <summary>
        /// A list of properties being shown for the current object.
        /// </summary>
	    public IEnumerable<string> VisibleObjectProperties => _pGrid.VisibleProperties;
        #endregion

        #region Constructors

        public ConfigWindow()
        {
            InitializeComponent();
        }
        #endregion

        #region Public Methods
		
		protected override bool ProcessCmdKey(ref System.Windows.Forms.Message msg, Keys keyData)
		{
		    // Main form handle command key events
            // Adapted from http://kiwigis.blogspot.com/2009/05/adding-tab-key-support-to-propertygrid.html
		    if ((keyData & Keys.KeyCode) != Keys.Tab) return base.ProcessCmdKey(ref msg, keyData);
		    var selectedItem = _pGrid.SelectedGridItem;
		    var gridRoot = selectedItem;
		    while (gridRoot.GridItemType != GridItemType.Root)
		    {
		        gridRoot = gridRoot.Parent;
		    }
						
		    var gridItems = new List<GridItem>();
		    FindChildGridItems(gridRoot, ref gridItems);
						
		    if (!ContainsGridItemProperty(gridItems))
		        return true;
						
		    var newItem = selectedItem;
						
		    // ReSharper disable once SwitchStatementMissingSomeCases
		    switch (keyData)
		    {
		        case (Keys.Tab | Keys.Shift):
		            newItem = FindPreviousGridItemProperty(gridItems, selectedItem);
		            break;
		        case Keys.Tab:
		            newItem = FindNextGridItemProperty(gridItems, selectedItem);
		            break;
		    }
						
		    _pGrid.SelectedGridItem = newItem;
						
		    return true; // Handled
		}
		
		private void FindChildGridItems(GridItem item, ref List<GridItem> gridItems)
		{
			gridItems.Add(item);

		    if (item.Expandable && !item.Expanded) return;
		    foreach (GridItem child in item.GridItems)
		    {
		        FindChildGridItems(child, ref gridItems);
		    }
		}
		
		private bool ContainsGridItemProperty(IEnumerable<GridItem> gridItems)
		{
		    return gridItems.Any(item => item.GridItemType == GridItemType.Property);
		}

        private GridItem FindPreviousGridItemProperty(IList<GridItem> gridItems, GridItem startItem)
		{
			if (gridItems.Count == 0 || startItem == null)
				return null;
			
			var startIndex = gridItems.IndexOf(startItem);
			if (startItem.GridItemType == GridItemType.Property)
			{
				startIndex--;
				if (startIndex < 0)
				{
					startIndex = gridItems.Count - 1;
				}
			}
			
			var previousIndex = 0;
			var previousIndexValid = false;
			for (var index = startIndex; index >= 0; index--)
			{
			    if (gridItems[index].GridItemType != GridItemType.Property) continue;
			    previousIndex = index;
			    previousIndexValid = true;
			    break;
			}
			
			if (previousIndexValid)
				return gridItems[previousIndex];
			
			for (var index = gridItems.Count - 1; index >= startIndex + 1; index--)
			{
			    if (gridItems[index].GridItemType != GridItemType.Property) continue;
			    previousIndex = index;
			    previousIndexValid = true;
			    break;
			}
			
			return !previousIndexValid ? null : gridItems[previousIndex];
		}
		
		private GridItem FindNextGridItemProperty(IList<GridItem> gridItems, GridItem startItem)
		{
			if (gridItems.Count == 0 || startItem == null)
				return null;
					
			var startIndex = gridItems.IndexOf(startItem);
			if (startItem.GridItemType == GridItemType.Property)
			{
				startIndex++;
				if (startIndex >= gridItems.Count)
				{
					startIndex = 0;
				}
			}
			
			var nextIndex = 0;
			var nextIndexValid = false;
			for (var index = startIndex; index <= gridItems.Count - 1; index++)
			{
			    if (gridItems[index].GridItemType != GridItemType.Property) continue;
			    nextIndex = index;
			    nextIndexValid = true;
			    break;
			}
			
			if (nextIndexValid)
				return gridItems[nextIndex];
					
			for (var index = 0; index <= startIndex - 1; index++)
			{
			    if (gridItems[index].GridItemType != GridItemType.Property) continue;
			    nextIndex = index;
			    nextIndexValid = true;
			    break;
			}
			
			return !nextIndexValid ? null : gridItems[nextIndex];
		}

	    private void SetPropertyGridObject(object propertyGridObject)
		{
			try
			{
                _btnShowProperties.Enabled = false;
                _btnShowInheritance.Enabled = false;
                _btnShowDefaultProperties.Enabled = false;
                _btnShowDefaultInheritance.Enabled = false;
                _btnIcon.Enabled = false;
                _btnHostStatus.Enabled = false;

                _btnIcon.Image = null;

			    var gridObjectAsConnectionInfo = propertyGridObject as ConnectionInfo;
			    if (gridObjectAsConnectionInfo != null) //CONNECTION INFO
				{
                    var gridObjectAsContainerInfo = propertyGridObject as ContainerInfo;
				    if (gridObjectAsContainerInfo != null) //CONTAINER
                    {
                        var gridObjectAsRootNodeInfo = propertyGridObject as RootNodeInfo;
                        if (gridObjectAsRootNodeInfo != null) // ROOT
					    {
					        // ReSharper disable once SwitchStatementMissingSomeCases
                            switch (gridObjectAsRootNodeInfo.Type)
					        {
					            case RootNodeType.Connection:
					                PropertiesVisible = true;
					                DefaultPropertiesVisible = false;
					                _btnShowProperties.Enabled = true;
					                _btnShowInheritance.Enabled = false;
					                _btnShowDefaultProperties.Enabled = true;
					                _btnShowDefaultInheritance.Enabled = true;
					                _btnIcon.Enabled = false;
					                _btnHostStatus.Enabled = false;
					                break;
					            case RootNodeType.PuttySessions:
					                PropertiesVisible = true;
					                DefaultPropertiesVisible = false;
					                _btnShowProperties.Enabled = true;
					                _btnShowInheritance.Enabled = false;
					                _btnShowDefaultProperties.Enabled = false;
					                _btnShowDefaultInheritance.Enabled = false;
					                _btnIcon.Enabled = false;
					                _btnHostStatus.Enabled = false;
					                break;
					        }
					        
					        _pGrid.SelectedObject = propertyGridObject;
					    }
					    else
                        {
					        _pGrid.SelectedObject = propertyGridObject;

					        _btnShowProperties.Enabled = true;
					        _btnShowInheritance.Enabled = 
                                gridObjectAsContainerInfo.Parent != null &&
                                !(gridObjectAsContainerInfo.Parent is RootNodeInfo);
					        _btnShowDefaultProperties.Enabled = false;
					        _btnShowDefaultInheritance.Enabled = false;
					        _btnIcon.Enabled = true;
					        _btnHostStatus.Enabled = false;

					        PropertiesVisible = true;
					    }
                    }
                    else //NO CONTAINER
				    {
                        if (PropertiesVisible) //Properties selected
                        {
                            _pGrid.SelectedObject = propertyGridObject;

                            _btnShowProperties.Enabled = true;
                            _btnShowInheritance.Enabled =
                                !(gridObjectAsConnectionInfo is PuttySessionInfo) &&
                                gridObjectAsConnectionInfo.Parent != null &&
                                !(gridObjectAsConnectionInfo.Parent is RootNodeInfo);
                            _btnShowDefaultProperties.Enabled = false;
                            _btnShowDefaultInheritance.Enabled = false;
                            _btnIcon.Enabled = true;
                            _btnHostStatus.Enabled = true;
                        }
                        else if (DefaultPropertiesVisible) //Defaults selected
                        {
                            _pGrid.SelectedObject = propertyGridObject;

                            if (propertyGridObject is DefaultConnectionInfo) //Is the default connection
                            {
                                _btnShowProperties.Enabled = true;
                                _btnShowInheritance.Enabled = false;
                                _btnShowDefaultProperties.Enabled = true;
                                _btnShowDefaultInheritance.Enabled = true;
                                _btnIcon.Enabled = true;
                                _btnHostStatus.Enabled = false;
                            }
                            else //is not the default connection
                            {
                                _btnShowProperties.Enabled = true;
                                _btnShowInheritance.Enabled = true;
                                _btnShowDefaultProperties.Enabled = false;
                                _btnShowDefaultInheritance.Enabled = false;
                                _btnIcon.Enabled = true;
                                _btnHostStatus.Enabled = true;

                                PropertiesVisible = true;
                            }
                        }
                        else if (InheritanceVisible) //Inheritance selected
                        {
                            _pGrid.SelectedObject = gridObjectAsConnectionInfo.Inheritance;

                            _btnShowProperties.Enabled = true;
                            _btnShowInheritance.Enabled = true;
                            _btnShowDefaultProperties.Enabled = false;
                            _btnShowDefaultInheritance.Enabled = false;
                            _btnIcon.Enabled = true;
                            _btnHostStatus.Enabled = true;
                        }
                        else if (DefaultInheritanceVisible) //Default Inhertiance selected
                        {
                            _pGrid.SelectedObject = propertyGridObject;

                            _btnShowProperties.Enabled = true;
                            _btnShowInheritance.Enabled = true;
                            _btnShowDefaultProperties.Enabled = false;
                            _btnShowDefaultInheritance.Enabled = false;
                            _btnIcon.Enabled = true;
                            _btnHostStatus.Enabled = true;

                            PropertiesVisible = true;
                        }
                    }

                    var conIcon = ConnectionIcon.FromString(Convert.ToString(gridObjectAsConnectionInfo.Icon),
                                                            GlyphSizeOn(_btnIcon));
					if (conIcon != null)
					{
                        _btnIcon.Image = conIcon;
					}
				}
				else if (propertyGridObject is ConnectionInfoInheritance) //INHERITANCE
				{
                    _pGrid.SelectedObject = propertyGridObject;
							
					if (InheritanceVisible)
					{
                        InheritanceVisible = true;
                        _btnShowProperties.Enabled = true;
                        _btnShowInheritance.Enabled = true;
                        _btnShowDefaultProperties.Enabled = false;
                        _btnShowDefaultInheritance.Enabled = false;
                        _btnIcon.Enabled = true;
                        _btnHostStatus.Enabled = !((ConnectionInfo)((ConnectionInfoInheritance)propertyGridObject).Parent).IsContainer;
                        InheritanceVisible = true;
                        var conIcon = ConnectionIcon.FromString(
                            Convert.ToString(((ConnectionInfo)((ConnectionInfoInheritance)propertyGridObject).Parent).Icon),
                            GlyphSizeOn(_btnIcon));
						if (conIcon != null)
						{
                            _btnIcon.Image = conIcon;
						}
					}
					else if (DefaultInheritanceVisible)
					{
                        _btnShowProperties.Enabled = true;
                        _btnShowInheritance.Enabled = false;
                        _btnShowDefaultProperties.Enabled = true;
                        _btnShowDefaultInheritance.Enabled = true;
                        _btnIcon.Enabled = false;
                        _btnHostStatus.Enabled = false;

                        DefaultInheritanceVisible = true;
					}
							
				}

                ShowHideGridItems();
                SetHostStatus(propertyGridObject);
			}
			catch (Exception ex)
			{
				Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, Language.strConfigPropertyGridObjectFailed + Environment.NewLine + ex.Message, true);
			}
		}
        #endregion
		
        #region Private Methods
		private void ApplyLanguage()
		{
			_btnShowInheritance.Text = Language.strButtonInheritance;
			_btnShowDefaultInheritance.Text = Language.strButtonDefaultInheritance;
			_btnShowProperties.Text = Language.strButtonProperties;
			_btnShowDefaultProperties.Text = Language.strButtonDefaultProperties;
			_btnIcon.Text = Language.strButtonIcon;
			_btnHostStatus.Text = Language.strStatus;
			Text = Language.strMenuConfig;
			TabText = Language.strMenuConfig;
			_propertyGridContextMenuShowHelpText.Text = Language.strMenuShowHelpText;
		}
		
        private void AddToolStripItems()
		{
			try
			{
				var customToolStrip = new ToolStrip();
				customToolStrip.Items.Add(_btnShowProperties);
				customToolStrip.Items.Add(_btnShowInheritance);
				customToolStrip.Items.Add(_btnShowDefaultProperties);
				customToolStrip.Items.Add(_btnShowDefaultInheritance);
				customToolStrip.Items.Add(_btnHostStatus);
				customToolStrip.Items.Add(_btnIcon);
				customToolStrip.Show();
						
				var propertyGridToolStrip = new ToolStrip();
						
				ToolStrip toolStrip = null;
				foreach (Control control in _pGrid.Controls)
				{
                    toolStrip = control as ToolStrip;
				    if (toolStrip == null) continue;
				    propertyGridToolStrip = toolStrip;
				    break;
				}
						
				if (toolStrip == null)
				{
					Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, Language.strCouldNotFindToolStripInFilteredPropertyGrid, true);
					return;
				}
						
				if (!_originalPropertyGridToolStripItemCountValid)
				{
					_originalPropertyGridToolStripItemCount = propertyGridToolStrip.Items.Count;
					_originalPropertyGridToolStripItemCountValid = true;
				}
				Debug.Assert(_originalPropertyGridToolStripItemCount == 5);
						
				// Hide the "Property Pages" button
				propertyGridToolStrip.Items[_originalPropertyGridToolStripItemCount - 1].Visible = false;

				StyleFrameworkSortButtons(propertyGridToolStrip, _originalPropertyGridToolStripItemCount);
						
				var expectedToolStripItemCount = _originalPropertyGridToolStripItemCount + customToolStrip.Items.Count;
			    if (propertyGridToolStrip.Items.Count == expectedToolStripItemCount) return;
			    propertyGridToolStrip.AllowMerge = true;
			    ToolStripManager.Merge(customToolStrip, propertyGridToolStrip);
			}
			catch (Exception ex)
			{
				Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, Language.strConfigUiLoadFailed + Environment.NewLine + ex.Message, true);
			}
		}

		/// <summary>
		/// Gives the property grid its own toolbar buttons the artwork the rest of the
		/// application uses.
		/// </summary>
		/// <remarks>
		/// These are the only images on this strip that are not this application's. A
		/// <see cref="PropertyGrid"/> builds its own toolbar from bitmaps embedded in
		/// System.Windows.Forms.dll, which still date from the .NET Framework 1.x era, and they
		/// sit directly beside the Fluent ones merged in below. They are also the only images
		/// here that <c>DpiScaling.ApplyImageScaling</c> cannot re-pick, because they never came
		/// from the glyph cache - so at 150% they were 16 pixel bitmaps stretched to 24.
		/// Assigning them from the cache fixes the look and puts them in the re-pick at once.
		/// <para>
		/// Measured against .NET 10, the strip is Categorized, Alphabetical, NoSort, a separator
		/// and Property Pages. NoSort is worth knowing about: the framework ships it with no
		/// text, no tooltip, and <em>the same bitmap as Alphabetical</em> - so stock WinForms
		/// draws two adjacent buttons that look identical and one of them cannot be identified
		/// by hovering it. It gets a mark of its own and a tooltip here.
		/// </para>
		/// <para>
		/// By index, like the Property Pages line above, and guarded by the same count. The text
		/// cannot be matched on instead: it comes from the framework's resources and follows the
		/// OS UI culture rather than this application's. If a future WinForms changes the shape
		/// of this strip, the guard leaves the framework artwork in place rather than putting
		/// the wrong mark on the wrong button.
		/// </para>
		/// </remarks>
		private static void StyleFrameworkSortButtons(ToolStrip strip, int originalItemCount)
		{
			const int categorized = 0, alphabetical = 1, noSort = 2;
			const int shapeThisWasWrittenAgainst = 5;

			if (originalItemCount != shapeThisWasWrittenAgainst)
				return;

			// The size the strip is already drawing at, so these are right immediately rather
			// than only after the next DPI change.
			var size = strip.ImageScalingSize.Width;

			strip.Items[categorized].Image = Glyphs.Get("Categorized", size);
			strip.Items[alphabetical].Image = Glyphs.Get("Sort_AZ", size);
			strip.Items[noSort].Image = Glyphs.Get("Unsorted", size);

			if (string.IsNullOrEmpty(strip.Items[noSort].ToolTipText))
				strip.Items[noSort].ToolTipText = @"No Sort";
		}
		
		/// <summary>
		/// The frame size an image put on this toolbar should be picked at.
		/// </summary>
		/// <remarks>
		/// Asked of the strip, for the reason <see cref="StyleFrameworkSortButtons"/> gives:
		/// <c>ImageScalingSize</c> is the rectangle the strip actually draws an item's image
		/// into, and it is what both the framework's own scaling and
		/// <c>DpiScaling.ApplyImageScaling</c> move.
		/// <para>
		/// The four mode buttons are given their image once, in <c>InitializeComponent</c>, so
		/// that pass re-picks them and they are right at any scale. The icon and the host status
		/// mark are not: they are reassigned on every selection change, on every ping reply and
		/// on every pick from the icon menu, all of which happen long after the pass has run.
		/// Each of those assignments asked for the 16 pixel frame, so above 100% the strip drew
		/// a 16 into a larger rectangle - two soft buttons beside four sharp ones.
		/// </para>
		/// <para>
		/// Before the merge there is no strip to ask - a tree node can be selected before this
		/// window is shown - and the window's own scaling stands in.
		/// </para>
		/// </remarks>
		private int GlyphSizeOn(ToolStripItem button) =>
			button.Owner?.ImageScalingSize.Width ?? LogicalToDeviceUnits(Glyphs.LogicalSize);

		/// <summary>
		/// One of the host status marks, at the size the toolbar is drawing at.
		/// </summary>
		/// <remarks>
		/// Re-picked through the cache rather than naming the glyph a second time as a string:
		/// <c>Resources.HostStatus_On</c> and its siblings always hand back the 16 pixel frame,
		/// because that is the only size a property can name, but going back through
		/// <see cref="Glyphs.TryRepick"/> keeps the compile-time check that the artwork exists.
		/// A glyph dropped from the manifest then fails the build here rather than throwing in
		/// front of a user.
		/// </remarks>
		private Bitmap HostStatusMark(Bitmap mark) =>
			Glyphs.TryRepick(mark, GlyphSizeOn(_btnHostStatus), out var repicked) ? repicked : mark;

		private void Config_Load(object? sender, EventArgs e)
		{
			ApplyLanguage();
			AddToolStripItems();
			_pGrid.HelpVisible = Settings.Default.ShowConfigHelpText;
		}
		
		private void Config_SystemColorsChanged(object? sender, EventArgs e)
		{
			AddToolStripItems();
		}
		
		private void pGrid_PropertyValueChanged(object? s, PropertyValueChangedEventArgs e)
		{
			try
            {
                UpdateConnectionInfoNode(e);
                UpdateRootInfoNode(e);
                UpdateInheritanceNode();
                ShowHideGridItems();
            }
            catch (Exception ex)
			{
				Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, Language.strConfigPropertyGridValueFailed + Environment.NewLine + ex.Message, true);
			}
		}

        private void UpdateConnectionInfoNode(PropertyValueChangedEventArgs e)
        {
            Debug.WriteLine("update config");
            var selectedGridObject = _pGrid.SelectedObject as ConnectionInfo;
            if (selectedGridObject == null) return;
            if (e.ChangedItem.Label == Language.strPropertyNameProtocol)
            {
                selectedGridObject.SetDefaultPort();
            }
            else if (e.ChangedItem.Label == Language.strPropertyNameName)
            {
                if (Settings.Default.SetHostnameLikeDisplayName)
                {
                    var connectionInfo = selectedGridObject;
                    if (!string.IsNullOrEmpty(connectionInfo.Name))
                        connectionInfo.Hostname = connectionInfo.Name;
                }
            }
            else if (e.ChangedItem.Label == Language.strPropertyNameIcon)
            {
                var conIcon = ConnectionIcon.FromString(Convert.ToString(selectedGridObject.Icon),
                                                        GlyphSizeOn(_btnIcon));
                if (conIcon != null)
                    _btnIcon.Image = conIcon;
            }
            else if (e.ChangedItem.Label == Language.strPropertyNameAddress)
            {
                SetHostStatus(selectedGridObject);
            }

            if (selectedGridObject is DefaultConnectionInfo)
                DefaultConnectionInfo.Instance.SaveTo(Settings.Default, a=>"ConDefault"+a);
        }

        private void UpdateRootInfoNode(PropertyValueChangedEventArgs e)
        {
            var rootInfo = _pGrid.SelectedObject as RootNodeInfo;
            if (rootInfo == null)
                return;

            if (e.ChangedItem.PropertyDescriptor?.Name != "Password")
                return;

            if (rootInfo.Password)
            {
                var passwordName = Path.GetFileName(Runtime.ConnectionsService.GetStartupConnectionFileName());

                var password = MiscTools.PasswordDialog(passwordName);

                // operation cancelled, dont set a password
                if (password == null || password.Length == 0)
                {
                    rootInfo.Password = false;
                    return;
                }

                rootInfo.PasswordString = password.ConvertToUnsecureString();
            }
            else
            {
                rootInfo.PasswordString = "";
            }
        }

        private void UpdateInheritanceNode()
        {
            if (!(_pGrid.SelectedObject is DefaultConnectionInheritance)) return;
            DefaultConnectionInheritance.Instance.SaveTo(Settings.Default, a=>"InhDefault"+a);
        }

        private void pGrid_PropertySortChanged(object? sender, EventArgs e)
		{
			if (_pGrid.PropertySort == PropertySort.CategorizedAlphabetical)
				_pGrid.PropertySort = PropertySort.Categorized;
		}
		
		private void ShowHideGridItems()
		{
			try
			{
                var strHide = new List<string>();
			    var o = _pGrid.SelectedObject as RootNodeInfo;
			    if (o != null)
                {
                    var rootInfo = o;
                    if (rootInfo.Type == RootNodeType.PuttySessions)
                    {
                        strHide.Add("Password");
                    }
                    strHide.Add("CacheBitmaps");
                    strHide.Add("Colors");
                    strHide.Add("DisplayThemes");
                    strHide.Add("DisplayWallpaper");
                    strHide.Add("EnableFontSmoothing");
                    strHide.Add("EnableDesktopComposition");
                    strHide.Add("Domain");
                    strHide.Add("RDGatewayDomain");
                    strHide.Add("RDGatewayHostname");
                    strHide.Add("RDGatewayPassword");
                    strHide.Add("RDGatewayUsageMethod");
                    strHide.Add("RDGatewayUseConnectionCredentials");
                    strHide.Add("RDGatewayUsername");
                    strHide.Add("RDPAuthenticationLevel");
                    strHide.Add("RDPMinutesToIdleTimeout");
                    strHide.Add("RDPAlertIdleTimeout");
                    strHide.Add("LoadBalanceInfo");
                    strHide.Add("RedirectDiskDrives");
                    strHide.Add("RedirectKeys");
                    strHide.Add("RedirectPorts");
                    strHide.Add("RedirectPrinters");
                    strHide.Add("RedirectSmartCards");
                    strHide.Add("RedirectClipboard");
                    strHide.Add("RedirectSound");
                    strHide.Add("Resolution");
                    strHide.Add("AutomaticResize");
                    strHide.Add("UseConsoleSession");
                    strHide.Add("UseCredSsp");
                    strHide.Add("Icon");
                    strHide.Add("Panel");
                    strHide.Add("Hostname");
                    strHide.Add("Username");
                    strHide.Add("Protocol");
                    strHide.Add("Port");
                    strHide.Add("PuttySession");
                    strHide.Add("MacAddress");
                    strHide.Add("UserField");
                    strHide.Add("Description");
                    strHide.Add("SoundQuality");
                }
                else if (_pGrid.SelectedObject is ConnectionInfo)
				{
                    var conI = (ConnectionInfo)_pGrid.SelectedObject;
                    // Each protocol shows only the properties that apply to it.
                    // RDP keeps its conditional sub-property rules; the PuTTY-driven protocols
                    // hide the RDP-specific settings wholesale.
                    switch (conI.Protocol)
                    {
                        case ProtocolType.RDP:
                            strHide.Add("PuttySession");
                            if (conI.RDPMinutesToIdleTimeout <= 0)
                            {
                                strHide.Add("RDPAlertIdleTimeout");
                            }
                            if (conI.RDGatewayUsageMethod == RdpProtocol.RDGatewayUsageMethod.Never)
                            {
                                strHide.Add("RDGatewayDomain");
                                strHide.Add("RDGatewayHostname");
                                strHide.Add("RDGatewayPassword");
                                strHide.Add("RDGatewayUseConnectionCredentials");
                                strHide.Add("RDGatewayUsername");
                            }
                            else if (conI.RDGatewayUseConnectionCredentials == RdpProtocol.RDGatewayUseConnectionCredentials.Yes)
                            {
                                strHide.Add("RDGatewayDomain");
                                strHide.Add("RDGatewayPassword");
                                strHide.Add("RDGatewayUsername");
                            }
                            if (!(conI.Resolution == RdpProtocol.RDPResolutions.FitToWindow || conI.Resolution == RdpProtocol.RDPResolutions.Fullscreen))
                            {
                                strHide.Add("AutomaticResize");
                            }
                            if (conI.RedirectSound != RdpProtocol.RDPSounds.BringToThisComputer)
                            {
                                strHide.Add("SoundQuality");
                            }
                            break;
                        case ProtocolType.SSH1:
                        case ProtocolType.SSH2:
                            strHide.Add("CacheBitmaps");
                            strHide.Add("Colors");
                            strHide.Add("DisplayThemes");
                            strHide.Add("DisplayWallpaper");
                            strHide.Add("EnableFontSmoothing");
                            strHide.Add("EnableDesktopComposition");
                            strHide.Add("Domain");
                            strHide.Add("RDGatewayDomain");
                            strHide.Add("RDGatewayHostname");
                            strHide.Add("RDGatewayPassword");
                            strHide.Add("RDGatewayUsageMethod");
                            strHide.Add("RDGatewayUseConnectionCredentials");
                            strHide.Add("RDGatewayUsername");
                            strHide.Add("RDPAuthenticationLevel");
                            strHide.Add("RDPMinutesToIdleTimeout");
                            strHide.Add("RDPAlertIdleTimeout");
                            strHide.Add("LoadBalanceInfo");
                            strHide.Add("RedirectDiskDrives");
                            strHide.Add("RedirectKeys");
                            strHide.Add("RedirectPorts");
                            strHide.Add("RedirectPrinters");
                            strHide.Add("RedirectSmartCards");
                            strHide.Add("RedirectClipboard");
                            strHide.Add("RedirectSound");
                            strHide.Add("Resolution");
                            strHide.Add("AutomaticResize");
                            strHide.Add("UseConsoleSession");
                            strHide.Add("UseCredSsp");
                            strHide.Add("SoundQuality");
                            break;
                        case ProtocolType.Telnet:
                        case ProtocolType.Rlogin:
                        case ProtocolType.RAW:
                            strHide.Add("CacheBitmaps");
                            strHide.Add("Colors");
                            strHide.Add("DisplayThemes");
                            strHide.Add("DisplayWallpaper");
                            strHide.Add("EnableFontSmoothing");
                            strHide.Add("EnableDesktopComposition");
                            strHide.Add("Domain");
                            strHide.Add("RDGatewayDomain");
                            strHide.Add("RDGatewayHostname");
                            strHide.Add("RDGatewayPassword");
                            strHide.Add("RDGatewayUsageMethod");
                            strHide.Add("RDGatewayUseConnectionCredentials");
                            strHide.Add("RDGatewayUsername");
                            strHide.Add("RDPAuthenticationLevel");
                            strHide.Add("RDPMinutesToIdleTimeout");
                            strHide.Add("RDPAlertIdleTimeout");
                            strHide.Add("LoadBalanceInfo");
                            strHide.Add("RedirectDiskDrives");
                            strHide.Add("RedirectKeys");
                            strHide.Add("RedirectPorts");
                            strHide.Add("RedirectPrinters");
                            strHide.Add("RedirectSmartCards");
                            strHide.Add("RedirectClipboard");
                            strHide.Add("RedirectSound");
                            strHide.Add("Resolution");
                            strHide.Add("AutomaticResize");
                            strHide.Add("UseConsoleSession");
                            strHide.Add("UseCredSsp");
                            strHide.Add("SoundQuality");
                            strHide.Add("Username");
                            strHide.Add("Password");
                            break;
                    }

					if (!(conI is DefaultConnectionInfo))
					{
						if (conI.Inheritance.CacheBitmaps)
							strHide.Add("CacheBitmaps");
						if (conI.Inheritance.Colors)
							strHide.Add("Colors");
						if (conI.Inheritance.Description)
							strHide.Add("Description");
						if (conI.Inheritance.DisplayThemes)
							strHide.Add("DisplayThemes");
						if (conI.Inheritance.DisplayWallpaper)
							strHide.Add("DisplayWallpaper");
						if (conI.Inheritance.EnableFontSmoothing)
							strHide.Add("EnableFontSmoothing");
						if (conI.Inheritance.EnableDesktopComposition)
							strHide.Add("EnableDesktopComposition");
						if (conI.Inheritance.Domain)
							strHide.Add("Domain");
						if (conI.Inheritance.Icon)
							strHide.Add("Icon");
						if (conI.Inheritance.Password)
							strHide.Add("Password");
						if (conI.Inheritance.Port)
							strHide.Add("Port");
						if (conI.Inheritance.PuttySession)
							strHide.Add("PuttySession");
						if (conI.Inheritance.Protocol)
							strHide.Add("Protocol");
						if (conI.Inheritance.RedirectDiskDrives)
                            strHide.Add("RedirectDiskDrives");
                        if (conI.Inheritance.RedirectKeys)
                            strHide.Add("RedirectKeys");
                        if (conI.Inheritance.RedirectPorts)
                            strHide.Add("RedirectPorts");
                        if (conI.Inheritance.RedirectPrinters)
                            strHide.Add("RedirectPrinters");
                        if (conI.Inheritance.RedirectSmartCards)
                            strHide.Add("RedirectSmartCards");
                        if (conI.Inheritance.RedirectClipboard)
                            strHide.Add("RedirectClipboard");
                        if (conI.Inheritance.RedirectSound)
                            strHide.Add("RedirectSound");
                        if (conI.Inheritance.Resolution)
                            strHide.Add("Resolution");
                        if (conI.Inheritance.AutomaticResize)
                            strHide.Add("AutomaticResize");
                        if (conI.Inheritance.UseConsoleSession)
                            strHide.Add("UseConsoleSession");
                        if (conI.Inheritance.UseCredSsp)
                            strHide.Add("UseCredSsp");
                        if (conI.Inheritance.RDPAuthenticationLevel)
                            strHide.Add("RDPAuthenticationLevel");
                        if (conI.Inheritance.RDPMinutesToIdleTimeout)
                            strHide.Add("RDPMinutesToIdleTimeout");
                        if (conI.Inheritance.RDPAlertIdleTimeout)
                            strHide.Add("RDPAlertIdleTimeout");
                        if (conI.Inheritance.LoadBalanceInfo)
                            strHide.Add("LoadBalanceInfo");
                        if (conI.Inheritance.Username)
                            strHide.Add("Username");
                        if (conI.Inheritance.Panel)
                            strHide.Add("Panel");
                        if (conI.IsContainer)
                            strHide.Add("Hostname");
                        if (conI.Inheritance.MacAddress)
                            strHide.Add("MacAddress");
                        if (conI.Inheritance.UserField)
                            strHide.Add("UserField");
                        if (conI.Inheritance.RDGatewayUsageMethod)
                            strHide.Add("RDGatewayUsageMethod");
                        if (conI.Inheritance.RDGatewayHostname)
                            strHide.Add("RDGatewayHostname");
                        if (conI.Inheritance.RDGatewayUsername)
                            strHide.Add("RDGatewayUsername");
                        if (conI.Inheritance.RDGatewayPassword)
                            strHide.Add("RDGatewayPassword");
                        if (conI.Inheritance.RDGatewayDomain)
                            strHide.Add("RDGatewayDomain");
                        if (conI.Inheritance.RDGatewayUseConnectionCredentials)
                            strHide.Add("RDGatewayUseConnectionCredentials");
                        if (conI.Inheritance.RDGatewayHostname)
                            strHide.Add("RDGatewayHostname");
                        if(conI.Inheritance.SoundQuality)
                            strHide.Add("SoundQuality");
                    }
					else
					{
						strHide.Add("Hostname");
						strHide.Add("Name");
					}
				}

                _pGrid.HiddenProperties = strHide.ToArray();
                _pGrid.Refresh();
			}
			catch (Exception ex)
			{
				Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, Language.strConfigPropertyGridHideItemsFailed + Environment.NewLine + ex.Message, true);
			}
		}
		
		private void btnShowProperties_Click(object? sender, EventArgs e)
		{
		    var o = _pGrid.SelectedObject as ConnectionInfoInheritance;
		    if (o != null)
			{
				if (_pGrid.SelectedObject is DefaultConnectionInheritance)
				{
                    PropertiesVisible = true;
                    InheritanceVisible = false;
                    DefaultPropertiesVisible = false;
                    DefaultInheritanceVisible = false;
                    SetPropertyGridObject((RootNodeInfo)_selectedTreeNode);
				}
				else
				{
                    PropertiesVisible = true;
                    InheritanceVisible = false;
                    DefaultPropertiesVisible = false;
                    DefaultInheritanceVisible = false;
                    SetPropertyGridObject(o.Parent);
				}
			}
			else if (_pGrid.SelectedObject is ConnectionInfo)
			{
			    if (!((ConnectionInfo) _pGrid.SelectedObject).IsDefault) return;
			    PropertiesVisible = true;
			    InheritanceVisible = false;
			    DefaultPropertiesVisible = false;
			    DefaultInheritanceVisible = false;
			    SetPropertyGridObject((RootNodeInfo)_selectedTreeNode);
			}
		}
		
		private void btnShowDefaultProperties_Click(object? sender, EventArgs e)
		{
		    if (!(_pGrid.SelectedObject is RootNodeInfo) && !(_pGrid.SelectedObject is ConnectionInfoInheritance)) return;
		    PropertiesVisible = false;
		    InheritanceVisible = false;
		    DefaultPropertiesVisible = true;
		    DefaultInheritanceVisible = false;
		    SetPropertyGridObject(DefaultConnectionInfo.Instance);
		}
		
		private void btnShowInheritance_Click(object? sender, EventArgs e)
		{
		    if (!(_pGrid.SelectedObject is ConnectionInfo)) return;
		    PropertiesVisible = false;
		    InheritanceVisible = true;
		    DefaultPropertiesVisible = false;
		    DefaultInheritanceVisible = false;
		    SetPropertyGridObject(((ConnectionInfo)_pGrid.SelectedObject).Inheritance);
		}
		
		private void btnShowDefaultInheritance_Click(object? sender, EventArgs e)
		{
		    if (!(_pGrid.SelectedObject is RootNodeInfo) && !(_pGrid.SelectedObject is ConnectionInfo)) return;
		    PropertiesVisible = false;
		    InheritanceVisible = false;
		    DefaultPropertiesVisible = false;
		    DefaultInheritanceVisible = true;
		    SetPropertyGridObject(DefaultConnectionInheritance.Instance);
		}
		
		private void btnHostStatus_Click(object? sender, EventArgs e)
		{
			SetHostStatus(_pGrid.SelectedObject);
		}
		
		private void btnIcon_Click(object? sender, MouseEventArgs e)
		{
			try
			{
			    if (!(_pGrid.SelectedObject is ConnectionInfo) || _pGrid.SelectedObject is PuttySessionInfo) return;
			    CMenIcons.Items.Clear();
							
			    // Built on demand, long after the DPI pass that sizes the rest of the
			    // application has run, so it asks for the size itself. Anything built during
			    // InitializeComponent is re-picked by DpiScaling.ApplyImageScaling instead.
			    var iconSize = LogicalToDeviceUnits(16);

			    foreach (var iStr in ConnectionIcon.Icons)
			    {
			        // Every name here comes from the assembly manifest, so this cannot miss.
			        // Guarded anyway: an unguarded null would throw out of the loop and leave
			        // the user with no menu at all rather than one missing entry.
			        var icon = ConnectionIcon.FromString(iStr, iconSize);
			        if (icon == null) continue;

			        var tI = new ToolStripMenuItem
			        {
			            Text = iStr,
			            Image = icon
			        };
			        tI.Click += IconMenu_Click;

			        CMenIcons.Items.Add(tI);
			    }
			    // A 96 DPI inset from the grid's right edge, computed at run time and so
			    // not reached by the auto-scale pass.
			    var mPos = new Point(new Size(PointToScreen(new Point(e.Location.X + _pGrid.Width - LogicalToDeviceUnits(100), e.Location.Y))));
			    CMenIcons.Show(mPos);
			}
			catch (Exception ex)
			{
				Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, Language.strConfigPropertyGridButtonIconClickFailed + Environment.NewLine + ex.Message, true);
			}
		}
		
		private void IconMenu_Click(object? sender, EventArgs e)
		{
			try
			{
				var connectionInfo = (ConnectionInfo)_pGrid.SelectedObject;
				if (connectionInfo == null) return;
						
				var selectedMenuItem = (ToolStripMenuItem)sender;

			    var iconName = selectedMenuItem?.Text;
				if (string.IsNullOrEmpty(iconName)) return;
						
				var connectionIcon = ConnectionIcon.FromString(iconName, GlyphSizeOn(_btnIcon));
				if (connectionIcon == null) return;
						
				_btnIcon.Image = connectionIcon;
						
				connectionInfo.Icon = iconName;
				_pGrid.Refresh();
						
				Runtime.ConnectionsService.SaveConnectionsAsync();
			}
			catch (Exception ex)
			{
				Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, Language.strConfigPropertyGridMenuClickFailed + Environment.NewLine + ex.Message, true);
			}
		}
        #endregion
		
        #region Host Status (Ping)
        /// <summary>
        /// Debounces the host status check and holds the result of the last one requested.
        /// </summary>
        /// <remarks>
        /// This used to start a fresh STA <see cref="Thread"/> per selection, each running a
        /// synchronous Ping.Send whose default timeout is five seconds. Holding an arrow key
        /// through a folder of fifty hosts made fifty threads and fifty pings; the field
        /// holding the thread was overwritten each time, so the earlier ones were orphaned but
        /// still running, and each finished into a *blocking* Invoke on the grid. Whether the
        /// image you ended up with described the host you had selected was a race.
        /// <para>
        /// A WinForms timer resumes its continuation on the UI thread, so the reply can be
        /// applied directly - there is no marshalling here at all any more.
        /// </para>
        /// </remarks>
        private readonly System.Windows.Forms.Timer _hostStatusDebounce =
            new System.Windows.Forms.Timer { Interval = 250 };
        private CancellationTokenSource _hostStatusCancellation;
        private string _hostStatusTarget;

        private void SetHostStatus(object connectionInfo)
        {
            try
            {
                // Whatever was pending describes a node that is no longer selected.
                _hostStatusDebounce.Stop();
                _hostStatusCancellation?.Cancel();

                // To check status, ConnectionInfo must be an mRemoteUG.Connection.Info that is not a container
                if (!(connectionInfo is ConnectionInfo info) || info.IsContainer)
                    return;

                _btnHostStatus.Image = HostStatusMark(Resources.HostStatus_Check);
                _hostStatusTarget = info.Hostname;
                _hostStatusDebounce.Start();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, Language.strConfigPropertyGridSetHostStatusFailed + Environment.NewLine + ex.Message, true);
            }
        }

        private async void HostStatusDebounceElapsed(object? sender, EventArgs e)
        {
            _hostStatusDebounce.Stop();

            if (string.IsNullOrEmpty(_hostStatusTarget))
            {
                _btnHostStatus.Image = HostStatusMark(Resources.HostStatus_Off);
                return;
            }

            var cancellation = new CancellationTokenSource();
            _hostStatusCancellation = cancellation;

            try
            {
                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync(_hostStatusTarget, TimeSpan.FromSeconds(5),
                                                         null, null, cancellation.Token);
                    if (!cancellation.IsCancellationRequested)
                        _btnHostStatus.Image = HostStatusMark(reply.Status == IPStatus.Success
                            ? Resources.HostStatus_On
                            : Resources.HostStatus_Off);
                }
            }
            catch (OperationCanceledException)
            {
                // Superseded by a newer selection. The newer check owns the image.
            }
            catch (Exception)
            {
                // An unresolvable name or an unreachable network is a normal answer here, not
                // something to report: it means the host is not up as far as we can tell.
                if (!cancellation.IsCancellationRequested)
                    _btnHostStatus.Image = HostStatusMark(Resources.HostStatus_Off);
            }
            finally
            {
                if (ReferenceEquals(_hostStatusCancellation, cancellation))
                    _hostStatusCancellation = null;
                cancellation.Dispose();
            }
        }
        #endregion

        #region Event Handlers
        private void propertyGridContextMenu_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
		{
			try
			{
				_propertyGridContextMenuShowHelpText.Checked = Settings.Default.ShowConfigHelpText;
				var gridItem = _pGrid.SelectedGridItem;
				_propertyGridContextMenuReset.Enabled = Convert.ToBoolean(_pGrid.SelectedObject != null && gridItem?.PropertyDescriptor != null && gridItem.PropertyDescriptor.CanResetValue(_pGrid.SelectedObject));
			}
			catch (Exception ex)
			{
				Runtime.MessageCollector.AddExceptionMessage("UI.Window.Config.propertyGridContextMenu_Opening() failed.", ex);
			}
		}
		
		private void propertyGridContextMenuReset_Click(object? sender, EventArgs e)
		{
			try
			{
				var gridItem = _pGrid.SelectedGridItem;
				if (_pGrid.SelectedObject != null && gridItem?.PropertyDescriptor != null && gridItem.PropertyDescriptor.CanResetValue(_pGrid.SelectedObject))
				{
					_pGrid.ResetSelectedProperty();
				}
			}
			catch (Exception ex)
			{
				Runtime.MessageCollector.AddExceptionMessage("UI.Window.Config.propertyGridContextMenuReset_Click() failed.", ex);
			}
		}
		
		private void propertyGridContextMenuShowHelpText_Click(object? sender, EventArgs e)
		{
			_propertyGridContextMenuShowHelpText.Checked = !_propertyGridContextMenuShowHelpText.Checked;
		}
		
		private void propertyGridContextMenuShowHelpText_CheckedChanged(object? sender, EventArgs e)
		{
            Settings.Default.ShowConfigHelpText = _propertyGridContextMenuShowHelpText.Checked;
			_pGrid.HelpVisible = _propertyGridContextMenuShowHelpText.Checked;
        }
        #endregion
    }
}
