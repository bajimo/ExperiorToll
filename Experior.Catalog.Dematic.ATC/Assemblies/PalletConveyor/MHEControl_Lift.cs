using Experior.Catalog.Dematic.Pallet.Assemblies;
using Experior.Core.Loads;
using Experior.Dematic.Base;
using System;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.ATC.Assemblies.PalletConveyor
{
    public class MHEControl_Lift : MHEControl
    {
        private LiftATCInfo liftATCInfo;
        private MHEController_Pallet palletPLC;
        private Lift lift;
        private static Experior.Core.Timer delayTimer = new Experior.Core.Timer(2);

        #region Constructors

        public MHEControl_Lift(LiftATCInfo info, Lift liftAssembly)
        {
            liftATCInfo = info;
            Info = info;  // set this to save properties 
            lift = liftAssembly;
            palletPLC = lift.Controller as MHEController_Pallet;
            lift.OnLiftRaised = OnLiftRaised;
            
        }

        public void OnLiftRaised(Lift lift, Load load)
        {
            // Add delay then lower the lift
            delayTimer.OnElapsed += LowerLift_OnElapsed;
            delayTimer.Start();
        }

        void LowerLift_OnElapsed(Experior.Core.Timer sender)
        {
            delayTimer.OnElapsed -= LowerLift_OnElapsed;
            lift.LowerLift();
        }

        public override void Dispose()
        {
            delayTimer.Dispose();
        }

        #endregion

    }

    [Serializable]
    [XmlInclude(typeof(LiftATCInfo))]
    public class LiftATCInfo : ProtocolInfo
    {

    }

}
