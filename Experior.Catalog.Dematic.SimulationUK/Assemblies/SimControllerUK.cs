
using Experior.Catalog.Assemblies;
using Experior.Catalog.Dematic.Case.Components;
using Experior.Catalog.Dematic.Case.Devices;
using Experior.Catalog.Dematic.Storage.MultiShuttle.Assemblies;
//using Experior.Catalog.Dematic.Storage.Assemblies;
using Experior.Core;
using Experior.Core.Assemblies;
using Experior.Core.Communication;
using Experior.Core.Forms;
using Experior.Core.Parts;
using Experior.Core.Properties;
using Experior.Dematic.Base;
using Microsoft.DirectX;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
//using System.Windows.Forms;

namespace Experior.Catalog.Dematic.SimulationUK.Assemblies
{
    /// <summary>
    /// This is a PLC that handels Datcom messages
    /// </summary>
    public class SimControllerUK : Assembly, IController
    {
        private Cube plcCube;//, door1, door2, plinth;
        private Text3D displayText;

        //enum CasePLC_State { Unknown, Ready, Auto, NotReady };
        //CasePLC_State PLC_State = CasePLC_State.Unknown;
        //Dictionary<CasePLC_State, Color> statusColours = new Dictionary<CasePLC_State, Color>();
        //CaseDatcomInfo caseDatcomInfo;
        //public Experior.Core.Communication.TCPIP.Connection recieveConnection, sendConnection;
        //public bool plcConnected;
        //private static string telegramTail = ",,13,10";
        public event EventHandler OnControllerDeletedEvent;
        public event EventHandler OnControllerRenamedEvent;

        //public delegate bool TelegramRecievedHandle(CasePLC_Datcom sender, string type, string[] telegramFields, ushort number_of_blocks);
        /// <summary>
        /// Handle project specific telegrams. If false is returned then the plc will handle it. If true is returned the plc expects the user to handle the telegram.
        /// </summary>
        //public TelegramRecievedHandle HandleTelegram;

        //public Dictionary<string, CallForwardLocation> callForwardTable = new Dictionary<string, CallForwardLocation>();


        //public int MaxRoutingTableEntries = int.MaxValue;
        //public int NumberOfDestWords = 4;
        //public Dictionary<string, UInt16[]> routingTable = new Dictionary<string, UInt16[]>(); //SSCCBarcode, Destinations[]     

        public SimControllerUK(SimulationUKInfo info) : base(info)
        {
            //statusColours.Add(CasePLC_State.Unknown, Color.Gainsboro);
            //statusColours.Add(CasePLC_State.Ready, Color.Yellow);
            //statusColours.Add(CasePLC_State.Auto, Color.DarkGreen);
            //statusColours.Add(CasePLC_State.NotReady, Color.Gainsboro);

            //caseDatcomInfo = info;

            plcCube = new Cube(Color.Wheat, 2.5f, 0.5f, 0.5f);

            //door1 = new Cube(Color.Wheat, 0.62f, 1.585f, 0.25f);
            //door2 = new Cube(Color.Wheat, 0.62f, 1.585f, 0.25f);
            //plinth = new Cube(Color.DimGray, 1.32f, 0.1985f, 0.42f);

            Font f = new Font("Helvetica", 0.4f, FontStyle.Bold, GraphicsUnit.Pixel);
            displayText = new Text3D(Color.Blue, 0.4f, 0.3f, f);
            displayText.Pitch = (float)Math.PI / 2;

            //AddPart((RigidPart)displayText, new Vector3(-0.62f, 1.25f, -0.125f));

            AddPart((RigidPart)plcCube, new Vector3(0, -0.25f, 0));           
            AddPart((RigidPart)displayText, new Vector3(-1.1f, -0.1f, -0.125f));


            
            //AddPart((RigidPart)door1, new Vector3(-0.325f, 0.5425f, -0.1f));
            //AddPart((RigidPart)door2, new Vector3(0.325f, 0.5425f, -0.1f));
            //AddPart((RigidPart)plinth, new Vector3(0, -0.355f, 0));

            displayText.Text = info.name;
            OnNameChanged += SimulationUK_OnNameChanged;

            //if (info.receiverID != 0)
                //ReceiverID = info.receiverID;

            //if (info.senderID != 0)
                //SenderID = info.senderID;

        }

        public override void DoubleClick()
        {
            base.DoubleClick();
        }

        public override void Dispose()
        {
            System.Windows.Forms.DialogResult result = System.Windows.Forms.MessageBox.Show("All control objects created by this "+
            "Controller will also be deleted. Are you sure that you want to delete this controller?", "Confirm Controller Deletion",
            System.Windows.Forms.MessageBoxButtons.OKCancel);

            if (result == System.Windows.Forms.DialogResult.OK)
            {
                //callForwardTable.Clear();
                //routingTable.Clear();
                if (OnControllerDeletedEvent != null)
                {
                    OnControllerDeletedEvent(this, new EventArgs());
                }
                base.Dispose();
            }
        }

        void SimulationUK_OnNameChanged(Assembly sender, string current, string old)
        {
            if (OnControllerRenamedEvent != null)
            {
                OnControllerRenamedEvent(this, new EventArgs());
            }
            displayText.Text = Name;
        }

        public override string Category
        {
           // get { return "Simulation UK"; }
            get { return "Controller"; }

        }

        public override Image Image
        {
            get { return Common.Icons.Get("Globe"); }
        }

        /// <summary>
        /// Creates the correct type of object to display to the user. This will depend on type of kit
        /// The communication point uses this to display the relevent fields to the user.
        /// Defined in BK10PLC.cs
        /// </summary>
        /// <param name="assem"></param>
        /// <returns></returns>
        public MHEControl CreateMHEControl(IControllable assem, ProtocolInfo info)
        {
            MHEControl protocolConfig = null;   //generic plc config object
            ProtocolInfo protocolInfo = null;  //generic plc config object constructor argument type
            //ProtocolInfo protocolInfo = new ProtocolInfo();  //generic plc config object constructor argument type

            if (assem is MultiShuttle)
            {
                if (info is MultiShuttleSimInfo) // create the defined protocol info
                {
                    info.assem = assem.Name;
                    protocolConfig = new MHEControl_MultiShuttleSimulation(info as MultiShuttleSimInfo, assem as MultiShuttle);
                }
                else //create a new protocol info
                {
                    protocolInfo = new MultiShuttleSimInfo();
                    protocolInfo.assem = assem.Name;
                    protocolConfig = new MHEControl_MultiShuttleSimulation(protocolInfo as MultiShuttleSimInfo, assem as MultiShuttle);
                }
            }
            else if (assem is MergeDivertConveyor)
            {
                if (info is MergeDivertSimulationInfo)
                {
                    ((MergeDivertSimulationInfo)info).assem = assem.Name;
                    protocolConfig = new MHEControl_MergeDivert(info as MergeDivertSimulationInfo, assem as MergeDivertConveyor);
                }
                else //create a new protocol info
                {
                    protocolInfo = new MergeDivertSimulationInfo();
                    protocolInfo.assem = assem.Name;
                    protocolConfig = new MHEControl_MergeDivert(protocolInfo as MergeDivertSimulationInfo, assem as MergeDivertConveyor);
                }
            }
            else if (assem is AngledDivert)
            {
                if (info is AngledDivertSimulationInfo)
                {
                    ((AngledDivertSimulationInfo)info).assem = assem.Name;
                    protocolConfig = new MHEControl_AngledDivert(info as AngledDivertSimulationInfo, assem as AngledDivert);
                }
                else //create a new protocol info
                {
                    protocolInfo = new AngledDivertSimulationInfo();
                    protocolInfo.assem = assem.Name;
                    protocolConfig = new MHEControl_AngledDivert(protocolInfo as AngledDivertSimulationInfo, assem as AngledDivert);
                }
            }
            else if (assem is Transfer)
            {
                if (info is TransferSimulationInfo)
                {
                    ((TransferSimulationInfo)info).assem = assem.Name;
                    protocolConfig = new MHEControl_Transfer(info as TransferSimulationInfo, assem as Transfer);
                }
                else //create a new protocol info
                {
                    protocolInfo = new TransferSimulationInfo();
                    protocolInfo.assem = assem.Name;
                    protocolConfig = new MHEControl_Transfer(protocolInfo as TransferSimulationInfo, assem as Transfer);
                }
            }

            else
            {
                Experior.Core.Environment.Log.Write("Can't create PLC config object ");
                return null;
            }
            //......other assemblies should be added here....do this with generics
            protocolConfig.ParentAssembly = (Assembly)assem;
            return protocolConfig as MHEControl;

        }

        //This is part of the IController interface and probably should not be needed
        //public event PickStationStatus MiniloadPickStationStatusEvent;

        public void RemoveSSCCBarcode(string ULID)
        {
            throw new NotImplementedException();
        }

        //public void SendCraneInputStationArrival(string craneNumber, List<string> CaseBarcodes, string status = "")
        //{
        //    throw new NotImplementedException();
        //}

        //public void SendCraneInputStationArrival(string craneNumber, List<Case_Load> EPCases, string status = "")
        //{
        //    throw new NotImplementedException();
        //}
    }

    [Serializable]
    [TypeConverter(typeof(SimulationUKInfo))]
    public class SimulationUKInfo : AssemblyInfo
    {

    }
}
