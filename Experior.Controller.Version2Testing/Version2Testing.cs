using System;
using System.Collections.Generic;
using System.Collections;
using System.Drawing;
using System.Linq;
using Experior.Catalog.Assemblies;
using Experior.Catalog.MainBK25;
using Experior.Core.Loads;
using Experior.Core.Routes;
using Experior.Dematic.Base;
using Experior.Core.Communication;
using Experior.Catalog.Assemblies.BK10;
using Experior.Core.Assemblies;
//using Experior.Catalog.BK10Interfaces;
using Microsoft.DirectX;
using System.Windows.Forms;
using Experior.Catalog.Assemblies.Multi_Shuttle;
using System.ComponentModel;

namespace Experior.Controller
{
    public partial class Version2Testing : Experior.Catalog.ControllerExtended
    {
        Experior.Core.Timer ticker = new Core.Timer(1); //Seconds! Why doesn't it tell you this?
        Random random = new Random();


        public Version2Testing() : base("Version2Testing")
        {
            //StandardConstructor();
            //EMUDAIConnectionConstructor();

            Experior.Core.Environment.Scene.OnResetCompleted += new Core.Environment.Scene.ResetCompletedEvent(Scene_OnResetCompleted);

            ticker.OnElapsed += new Core.Timer.ElapsedEvent(ticker_OnElapsed);
            ticker.AutoReset = true;
            ticker.Start();

            Experior.Catalog.Assemblies.BK10.MergeDivertConveyor.OnDivertPoint += new MergeDivertConveyor.OnDivertPointEvent(MergeDivertConveyor_OnDivertPoint);



            //Get all enter events on diverters
            //Experior.Catalog.Logistic.Track.TransportSections.Diverter.Enter += new Catalog.Logistic.Track.TransportSections.Diverter.EnterEvent(Diverter_Enter);

            //Get all enter events on two way transfer conveyors
            //Experior.Catalog.Assemblies.BK10.TwoWayTransferConveyor.Enter += new TwoWayTransferConveyor.EnterEvent(TwoWayTransferConveyor_Enter);
            ////Get all enter events on merge/divert
            //Experior.Catalog.Assemblies.BK10.MergeDivertConveyor.Enter += new MergeDivertConveyor.EnterEvent(MergeDivertConveyor_Enter);
            //Experior.Catalog.Assemblies.BK10.MergeDivertConveyor.OnDivertPoint += new MergeDivertConveyor.OnDivertPointEvent(MergeDivertConveyor_OnDivertPoint);

            //Experior.Catalog.Assemblies.BK10.StraightConveyor.OnToteExitedLane += new StraightConveyor.OnToteExitedLaneEvent(OnToteExitedLane);
            //Experior.Catalog.Assemblies.BK10.StraightConveyor.OnToteEnteredLane += new StraightConveyor.OnToteEnteredLaneEvent(OnToteEnteredLane);

            //Experior.Catalog.Assemblies.BK10.LuffingBeltConveyor.Enter += new Catalog.Logistic.Track.VerticalRotationDiverter.EnterEvent(LuffingBeltConveyor_Enter);
        }

        void MergeDivertConveyor_OnDivertPoint(MergeDivertConveyor sender, Load load)
        {
            //Test to randomly route cases on a divert point
            List<Direction> directions = new List<Direction>();
            if (sender.LeftMode == MergeDivertConveyor.Modes.Divert) directions.Add(Direction.Left);
            if (sender.RightMode == MergeDivertConveyor.Modes.Divert) directions.Add(Direction.Right);
            if (sender.StraightMode == MergeDivertConveyor.Modes.Divert) directions.Add(Direction.Straight);

            int route = random.Next(directions.Count);

            if (directions[route] == Direction.Straight)
                sender.RouteLoadStraight(load);
            else if (directions[route] == Direction.Left)
                sender.RouteLoadLeft(load);
            else if (directions[route] == Direction.Right)
                sender.RouteLoadRight(load);
        }

        void Scene_OnResetCompleted()
        {
            
        }

        void ticker_OnElapsed(Core.Timer sender)
        {
            
        }

        protected override void Received(Core.Communication.Connection connection, string telegram)
        {
      
        }

        public bool IsBitSet(ushort word, int bit)
        {
            int shiftBit = bit - 1;
            return (word & (1 << shiftBit)) != 0;
        }

        private void ResetStandard()
        {
            Experior.Core.Environment.Scene.Reset();

            foreach (Core.Communication.Connection connection in Core.Communication.Connection.Items.Values)
            {
                connection.Disconnect();
            }
        }

        public override void Dispose()
        {
            ticker.Dispose();

            //Experior.Catalog.Logistic.Track.TransportSections.Diverter.Enter -= new Catalog.Logistic.Track.TransportSections.Diverter.EnterEvent(Diverter_Enter);

           
            Core.Environment.UI.Toolbar.Remove(Speed1);
            Core.Environment.UI.Toolbar.Remove(Speed5);
            Core.Environment.UI.Toolbar.Remove(Speed10);
            Core.Environment.UI.Toolbar.Remove(Speed20);

            // Core.Environment.UI.Toolbar.Remove(btnAutomodImport);
            Core.Environment.UI.Toolbar.Remove(btnWMSt);
            Core.Environment.UI.Toolbar.Remove(fps1);
            Core.Environment.UI.Toolbar.Remove(Reset);
            base.Dispose();
        }
    }
}


