namespace Experior.Controller
{
    partial class ModelTools
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
            base.Hide();
            //base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ModelTools));
            this.comboBoxCC51CARTON = new System.Windows.Forms.ComboBox();
            this.labelCC51CARTON = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // comboBoxCC51CARTON
            // 
            this.comboBoxCC51CARTON.FormattingEnabled = true;
            this.comboBoxCC51CARTON.Location = new System.Drawing.Point(111, 29);
            this.comboBoxCC51CARTON.Name = "comboBoxCC51CARTON";
            this.comboBoxCC51CARTON.Size = new System.Drawing.Size(90, 21);
            this.comboBoxCC51CARTON.TabIndex = 0;
            this.comboBoxCC51CARTON.TextChanged += new System.EventHandler(this.comboBoxCC51CARTON_TextChanged);
            // 
            // labelCC51CARTON
            // 
            this.labelCC51CARTON.AutoSize = true;
            this.labelCC51CARTON.Location = new System.Drawing.Point(27, 32);
            this.labelCC51CARTON.Name = "labelCC51CARTON";
            this.labelCC51CARTON.Size = new System.Drawing.Size(78, 13);
            this.labelCC51CARTON.TabIndex = 1;
            this.labelCC51CARTON.Text = "CC51CARTON";
            // 
            // ModelTools
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(516, 337);
            this.Controls.Add(this.labelCC51CARTON);
            this.Controls.Add(this.comboBoxCC51CARTON);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ModelTools";
            this.Text = "DEMATIC Tools";
            this.TopMost = true;
            this.ResumeLayout(false);
            this.PerformLayout();

        }




        #endregion

        private System.Windows.Forms.ComboBox comboBoxCC51CARTON;
        private System.Windows.Forms.Label labelCC51CARTON;
    }
}