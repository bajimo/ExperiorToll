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

            Core.Environment.Invoke(() =>
            {
                dataGridViewEquipmentStatuses.DataSource = controller.EquipmentStatuses;
                dataGridViewEquipmentStatuses.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.ColumnHeader);
            });
        }
    }
}