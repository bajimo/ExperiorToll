using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Experior.Catalog.Dematic.ATC
{
    public partial class ProjectFieldTools : Form
    {
        ATCCaseLoad caseLoad = null;
        ATCTray tray = null;
        ATCEuroPallet euroPallet = null;
        public ProjectFieldTools(ATCCaseLoad CaseLoad)
        {
            InitializeComponent();
            caseLoad = CaseLoad;

            label1.Hide(); field1Text.Hide();
            label2.Hide(); field2Text.Hide();
            label3.Hide(); field3Text.Hide();
            label4.Hide(); field4Text.Hide();
            label5.Hide(); field5Text.Hide();

            if (caseLoad.ProjectFields.Count > 0 && caseLoad.ProjectFields.Count < 6)
            {
                switch (caseLoad.ProjectFields.Count)
                {
                    case 1: Height = 105; save.Location = new Point(375, 30); cancel.Location = new Point(262, 30); break;
                    case 2: Height = 140; save.Location = new Point(375, 55); cancel.Location = new Point(262, 55); break;
                    case 3: Height = 165; save.Location = new Point(375, 80); cancel.Location = new Point(262, 80); break;
                    case 4: Height = 190; save.Location = new Point(375, 105); cancel.Location = new Point(262, 105); break;
                }

                int i = 1;
                foreach (var field in caseLoad.ProjectFields)
                {
                    switch (i)
                    {
                        case 1: label1.Text = field.Key; field1Text.Text = field.Value; label1.Show(); field1Text.Show(); break;
                        case 2: label2.Text = field.Key; field2Text.Text = field.Value; label2.Show(); field2Text.Show(); break;
                        case 3: label3.Text = field.Key; field3Text.Text = field.Value; label3.Show(); field3Text.Show(); break;
                        case 4: label4.Text = field.Key; field4Text.Text = field.Value; label4.Show(); field4Text.Show(); break;
                        case 5: label5.Text = field.Key; field5Text.Text = field.Value; label5.Show(); field5Text.Show(); break;
                    }
                    i++;
                }
            }
        }

        public ProjectFieldTools(ATCTray trayLoad)
        {
            InitializeComponent();
            tray = trayLoad;

            label1.Hide(); field1Text.Hide();
            label2.Hide(); field2Text.Hide();
            label3.Hide(); field3Text.Hide();
            label4.Hide(); field4Text.Hide();
            label5.Hide(); field5Text.Hide();

            if (tray.ProjectFields.Count > 0 && tray.ProjectFields.Count < 6)
            {
                switch (tray.ProjectFields.Count)
                {
                    case 1: Height = 85; save.Location = new Point(375, 30); cancel.Location = new Point(262, 30); break;
                    case 2: Height = 120; save.Location = new Point(375, 55); cancel.Location = new Point(262, 55); break;
                    case 3: Height = 145; save.Location = new Point(375, 80); cancel.Location = new Point(262, 80); break;
                    case 4: Height = 170; save.Location = new Point(375, 105); cancel.Location = new Point(262, 105); break;
                }

                int i = 1;
                foreach (var field in tray.ProjectFields)
                {
                    switch (i)
                    {
                        case 1: label1.Text = field.Key; field1Text.Text = field.Value; label1.Show(); field1Text.Show(); break;
                        case 2: label2.Text = field.Key; field2Text.Text = field.Value; label2.Show(); field2Text.Show(); break;
                        case 3: label3.Text = field.Key; field3Text.Text = field.Value; label3.Show(); field3Text.Show(); break;
                        case 4: label4.Text = field.Key; field4Text.Text = field.Value; label4.Show(); field4Text.Show(); break;
                        case 5: label5.Text = field.Key; field5Text.Text = field.Value; label5.Show(); field5Text.Show(); break;
                    }
                    i++;
                }
            }
        }

        public ProjectFieldTools(ATCEuroPallet PalletLoad)
        {
            InitializeComponent();
            euroPallet = PalletLoad;

            label1.Hide(); field1Text.Hide();
            label2.Hide(); field2Text.Hide();
            label3.Hide(); field3Text.Hide();
            label4.Hide(); field4Text.Hide();
            label5.Hide(); field5Text.Hide();

            if (euroPallet.ProjectFields.Count > 0 && euroPallet.ProjectFields.Count < 6)
            {
                switch (euroPallet.ProjectFields.Count)
                {
                    case 1: Height = 85; save.Location = new Point(375, 30); cancel.Location = new Point(262, 30); break;
                    case 2: Height = 120; save.Location = new Point(375, 55); cancel.Location = new Point(262, 55); break;
                    case 3: Height = 145; save.Location = new Point(375, 80); cancel.Location = new Point(262, 80); break;
                    case 4: Height = 170; save.Location = new Point(375, 105); cancel.Location = new Point(262, 105); break;
                }

                int i = 1;
                foreach (var field in euroPallet.ProjectFields)
                {
                    switch (i)
                    {
                        case 1: label1.Text = field.Key; field1Text.Text = field.Value; label1.Show(); field1Text.Show(); break;
                        case 2: label2.Text = field.Key; field2Text.Text = field.Value; label2.Show(); field2Text.Show(); break;
                        case 3: label3.Text = field.Key; field3Text.Text = field.Value; label3.Show(); field3Text.Show(); break;
                        case 4: label4.Text = field.Key; field4Text.Text = field.Value; label4.Show(); field4Text.Show(); break;
                        case 5: label5.Text = field.Key; field5Text.Text = field.Value; label5.Show(); field5Text.Show(); break;
                    }
                    i++;
                }
            }
        }

        private void save_Click(object sender, EventArgs e)
        {
            List<string> fieldList = caseLoad.ProjectFields.Keys.ToList();
            int i = 1;
            foreach (string field in fieldList)
            {
                switch (i)
                {
                    case 1: caseLoad.ProjectFields[field] = field1Text.Text; break;
                    case 2: caseLoad.ProjectFields[field] = field2Text.Text; break;
                    case 3: caseLoad.ProjectFields[field] = field3Text.Text; break;
                    case 4: caseLoad.ProjectFields[field] = field4Text.Text; break;
                    case 5: caseLoad.ProjectFields[field] = field5Text.Text; break;
                }
                i++;
            }
            Close();
        }

        private void cancel_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
