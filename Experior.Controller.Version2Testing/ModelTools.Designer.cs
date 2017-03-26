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
            this.cbArriving = new System.Windows.Forms.ComboBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label2 = new System.Windows.Forms.Label();
            this.tbArriving = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.label3 = new System.Windows.Forms.Label();
            this.tbDesArriving = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.cbDesArriving = new System.Windows.Forms.ComboBox();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.label5 = new System.Windows.Forms.Label();
            this.tbManArriving = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.cbManArriving = new System.Windows.Forms.ComboBox();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.label9 = new System.Windows.Forms.Label();
            this.ETB2Count = new System.Windows.Forms.TextBox();
            this.ETB1Count = new System.Windows.Forms.TextBox();
            this.label8 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.SuspendLayout();
            // 
            // cbArriving
            // 
            this.cbArriving.FormattingEnabled = true;
            this.cbArriving.Items.AddRange(new object[] {
            "Instant Delete",
            "Always Stop",
            "Wait x Seconds"});
            this.cbArriving.Location = new System.Drawing.Point(6, 19);
            this.cbArriving.Name = "cbArriving";
            this.cbArriving.Size = new System.Drawing.Size(112, 21);
            this.cbArriving.TabIndex = 0;
            this.cbArriving.Text = "Always Stop";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.tbArriving);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.cbArriving);
            this.groupBox1.Location = new System.Drawing.Point(12, 95);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(233, 77);
            this.groupBox1.TabIndex = 1;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "QA Packing Options";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(44, 49);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(83, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "Waiting Time (s)";
            // 
            // tbArriving
            // 
            this.tbArriving.Location = new System.Drawing.Point(6, 46);
            this.tbArriving.Name = "tbArriving";
            this.tbArriving.Size = new System.Drawing.Size(32, 20);
            this.tbArriving.TabIndex = 2;
            this.tbArriving.Text = "60";
            this.tbArriving.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(124, 22);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(93, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Arriving UL Option";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.label3);
            this.groupBox2.Controls.Add(this.tbDesArriving);
            this.groupBox2.Controls.Add(this.label4);
            this.groupBox2.Controls.Add(this.cbDesArriving);
            this.groupBox2.Location = new System.Drawing.Point(12, 178);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(233, 77);
            this.groupBox2.TabIndex = 4;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Despatch Options";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(44, 49);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(83, 13);
            this.label3.TabIndex = 3;
            this.label3.Text = "Waiting Time (s)";
            // 
            // tbDesArriving
            // 
            this.tbDesArriving.Location = new System.Drawing.Point(6, 46);
            this.tbDesArriving.Name = "tbDesArriving";
            this.tbDesArriving.Size = new System.Drawing.Size(32, 20);
            this.tbDesArriving.TabIndex = 2;
            this.tbDesArriving.Text = "60";
            this.tbDesArriving.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(124, 22);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(93, 13);
            this.label4.TabIndex = 1;
            this.label4.Text = "Arriving UL Option";
            // 
            // cbDesArriving
            // 
            this.cbDesArriving.FormattingEnabled = true;
            this.cbDesArriving.Items.AddRange(new object[] {
            "Instant Delete",
            "Always Stop",
            "Wait x Seconds"});
            this.cbDesArriving.Location = new System.Drawing.Point(6, 19);
            this.cbDesArriving.Name = "cbDesArriving";
            this.cbDesArriving.Size = new System.Drawing.Size(112, 21);
            this.cbDesArriving.TabIndex = 0;
            this.cbDesArriving.Text = "Instant Delete";
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.label5);
            this.groupBox3.Controls.Add(this.tbManArriving);
            this.groupBox3.Controls.Add(this.label6);
            this.groupBox3.Controls.Add(this.cbManArriving);
            this.groupBox3.Location = new System.Drawing.Point(12, 12);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(233, 77);
            this.groupBox3.TabIndex = 5;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Manual Outfeed Options";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(44, 49);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(83, 13);
            this.label5.TabIndex = 3;
            this.label5.Text = "Waiting Time (s)";
            // 
            // tbManArriving
            // 
            this.tbManArriving.Location = new System.Drawing.Point(6, 46);
            this.tbManArriving.Name = "tbManArriving";
            this.tbManArriving.Size = new System.Drawing.Size(32, 20);
            this.tbManArriving.TabIndex = 2;
            this.tbManArriving.Text = "60";
            this.tbManArriving.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(124, 22);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(93, 13);
            this.label6.TabIndex = 1;
            this.label6.Text = "Arriving UL Option";
            // 
            // cbManArriving
            // 
            this.cbManArriving.FormattingEnabled = true;
            this.cbManArriving.Items.AddRange(new object[] {
            "Instant Delete",
            "Always Stop",
            "Wait x Seconds"});
            this.cbManArriving.Location = new System.Drawing.Point(6, 19);
            this.cbManArriving.Name = "cbManArriving";
            this.cbManArriving.Size = new System.Drawing.Size(112, 21);
            this.cbManArriving.TabIndex = 0;
            this.cbManArriving.Text = "Always Stop";
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.label9);
            this.groupBox4.Controls.Add(this.ETB2Count);
            this.groupBox4.Controls.Add(this.ETB1Count);
            this.groupBox4.Controls.Add(this.label8);
            this.groupBox4.Controls.Add(this.label7);
            this.groupBox4.Location = new System.Drawing.Point(12, 261);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(233, 93);
            this.groupBox4.TabIndex = 6;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Empty Tote Buffer";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(17, 70);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(124, 13);
            this.label9.TabIndex = 4;
            this.label9.Text = "NOTE: Max Count = 200";
            // 
            // ETB2Count
            // 
            this.ETB2Count.Location = new System.Drawing.Point(109, 41);
            this.ETB2Count.Name = "ETB2Count";
            this.ETB2Count.Size = new System.Drawing.Size(65, 20);
            this.ETB2Count.TabIndex = 3;
            this.ETB2Count.Text = "150";
            this.ETB2Count.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // ETB1Count
            // 
            this.ETB1Count.Location = new System.Drawing.Point(17, 41);
            this.ETB1Count.Name = "ETB1Count";
            this.ETB1Count.Size = new System.Drawing.Size(65, 20);
            this.ETB1Count.TabIndex = 2;
            this.ETB1Count.Text = "150";
            this.ETB1Count.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(109, 22);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(65, 13);
            this.label8.TabIndex = 1;
            this.label8.Text = "ETB2 Count";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(17, 22);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(65, 13);
            this.label7.TabIndex = 0;
            this.label7.Text = "ETB1 Count";
            // 
            // ModelTools
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(257, 364);
            this.Controls.Add(this.groupBox4);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ModelTools";
            this.Text = "DEMATIC Tools";
            this.TopMost = true;
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        public System.Windows.Forms.ComboBox cbArriving;
        public System.Windows.Forms.TextBox tbArriving;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label label3;
        public System.Windows.Forms.TextBox tbDesArriving;
        private System.Windows.Forms.Label label4;
        public System.Windows.Forms.ComboBox cbDesArriving;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Label label5;
        public System.Windows.Forms.TextBox tbManArriving;
        private System.Windows.Forms.Label label6;
        public System.Windows.Forms.ComboBox cbManArriving;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label7;
        public System.Windows.Forms.TextBox ETB2Count;
        public System.Windows.Forms.TextBox ETB1Count;


    }
}