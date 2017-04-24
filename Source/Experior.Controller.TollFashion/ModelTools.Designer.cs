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
            this.dataGridViewEquipmentStatuses = new System.Windows.Forms.DataGridView();
            this.label1 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewEquipmentStatuses)).BeginInit();
            this.SuspendLayout();
            // 
            // dataGridViewEquipmentStatuses
            // 
            this.dataGridViewEquipmentStatuses.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewEquipmentStatuses.Location = new System.Drawing.Point(12, 29);
            this.dataGridViewEquipmentStatuses.Name = "dataGridViewEquipmentStatuses";
            this.dataGridViewEquipmentStatuses.Size = new System.Drawing.Size(263, 202);
            this.dataGridViewEquipmentStatuses.TabIndex = 2;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(12, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(123, 17);
            this.label1.TabIndex = 3;
            this.label1.Text = "Equipment Status:";
            // 
            // ModelTools
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(516, 337);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.dataGridViewEquipmentStatuses);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ModelTools";
            this.Text = "DEMATIC Tools";
            this.TopMost = true;
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewEquipmentStatuses)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }




        #endregion
        private System.Windows.Forms.DataGridView dataGridViewEquipmentStatuses;
        private System.Windows.Forms.Label label1;
    }
}