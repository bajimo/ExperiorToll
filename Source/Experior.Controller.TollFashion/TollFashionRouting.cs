using Experior.Core.Loads;
using Experior.Core.Routes;

namespace Experior.Controller.TollFashion
{
    public partial class TollFashionRouting : Catalog.ControllerExtended
    {
        public TollFashionRouting() : base("TollFashionRouting")
        {
            StandardConstructor();

            Core.Environment.Scene.OnResetCompleted += Scene_OnResetCompleted;
        }

        void Scene_OnResetCompleted()
        {
            
        }

        protected override void Arriving(INode node, Load load)
        {
            if (node.Name.StartsWith("ROUTETO:"))
            {
                var dest = node.Name.Replace("ROUTETO:", "");
            }
        }

        private void ResetStandard()
        {
            Core.Environment.Scene.Reset();

            foreach (Core.Communication.Connection connection in Core.Communication.Connection.Items.Values)
            {
                connection.Disconnect();
            }
        }

        public override void Dispose()
        {
            Core.Environment.UI.Toolbar.Remove(Speed1);
            Core.Environment.UI.Toolbar.Remove(Speed2);
            Core.Environment.UI.Toolbar.Remove(Speed5);
            Core.Environment.UI.Toolbar.Remove(Speed10);
            Core.Environment.UI.Toolbar.Remove(Speed20);

            Core.Environment.UI.Toolbar.Remove(Reset);
            Core.Environment.UI.Toolbar.Remove(fps1);
            Core.Environment.UI.Toolbar.Remove(localProp);
            Core.Environment.UI.Toolbar.Remove(connectButt);
            Core.Environment.UI.Toolbar.Remove(disconnectButt);
            Core.Environment.Scene.OnResetCompleted -= Scene_OnResetCompleted;
            base.Dispose();
        }
    }
}