using System.Collections.Generic;
using System.Linq;
using Experior.Catalog.Dematic.Case.Components;
using Experior.Core;
using Experior.Core.Routes;

namespace Experior.Controller.TollFashion
{
    public class LoopControl
    {
        private readonly List<ActionPoint> rapidPickMergeLine1 = new List<ActionPoint>();
        private readonly List<ActionPoint> rapidPickMergeLine2 = new List<ActionPoint>();
        private readonly Timer inductTimer;
        private readonly List<StraightBeltConveyor> loopBelts = new List<StraightBeltConveyor>();
        private readonly List<StraightAccumulationConveyor> loopAccu = new List<StraightAccumulationConveyor>();
        private readonly List<CurveBeltConveyor> loopCurves = new List<CurveBeltConveyor>();
        private readonly List<MergeDivertConveyor> loopMerges = new List<MergeDivertConveyor>();
        private readonly List<TwoToOneMerge> loopTwoOneMerges = new List<TwoToOneMerge>();
        private ActionPoint lma, lmb, lmc, lmd;

        private int nextReleaseLine1;
        private int nextReleaseLine2;
        private int loopBlock = 150;
        private int loopOk = 120;
        private bool loopBlocked;

        public LoopControl()
        {
            inductTimer = new Timer(1.16f);
            inductTimer.AutoReset = true;
            inductTimer.OnElapsed += InductTimer_OnElapsed;
            Environment.Scene.OnStarting += Scene_OnStarting;

            for (int i = 1; i <= 24; i++)
            {
                var name = $"LM{i:D2}";
                var merge = Core.Assemblies.Assembly.Items[name] as StraightAccumulationConveyor;
                var ap = merge.TransportSection.Route.InsertActionPoint(merge.Length - 0.15f);
                ap.Edge = ActionPoint.Edges.Leading;
                ap.OnEnter += RapidPickMergeLoopEnter;
                if (i <= 12)
                {
                    rapidPickMergeLine1.Add(ap);
                }
                else
                {
                    rapidPickMergeLine2.Add(ap);
                }
            }

            for (int i = 1; i <= 3; i++)
            {
                var name = $"LA{i:D2}";
                var accu = Core.Assemblies.Assembly.Items[name] as StraightAccumulationConveyor;
                loopAccu.Add(accu);
            }
            for (int i = 1; i <= 39; i++)
            {
                var name = $"LB{i:D2}";
                var belt = Core.Assemblies.Assembly.Items[name] as StraightBeltConveyor;
                loopBelts.Add(belt);
            }
            for (int i = 1; i <= 4; i++)
            {
                var name = $"LC{i:D2}";
                var curve = Core.Assemblies.Assembly.Items[name] as CurveBeltConveyor;
                loopCurves.Add(curve);
            }
            for (int i = 1; i <= 24; i++)
            {
                var name = $"LMD{i:D2}";
                var merge = Core.Assemblies.Assembly.Items[name] as MergeDivertConveyor;
                loopMerges.Add(merge);
            }
            var twoone = Core.Assemblies.Assembly.Items["Two2One"] as TwoToOneMerge;
            loopTwoOneMerges.Add(twoone);

            var lmaConveyor = Core.Assemblies.Assembly.Items["LMA"] as StraightAccumulationConveyor;
            lma = lmaConveyor.TransportSection.Route.InsertActionPoint(lmaConveyor.Length - 0.3f);
            lma.Edge = ActionPoint.Edges.Leading;
            lma.OnEnter += LoopMerge_OnEnter;
            var lmbConveyor = Core.Assemblies.Assembly.Items["LMB"] as StraightBeltConveyor;
            lmb = lmbConveyor.TransportSection.Route.InsertActionPoint(lmbConveyor.Length - 0.3f);
            lmb.Edge = ActionPoint.Edges.Leading;
            lmb.OnEnter += LoopMerge_OnEnter;
            var lmcConveyor = Core.Assemblies.Assembly.Items["LMC"] as StraightBeltConveyor;
            lmc = lmcConveyor.TransportSection.Route.InsertActionPoint(lmcConveyor.Length - 0.3f);
            lmc.Edge = ActionPoint.Edges.Leading;
            lmc.OnEnter += LoopMerge_OnEnter;
            var lmdConveyor = Core.Assemblies.Assembly.Items["LMD"] as StraightBeltConveyor;
            lmd = lmdConveyor.TransportSection.Route.InsertActionPoint(lmdConveyor.Length - 0.3f);
            lmd.Edge = ActionPoint.Edges.Leading;
            lmd.OnEnter += LoopMerge_OnEnter;
        }

        private void LoopMerge_OnEnter(ActionPoint sender, Core.Loads.Load load)
        {
            if (loopBlocked)
                load.Stop();
        }

        private int LoopCount()
        {
            var count = 0;
            count += loopAccu.Sum(a => a.LoadCount);
            count += loopBelts.Sum(a => a.LoadCount);
            count += loopCurves.Sum(a => a.LoadCount);
            count += loopMerges.Sum(a => a.LoadCount);
            count += loopTwoOneMerges.Sum(a => a.LoadCount);
            return count;
        }

        private void RapidPickMergeLoopEnter(ActionPoint sender, Core.Loads.Load load)
        {
            //Wait for induct control to release
            load.Stop();
        }

        private void Scene_OnStarting()
        {
            inductTimer.Start();
        }

        private void ReleaseWaitingLoopMerges()
        {
            //Loop is ok again. Relase 4 loop merges if they are waiting. (Not rapid pick)
            lma.Release();
            lmb.Release();
            lmc.Release();
            lmd.Release();
        }

        private void InductTimer_OnElapsed(Timer sender)
        {
            var loopCount = LoopCount();
            if (loopCount >= loopBlock)
            {
                if (!loopBlocked)
                {
                    Log.Write("Loop filled!");
                    loopBlocked = true;
                }
            }
            if (loopCount < loopOk)
            {
                if (loopBlocked)
                {
                    Log.Write("Loop ok!");
                    loopBlocked = false;
                    ReleaseWaitingLoopMerges();
                }
            }

            if (loopBlocked)
                return;

            for (var i = 0; i < rapidPickMergeLine1.Count; i++)
            {
                var index = (i + nextReleaseLine1) % rapidPickMergeLine1.Count;
                var ap = rapidPickMergeLine1[index];
                if (ap.Active)
                {
                    nextReleaseLine1 = index + 1;
                    ap.Release();
                    break;
                }
            }
            for (var i = 0; i < rapidPickMergeLine2.Count; i++)
            {
                var index = (i + nextReleaseLine2) % rapidPickMergeLine2.Count;
                var ap = rapidPickMergeLine2[index];
                if (ap.Active)
                {
                    nextReleaseLine2 = index + 1;
                    ap.Release();
                    break;
                }
            }

        }

        public void Reset()
        {
            loopBlocked = false;
            nextReleaseLine1 = 0;
            nextReleaseLine2 = 0;
        }

        public void Dispose()
        {
            rapidPickMergeLine1.Clear();
            rapidPickMergeLine2.Clear();
            inductTimer.OnElapsed -= InductTimer_OnElapsed;
            inductTimer.Dispose();
        }
    }
}
