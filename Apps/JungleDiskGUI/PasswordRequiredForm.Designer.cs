namespace JungleDisk.JungleDiskGUI
{
    partial class PasswordRequiredForm
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
            this.button1 = new System.Windows.Forms.Button();
            this.password = new System.Windows.Forms.TextBox();
            this.title1 = new System.Windows.Forms.Label();
            this.details = new System.Windows.Forms.Label();
            this.button2 = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // button1
            // 
            this.button1.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.button1.Location = new System.Drawing.Point(205, 55);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 0;
            this.button1.Text = "&OK";
            this.button1.UseVisualStyleBackColor = true;
            // 
            // password
            // 
            this.password.Location = new System.Drawing.Point(15, 57);
            this.password.Name = "password";
            this.password.PasswordChar = '*';
            this.password.Size = new System.Drawing.Size(184, 20);
            this.password.TabIndex = 1;
            // 
            // title1
            // 
            this.title1.AutoSize = true;
            this.title1.Location = new System.Drawing.Point(12, 9);
            this.title1.Name = "title1";
            this.title1.Size = new System.Drawing.Size(193, 13);
            this.title1.TabIndex = 2;
            this.title1.Text = "JungleDiskGUI requires a password for:";
            // 
            // details
            // 
            this.details.AutoSize = true;
            this.details.Location = new System.Drawing.Point(33, 32);
            this.details.Name = "details";
            this.details.Size = new System.Drawing.Size(37, 13);
            this.details.TabIndex = 3;
            this.details.Text = "details";
            // 
            // button2
            // 
            this.button2.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.button2.Location = new System.Drawing.Point(287, 54);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(75, 23);
            this.button2.TabIndex = 4;
            this.button2.Text = "Cancel";
            this.button2.UseVisualStyleBackColor = true;
            // 
            // PasswordRequiredForm
            // 
            this.AcceptButton = this.button1;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.button2;
            this.ClientSize = new System.Drawing.Size(379, 99);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.details);
            this.Controls.Add(this.title1);
            this.Controls.Add(this.password);
            this.Controls.Add(this.button1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PasswordRequiredForm";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Password Required";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

		private System.Windows.Forms.Button button1;
        public System.Windows.Forms.Label details;
        public System.Windows.Forms.TextBox password;
		public System.Windows.Forms.Label title1;
        private System.Windows.Forms.Button button2;
    }
}