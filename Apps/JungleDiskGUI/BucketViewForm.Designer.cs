namespace JungleDisk.JungleDiskGUI
{
    partial class BucketViewForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
			this.components = new System.ComponentModel.Container();
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(BucketViewForm));
			this.treeView = new System.Windows.Forms.TreeView();
			this.imageList = new System.Windows.Forms.ImageList(this.components);
			this.buttonDownload = new System.Windows.Forms.Button();
			this.buttonUpload = new System.Windows.Forms.Button();
			this.bucketSelectionBox = new System.Windows.Forms.ComboBox();
			this.label1 = new System.Windows.Forms.Label();
			this.saveFileDialog = new System.Windows.Forms.SaveFileDialog();
			this.openFileDialog = new System.Windows.Forms.OpenFileDialog();
			this.buttonDelete = new System.Windows.Forms.Button();
			this.buttonNewBucket = new System.Windows.Forms.Button();
			this.SuspendLayout();
			// 
			// treeView
			// 
			this.treeView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
						| System.Windows.Forms.AnchorStyles.Left)
						| System.Windows.Forms.AnchorStyles.Right)));
			this.treeView.ImageIndex = 0;
			this.treeView.ImageList = this.imageList;
			this.treeView.ItemHeight = 18;
			this.treeView.Location = new System.Drawing.Point(12, 40);
			this.treeView.Name = "treeView";
			this.treeView.SelectedImageIndex = 0;
			this.treeView.Size = new System.Drawing.Size(339, 332);
			this.treeView.TabIndex = 1;
			this.treeView.BeforeExpand += new System.Windows.Forms.TreeViewCancelEventHandler(this.treeView_BeforeExpand);
			this.treeView.BeforeCollapse += new System.Windows.Forms.TreeViewCancelEventHandler(this.treeView_BeforeCollapse);
			this.treeView.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.treeView_AfterSelect);
			// 
			// imageList
			// 
			this.imageList.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList.ImageStream")));
			this.imageList.TransparentColor = System.Drawing.Color.Transparent;
			this.imageList.Images.SetKeyName(0, "binary.png");
			this.imageList.Images.SetKeyName(1, "folder_yellow.png");
			this.imageList.Images.SetKeyName(2, "folder_yellow_open.png");
			this.imageList.Images.SetKeyName(3, "trashcan_empty.png");
			// 
			// buttonDownload
			// 
			this.buttonDownload.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.buttonDownload.Location = new System.Drawing.Point(276, 378);
			this.buttonDownload.Name = "buttonDownload";
			this.buttonDownload.Size = new System.Drawing.Size(75, 23);
			this.buttonDownload.TabIndex = 3;
			this.buttonDownload.Text = "&Download";
			this.buttonDownload.UseVisualStyleBackColor = true;
			this.buttonDownload.Click += new System.EventHandler(this.buttonDownload_Click);
			// 
			// buttonUpload
			// 
			this.buttonUpload.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.buttonUpload.Location = new System.Drawing.Point(195, 378);
			this.buttonUpload.Name = "buttonUpload";
			this.buttonUpload.Size = new System.Drawing.Size(75, 23);
			this.buttonUpload.TabIndex = 2;
			this.buttonUpload.Text = "&Upload";
			this.buttonUpload.UseVisualStyleBackColor = true;
			this.buttonUpload.Click += new System.EventHandler(this.buttonUpload_Click);
			// 
			// bucketSelectionBox
			// 
			this.bucketSelectionBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
						| System.Windows.Forms.AnchorStyles.Right)));
			this.bucketSelectionBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.bucketSelectionBox.FormattingEnabled = true;
			this.bucketSelectionBox.Location = new System.Drawing.Point(67, 13);
			this.bucketSelectionBox.Name = "bucketSelectionBox";
			this.bucketSelectionBox.Size = new System.Drawing.Size(170, 21);
			this.bucketSelectionBox.TabIndex = 0;
			this.bucketSelectionBox.SelectedIndexChanged += new System.EventHandler(this.bucketSelectionBox_SelectedIndexChanged);
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(12, 16);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(49, 13);
			this.label1.TabIndex = 4;
			this.label1.Text = "Buckets:";
			// 
			// openFileDialog
			// 
			this.openFileDialog.FileName = "openFileDialog1";
			// 
			// buttonDelete
			// 
			this.buttonDelete.Location = new System.Drawing.Point(114, 378);
			this.buttonDelete.Name = "buttonDelete";
			this.buttonDelete.Size = new System.Drawing.Size(75, 23);
			this.buttonDelete.TabIndex = 5;
			this.buttonDelete.Text = "D&elete";
			this.buttonDelete.UseVisualStyleBackColor = true;
			this.buttonDelete.Click += new System.EventHandler(this.buttonDelete_Click);
			// 
			// buttonNewBucket
			// 
			this.buttonNewBucket.Location = new System.Drawing.Point(276, 12);
			this.buttonNewBucket.Name = "buttonNewBucket";
			this.buttonNewBucket.Size = new System.Drawing.Size(75, 23);
			this.buttonNewBucket.TabIndex = 6;
			this.buttonNewBucket.Text = "New Bucket";
			this.buttonNewBucket.UseVisualStyleBackColor = true;
			this.buttonNewBucket.Click += new System.EventHandler(this.buttonNewBucket_Click);
			// 
			// BucketViewForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(363, 413);
			this.Controls.Add(this.buttonNewBucket);
			this.Controls.Add(this.buttonDelete);
			this.Controls.Add(this.label1);
			this.Controls.Add(this.bucketSelectionBox);
			this.Controls.Add(this.buttonUpload);
			this.Controls.Add(this.buttonDownload);
			this.Controls.Add(this.treeView);
			this.Name = "BucketViewForm";
			this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Show;
			this.Text = "Jungle Disk Dot Net";
			this.Load += new System.EventHandler(this.BucketViewForm_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TreeView treeView;
        private System.Windows.Forms.Button buttonDownload;
        private System.Windows.Forms.Button buttonUpload;
        private System.Windows.Forms.ComboBox bucketSelectionBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.SaveFileDialog saveFileDialog;
        private System.Windows.Forms.OpenFileDialog openFileDialog;
        private System.Windows.Forms.ImageList imageList;
        private System.Windows.Forms.Button buttonDelete;
		private System.Windows.Forms.Button buttonNewBucket;
    }
}

