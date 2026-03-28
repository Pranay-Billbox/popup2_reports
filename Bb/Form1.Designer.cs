namespace Bb
{
    partial class Form1
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
            this.boxName = new System.Windows.Forms.TextBox();
            this.boxMobile = new System.Windows.Forms.TextBox();
            this.txtName = new System.Windows.Forms.Label();
            this.txtMobile = new System.Windows.Forms.Label();
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // button1
            // 
            this.button1.Font = new System.Drawing.Font("Microsoft Sans Serif", 13F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.button1.Location = new System.Drawing.Point(214, 315);
            this.button1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(156, 63);
            this.button1.TabIndex = 3;
            this.button1.Text = "E-Bill";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // boxName
            // 
            this.boxName.Font = new System.Drawing.Font("Microsoft Sans Serif", 15.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.boxName.Location = new System.Drawing.Point(214, 138);
            this.boxName.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.boxName.Name = "boxName";
            this.boxName.Size = new System.Drawing.Size(312, 43);
            this.boxName.TabIndex = 1;
            this.boxName.TextChanged += new System.EventHandler(this.boxName_TextChanged_1);
            // 
            // boxMobile
            // 
            this.boxMobile.Font = new System.Drawing.Font("Microsoft Sans Serif", 15.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.boxMobile.Location = new System.Drawing.Point(214, 59);
            this.boxMobile.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.boxMobile.MaxLength = 10;
            this.boxMobile.Name = "boxMobile";
            this.boxMobile.Size = new System.Drawing.Size(312, 43);
            this.boxMobile.TabIndex = 0;
            // 
            // txtName
            // 
            this.txtName.AutoSize = true;
            this.txtName.Font = new System.Drawing.Font("Arial Narrow", 16F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtName.Location = new System.Drawing.Point(85, 138);
            this.txtName.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.txtName.Name = "txtName";
            this.txtName.Size = new System.Drawing.Size(105, 37);
            this.txtName.TabIndex = 9;
            this.txtName.Text = "Name :";
            // 
            // txtMobile
            // 
            this.txtMobile.AutoSize = true;
            this.txtMobile.Font = new System.Drawing.Font("Arial Narrow", 16F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtMobile.Location = new System.Drawing.Point(32, 59);
            this.txtMobile.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.txtMobile.Name = "txtMobile";
            this.txtMobile.Size = new System.Drawing.Size(158, 37);
            this.txtMobile.TabIndex = 8;
            this.txtMobile.Text = "Mobile No.:";
            // 
            // checkBox1
            // 
            this.checkBox1.AutoSize = true;
            this.checkBox1.Font = new System.Drawing.Font("Arial", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.checkBox1.Location = new System.Drawing.Point(92, 231);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(160, 36);
            this.checkBox1.TabIndex = 2;
            this.checkBox1.Text = "Paper-Bill";
            this.checkBox1.UseVisualStyleBackColor = true;
            this.checkBox1.CheckedChanged += new System.EventHandler(this.checkBox1_CheckedChanged);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(633, 422);
            this.Controls.Add(this.checkBox1);
            this.Controls.Add(this.txtName);
            this.Controls.Add(this.txtMobile);
            this.Controls.Add(this.boxMobile);
            this.Controls.Add(this.boxName);
            this.Controls.Add(this.button1);
            this.Cursor = System.Windows.Forms.Cursors.SizeAll;
            this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.MinimizeBox = false;
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "BillBox";
            this.TopMost = true;
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.TextBox boxName;
        private System.Windows.Forms.TextBox boxMobile;
        private System.Windows.Forms.Label txtName;
        private System.Windows.Forms.Label txtMobile;
        private System.Windows.Forms.CheckBox checkBox1;
    }
}

