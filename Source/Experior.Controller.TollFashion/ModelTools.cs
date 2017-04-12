using System;
using System.Linq;
using System.Windows.Forms;
using Experior.Controller.TollFashion;

namespace Experior.Controller
{
    public partial class ModelTools : Form
    {
        private readonly TollFashionRouting controller;
        public ModelTools(TollFashionRouting controller)
        {
            this.controller = controller;
            InitializeComponent();

            comboBoxCC51CARTON.DataSource = controller.PossibleStatuses;
            comboBoxCC51CARTON.SelectedIndex = 3;
        }

        private void comboBoxCC51CARTON_TextChanged(object sender, System.EventArgs e)
        {
            if (controller.EquipmentStatuses == null)
                return;

            try
            {
                var equipmentStatus = controller.EquipmentStatuses.First(eq => eq.FunctionGroup == labelCC51CARTON.Text);
                equipmentStatus.GroupStatus = comboBoxCC51CARTON.Text;
                equipmentStatus.Plc.SendEquipmentStatus(equipmentStatus.FunctionGroup, equipmentStatus.GroupStatus);
            }
            catch (Exception exception)
            {
                Log.Write("Error setting equipment status");
                Log.Write(exception.ToString());
            }
        }
    }
}
