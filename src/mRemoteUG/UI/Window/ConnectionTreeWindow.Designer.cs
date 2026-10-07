

using mRemoteUG.Connection;
using mRemoteUG.Tree;

namespace mRemoteUG.UI.Window
{
	public partial class ConnectionTreeWindow : BaseWindow
	{
        #region  Windows Form Designer generated code
		internal System.Windows.Forms.TextBox txtSearch;
		internal System.Windows.Forms.Panel pnlSearch;
		internal System.Windows.Forms.MenuStrip msMain;
		internal System.Windows.Forms.ToolStripMenuItem mMenView;
		internal System.Windows.Forms.ToolStripMenuItem mMenViewExpandAllFolders;
		internal System.Windows.Forms.ToolStripMenuItem mMenViewCollapseAllFolders;
		internal System.Windows.Forms.PictureBox PictureBox1;
		internal System.Windows.Forms.ToolStripMenuItem mMenSortAscending;
		internal System.Windows.Forms.ToolStripMenuItem mMenAddConnection;
		internal System.Windows.Forms.ToolStripMenuItem mMenAddFolder;
		private void InitializeComponent()
		{
            this.components = new System.ComponentModel.Container();
            mRemoteUG.Tree.TreeNodeCompositeClickHandler treeNodeCompositeClickHandler1 = new mRemoteUG.Tree.TreeNodeCompositeClickHandler();
            mRemoteUG.Tree.AlwaysConfirmYes alwaysConfirmYes1 = new mRemoteUG.Tree.AlwaysConfirmYes();
            mRemoteUG.Tree.TreeNodeCompositeClickHandler treeNodeCompositeClickHandler2 = new mRemoteUG.Tree.TreeNodeCompositeClickHandler();
            this.olvConnections = new mRemoteUG.UI.Controls.ConnectionTree();
            this.pnlSearch = new System.Windows.Forms.Panel();
            this.PictureBox1 = new System.Windows.Forms.PictureBox();
            this.txtSearch = new System.Windows.Forms.TextBox();
            this.msMain = new System.Windows.Forms.MenuStrip();
            this.mMenAddConnection = new System.Windows.Forms.ToolStripMenuItem();
            this.mMenAddFolder = new System.Windows.Forms.ToolStripMenuItem();
            this.mMenView = new System.Windows.Forms.ToolStripMenuItem();
            this.mMenViewExpandAllFolders = new System.Windows.Forms.ToolStripMenuItem();
            this.mMenViewCollapseAllFolders = new System.Windows.Forms.ToolStripMenuItem();
            this.mMenSortAscending = new System.Windows.Forms.ToolStripMenuItem();
            this.pnlSearch.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.PictureBox1)).BeginInit();
            this.msMain.SuspendLayout();
            this.SuspendLayout();
            //
            //Theming support
            //
            // 
            // olvConnections
            // 
            this.olvConnections.AllowDrop = true;
            this.olvConnections.Dock = System.Windows.Forms.DockStyle.Fill;
            this.olvConnections.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.olvConnections.ConnectionTreeModel = new ConnectionTreeModel();
            this.olvConnections.Cursor = System.Windows.Forms.Cursors.Default;
            treeNodeCompositeClickHandler1.ClickHandlers = new ITreeNodeClickHandler<ConnectionInfo>[0];
            this.olvConnections.DoubleClickHandler = treeNodeCompositeClickHandler1;
            this.olvConnections.HideSelection = false;
            this.olvConnections.LabelEdit = true;
            this.olvConnections.FullRowSelect = true;
            this.olvConnections.ShowLines = false;
            this.olvConnections.ShowRootLines = false;
            this.olvConnections.ShowPlusMinus = true;
            this.olvConnections.ShowNodeToolTips = true;
            this.olvConnections.Sorted = false;
            this.olvConnections.Name = "olvConnections";
            this.olvConnections.NodeDeletionConfirmer = alwaysConfirmYes1;
            this.olvConnections.PostSetupActions = new mRemoteUG.UI.Controls.IConnectionTreeDelegate[0];
            treeNodeCompositeClickHandler2.ClickHandlers = new mRemoteUG.Tree.ITreeNodeClickHandler<ConnectionInfo>[0];
            this.olvConnections.SingleClickHandler = treeNodeCompositeClickHandler2;
            this.olvConnections.TabIndex = 20;
            // 
            // pnlSearch
            // 
            // The search row is docked rather than anchored, so the gap between it and the tree is
            // not a distance worked out once against a client size this window no longer has by
            // the time it is embedded. Its height and the glyph column beside the box are measured
            // from the font in ApplySearchRowMetrics, which is why neither is set here.
            //
            // Docked children are laid out from the last index down to index 0, each edge-docked
            // one taking its band out of what is left - so the Fill child is laid out last, which
            // means it has to be added first. Same order as OptionsForm, which adds tabOptions
            // before pnlBottom.
            this.pnlSearch.Controls.Add(this.txtSearch);
            this.pnlSearch.Controls.Add(this.PictureBox1);
            this.pnlSearch.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlSearch.Name = "pnlSearch";
            this.pnlSearch.TabIndex = 9;
            // 
            // PictureBox1
            // 
            this.PictureBox1.Dock = System.Windows.Forms.DockStyle.Left;
            this.PictureBox1.Image = global::mRemoteUG.Resources.Search;
            this.PictureBox1.Name = "PictureBox1";
            // Zoom rather than AutoSize: an auto-sizing control fights the dock that gives it its
            // column. The artwork is 16x16 like every other glyph here and is stretched above 100%.
            this.PictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.PictureBox1.TabIndex = 1;
            this.PictureBox1.TabStop = false;
            // 
            // txtSearch
            // 
            this.txtSearch.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtSearch.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.txtSearch.ForeColor = System.Drawing.SystemColors.GrayText;
            this.txtSearch.Name = "txtSearch";
            this.txtSearch.TabIndex = 30;
            this.txtSearch.TabStop = false;
            this.txtSearch.Text = "Search";
            this.txtSearch.TextChanged += new System.EventHandler(this.txtSearch_TextChanged);
            this.txtSearch.GotFocus += new System.EventHandler(this.txtSearch_GotFocus);
            this.txtSearch.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtSearch_KeyDown);
            this.txtSearch.LostFocus += new System.EventHandler(this.txtSearch_LostFocus);
            // 
            // msMain
            // 
            this.msMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.mMenAddConnection,
            this.mMenAddFolder,
            this.mMenView,
            this.mMenSortAscending});
            this.msMain.Dock = System.Windows.Forms.DockStyle.Top;
            this.msMain.Name = "msMain";
            this.msMain.ShowItemToolTips = true;
            this.msMain.TabIndex = 10;
            this.msMain.Text = "MenuStrip1";
            // 
            // mMenAddConnection
            // 
            this.mMenAddConnection.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.mMenAddConnection.Image = global::mRemoteUG.Resources.Connection_Add;
            this.mMenAddConnection.Name = "mMenAddConnection";
            this.mMenAddConnection.Size = new System.Drawing.Size(28, 20);
            this.mMenAddConnection.Click += new System.EventHandler(this.cMenTreeAddConnection_Click);
            // 
            // mMenAddFolder
            // 
            this.mMenAddFolder.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.mMenAddFolder.Image = global::mRemoteUG.Resources.Folder_Add;
            this.mMenAddFolder.Name = "mMenAddFolder";
            this.mMenAddFolder.Size = new System.Drawing.Size(28, 20);
            this.mMenAddFolder.Click += new System.EventHandler(this.cMenTreeAddFolder_Click);
            // 
            // mMenView
            // 
            this.mMenView.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.mMenView.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.mMenViewExpandAllFolders,
            this.mMenViewCollapseAllFolders});
            this.mMenView.Image = global::mRemoteUG.Resources.View;
            this.mMenView.Name = "mMenView";
            this.mMenView.Size = new System.Drawing.Size(28, 20);
            this.mMenView.Text = "&View";
            // 
            // mMenViewExpandAllFolders
            // 
            this.mMenViewExpandAllFolders.Image = global::mRemoteUG.Resources.Expand;
            this.mMenViewExpandAllFolders.Name = "mMenViewExpandAllFolders";
            this.mMenViewExpandAllFolders.Size = new System.Drawing.Size(172, 22);
            this.mMenViewExpandAllFolders.Text = "Expand all folders";
            // 
            // mMenViewCollapseAllFolders
            // 
            this.mMenViewCollapseAllFolders.Image = global::mRemoteUG.Resources.Collapse;
            this.mMenViewCollapseAllFolders.Name = "mMenViewCollapseAllFolders";
            this.mMenViewCollapseAllFolders.Size = new System.Drawing.Size(172, 22);
            this.mMenViewCollapseAllFolders.Text = "Collapse all folders";
            // 
            // mMenSortAscending
            // 
            this.mMenSortAscending.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.mMenSortAscending.Image = global::mRemoteUG.Resources.Sort_AZ;
            this.mMenSortAscending.Name = "mMenSortAscending";
            this.mMenSortAscending.Size = new System.Drawing.Size(28, 20);
            // 
            // ConnectionTreeWindow
            // 
            // Segoe UI 8.25pt, set below, measures 6x13 at 96 DPI.
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(192, 453);
            this.Controls.Add(this.olvConnections);
            this.Controls.Add(this.pnlSearch);
            this.Controls.Add(this.msMain);
            this.HideOnClose = true;
            this.Icon = global::mRemoteUG.Resources.Root_Icon;
            this.Name = "ConnectionTreeWindow";
            this.TabText = "Connections";
            this.Text = "Connections";
            this.Load += new System.EventHandler(this.Tree_Load);
            this.pnlSearch.ResumeLayout(false);
            this.pnlSearch.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.PictureBox1)).EndInit();
            this.msMain.ResumeLayout(false);
            this.msMain.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

		}
        #endregion

        private System.ComponentModel.IContainer components;
        private Controls.ConnectionTree olvConnections;
    }
}
