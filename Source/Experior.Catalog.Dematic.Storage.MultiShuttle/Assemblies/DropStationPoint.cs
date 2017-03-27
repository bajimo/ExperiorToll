using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Experior.Core.Parts;
using Experior.Dematic;
using Experior.Dematic.Storage.Base;
using System.ComponentModel;
using Microsoft.DirectX;
using System.Drawing;
using Experior.Dematic.Base;

namespace Experior.Catalog.Dematic.Storage.Assemblies
{
    public class DropStationPoint : FixPoint, Core.IEntity
    {
        [Browsable(false)]
        public MultiShuttle Multishuttle;
        [Browsable(false)]
        public Elevator Elevator;
        [Browsable(false)]
        public LevelHeight SavedLevel { get; set; }
        [Browsable(false)]
        public string DropPositionGroupSide { get; set; }

        [CategoryAttribute("Configuration")]
        [DescriptionAttribute("Level")]
        [DisplayName("Level")]
        public string Level 
        {
            get { return SavedLevel.Level; }
            set { SavedLevel.Level = value; }
        }

        [CategoryAttribute("Configuration")]
        [DescriptionAttribute("Level Height in meter")]
        [DisplayName("Level Height (m.)")]
        public float LevelHeight
        {
            get { return LocalPosition.Y; }
            set
            {
                LocalPosition = new Vector3(LocalPosition.X, value, LocalPosition.Z);
                SavedLevel.Height = value;
            }
        }

        private string name;

        [CategoryAttribute("Configuration")]
        [DescriptionAttribute("Name")]
        [DisplayName("Name")]
        public new string Name
        {
            get { return name; }
            set { name = value; }
        }

        public DropStationPoint(Types type, MultiShuttle parent, Elevator elevator) : base(type, parent)
        {
            Multishuttle = parent;
            Elevator = elevator;

            Core.Communication.Internal.AddListener(this, new Core.Communication.Internal.RecieveMessage(listen));
        }

        /// <summary>
        /// Listen for Handshake message from drop station (case conveyor)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="reciever"></param>
        /// <param name="message"></param>
        /// <param name="broadcast"></param>
        private void listen(object sender, object reciever, object message, bool broadcast)
        {
            if (broadcast)
                return;

            if (message is HandshakeMessage)
            {
                HandshakeMessage msg = message as HandshakeMessage;

                if (msg.MessageType == HandshakeMessage.MessageTypes.Tote_Arrived_On_Dropstation)
                {
                    Case_Load caseload = msg.Load as Case_Load;
                   // if (ParentMultishuttle.Control != null)
                     //   ParentMultishuttle.Control.ToteArrivedAtConvDropStation(this, Elevator, caseload, ParentMultishuttle);
                }
            }
        }

        public override void Dispose()
        {
            Core.Communication.Internal.RemoveListener(this);
            base.Dispose();
        }

        [Browsable(false)]
        public bool Deletable { get; set; }
        [Browsable(false)]
        public ulong EntityId { get; set; }
        [Browsable(false)]
        public Image Image { get { return null; } }
        [Browsable(false)]
        public bool ListSolutionExplorer { get; set; }
        [Browsable(false)]
        public bool Warning { get { return false; } }
    }
}
