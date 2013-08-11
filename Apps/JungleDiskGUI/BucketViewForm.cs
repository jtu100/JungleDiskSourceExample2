/**
 *  This program is free software; you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License version 2 as published by
 *  the Free Software Foundation
 *  
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *  
 *  You should have received a copy of the GNU General Public License
 *  along with this program; if not, write to the Free Software
 *  Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Security.Cryptography;

namespace JungleDisk.JungleDiskGUI
{
    public partial class BucketViewForm : Form
    {
        JungleDiskConnection conn = null;
        JungleDiskBucket bucket = null;
        List<JungleDiskBucket> buckets = null;

        const int imgFile = 0;
        const int imgFolder = 1;
        const int imgFolderOpen = 2;
        const int imgBucket = 3;

        public BucketViewForm(JungleDiskConnection connection)
        {
            this.conn = connection;
            this.conn.passwordRequiredDelegate = PasswordRequested;
            InitializeComponent();
        }

        private string PasswordRequested(string details)
        {
            PasswordRequiredForm form = new PasswordRequiredForm();
            form.details.Text = details;
            form.password.Focus(); // doesn't work..?
            if (form.ShowDialog() == DialogResult.OK)
                return form.password.Text;
            else
                return null;
        }
        private static int CompareBuckets(JungleDiskBucket a, JungleDiskBucket b)
        {
            return a.DisplayName.CompareTo(b.DisplayName);
        }
        private void BucketViewForm_Load(object sender, EventArgs e)
        {
			LoadBucketList();
          
        }

		private void LoadBucketList()
		{
			try
			{
				buckets = conn.GetBucketList(JungleDiskConnection.BucketFilter.AdvancedOnly);
			}
			catch (S3Exception ex)
			{
				MessageBox.Show(ex.ToString(), "Error Getting Bucket List");
				Close();
				return;
			}
			buckets.Sort(CompareBuckets);
			bucketSelectionBox.Items.Clear();
			foreach (JungleDiskBucket b in buckets)
				bucketSelectionBox.Items.Add(b.DisplayName);
			if (buckets.Count > 0)
				bucketSelectionBox.SelectedItem = 0;
			buttonDownload.Enabled = buttonUpload.Enabled = buttonDelete.Enabled = false;
		}


        private void Populate(TreeNode parent, JungleDiskBucket bucket, string path)
        {
            TreeNode node;
            DirectoryContents contents = null;
            try
            {
                contents = conn.GetDirectoryListing(bucket, path);
            }
            catch (Exception ex)
            {
                return;
            }

            foreach (DirectoryItem item in contents.SortedDirectories)
            {
                node = parent.Nodes.Add(item.Name);
                node.ImageIndex = imgFolder;
                node.Tag = item;
                node.Nodes.Add(".");
            }

            foreach (DirectoryItem item in contents.SortedFiles)
            {
                node = parent.Nodes.Add(item.Name);
                node.ImageIndex = imgFile;
                node.Tag = item;
            }
        }

        private string GetPathOfNode(TreeNode n)
        {
            string path = "";
            while (n.Parent != null && n.Tag != null)
            {
                path = "/" + ((DirectoryItem)n.Tag).Name + path;
                n = n.Parent;
            }
            return path;
        }

        private void treeView_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            TreeNode nodeSelected = e.Node;
            if (nodeSelected.Nodes[0].Text == ".")
            {
                nodeSelected.Nodes.Clear();
                string path = GetPathOfNode(nodeSelected);
                Populate(nodeSelected, bucket, path);

            }
            if (nodeSelected.Tag != null)
                nodeSelected.ImageIndex = imgFolderOpen;
            else
                nodeSelected.ImageIndex = imgBucket;
        }

        private void treeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            DirectoryItem item = (DirectoryItem)treeView.SelectedNode.Tag;
            bool isFileSelected = (item == null) ? false : !item.IsDirectory;
            buttonDownload.Enabled = buttonDelete.Enabled = isFileSelected;
            buttonUpload.Enabled = !isFileSelected;
            treeView.SelectedImageIndex = treeView.SelectedNode.ImageIndex;
        }

        private void Repopulate(TreeNode node)
        {
            node.Nodes.Clear();
            string path = GetPathOfNode(node);
            Populate(node, bucket, path);
        }

        private void buttonUpload_Click(object sender, EventArgs e)
        {
            openFileDialog.Title = "Choose a file to upload...";
            openFileDialog.FileName = "";
            openFileDialog.CheckFileExists = true;
            if (openFileDialog.ShowDialog() != DialogResult.OK)
                return;

            FileInfo fileInfo = new FileInfo(openFileDialog.FileName);

            DirectoryItem item = (DirectoryItem)treeView.SelectedNode.Tag;
            if (item != null && !item.IsDirectory)
                return;
            string dirPath = GetPathOfNode(treeView.SelectedNode);
            string path = dirPath + "/" + fileInfo.Name;

            FileStream file = File.Open(fileInfo.FullName, FileMode.Open, FileAccess.Read);
            conn.WriteFile(bucket, path, file);
            file.Close();

            // Update the tree view to show the newly-uploaded file.
            Repopulate(treeView.SelectedNode);
        }

        private void buttonDownload_Click(object sender, EventArgs e)
        {
            DirectoryItem item = (DirectoryItem)treeView.SelectedNode.Tag;
            if (item.IsDirectory)
                return;
            string path = GetPathOfNode(treeView.SelectedNode);

            saveFileDialog.FileName = item.Name;
            if (saveFileDialog.ShowDialog() != DialogResult.OK)
                return;

            Stream stream = conn.ReadFile(bucket, path);
            FileStream saveFile = File.Open(saveFileDialog.FileName, FileMode.Create, FileAccess.Write);

            // Is there a better way to do this? (pipe data from one stream into another)
            byte[] buffer = new byte[1024];
            int bytesRead = 0;
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
                saveFile.Write(buffer, 0, bytesRead);
            saveFile.Close();
            stream.Close();
        }

        private void bucketSelectionBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            bucket = buckets[bucketSelectionBox.SelectedIndex];
            if (bucket == null)
                return;
            treeView.Nodes.Clear();
            TreeNode root = treeView.Nodes.Add(bucket.DisplayName);
            root.ImageIndex = imgBucket;
            root.Tag = null;
            Populate(root, bucket, "/");

            buttonDownload.Enabled = false;
            buttonUpload.Enabled = false;
            buttonDelete.Enabled = false;
        }

        private void treeView_BeforeCollapse(object sender, TreeViewCancelEventArgs e)
        {
            if (e.Node.Tag != null)
                e.Node.ImageIndex = imgFolder;
            else
                e.Node.ImageIndex = imgBucket;
        }

        private void buttonDelete_Click(object sender, EventArgs e)
        {
            DirectoryItem item = (DirectoryItem)treeView.SelectedNode.Tag;
            if (item.IsDirectory)
            {
                MessageBox.Show("Directory deletion is not presently supported.");
                return;
            }
            if (MessageBox.Show("Are you sure you want to delete " + item.Name + "?", "Delete confirmation", MessageBoxButtons.OKCancel) != DialogResult.OK)
                return;
            conn.DeleteFile(bucket, item);
            Repopulate(treeView.SelectedNode.Parent);
            treeView.SelectedNode = treeView.SelectedNode.Parent;
        }

		public static JungleDiskBucket JungleDiskBucketFromDisplayName(string accessKey, string displayName)
		{
			string accessKeyHash = Crypto.HexString((MD5.Create()).ComputeHash(Encoding.Default.GetBytes(accessKey)));
			string s3Bucket = "jd2-" + accessKeyHash + "-us";
			return new JungleDiskBucket(BucketType.Advanced, s3Bucket, s3Bucket + "/" + displayName, displayName);
		}
		private void buttonNewBucket_Click(object sender, EventArgs e)
		{
			PasswordRequiredForm form = new PasswordRequiredForm();

			form.title1.Text = "New Bucket Name";
			form.Text = "New Bucket Name";
			form.details.Text = "Please enter a name for the new bucket";
			
			if (form.ShowDialog() != DialogResult.OK)
				return;

			conn.CreateBucket(JungleDiskBucketFromDisplayName(conn.AccessKey, form.password.Text), null, false);
			LoadBucketList();
		}

    }
}