﻿using CoreSystems.Api;
using Digi.Example_NetworkProtobuf;
using Draygo.API;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using VRage;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace ThrustBeacon
{
    public partial class Session : MySessionComponentBase
    {
        internal static int Tick;

        internal bool Client;
        internal bool Server;
        internal bool MPActive;
        static HudAPIv2 hudAPI;
        WcApi wcAPI;
        public Networking Networking = new Networking(1337); //TODO: Pick a new number based on mod ID
        internal MyStringId symbol = MyStringId.GetOrCompute("FrameSignal");
        internal MyStringId symbolOffscreenArrow = MyStringId.GetOrCompute("ArrowOffset");

        internal MyStringId symbolOffscreen = MyStringId.GetOrCompute("Arrow");
        internal List<MyStringId> symbolList = new List<MyStringId>(){MyStringId.GetOrCompute("IdleSignal"), MyStringId.GetOrCompute("SmallSignal"), MyStringId.GetOrCompute("MediumSignal"),
        MyStringId.GetOrCompute("LargeSignal"), MyStringId.GetOrCompute("HugeSignal"), MyStringId.GetOrCompute("MassiveSignal")};
        internal List<string> messageList = new List<string>() {"Idle Sig", "Small Sig", "Medium Sig", "Large Sig", "Huge Sig", "Massive Sig"};


        internal float symbolHeight = 0f;//Leave this as zero, monitor aspect ratio is figured in later
        internal float aspectRatio = 0f;
        internal Vector2D offscreenSquish = new Vector2D(0.9, 0.7);
        internal int viewDist = 0;
        internal float offscreenHeight = 0f;

        private readonly Stack<GridComp> _gridCompPool = new Stack<GridComp>(128);
        private readonly ConcurrentCachingList<MyCubeBlock> _startBlocks = new ConcurrentCachingList<MyCubeBlock>();
        private readonly ConcurrentCachingList<MyCubeGrid> _startGrids = new ConcurrentCachingList<MyCubeGrid>();
        internal readonly List<GridComp> GridList = new List<GridComp>();
        internal readonly ConcurrentDictionary<IMyCubeGrid, GridComp> GridMap = new ConcurrentDictionary<IMyCubeGrid, GridComp>();
        internal List<IMyPlayer> PlayerList = new List<IMyPlayer>();
        internal static ConcurrentDictionary<long, MyTuple<SignalComp, int>> SignalList = new ConcurrentDictionary<long, MyTuple<SignalComp, int>>();
        internal ICollection<MyTuple<MyEntity, float>> threatList = new List<MyTuple<MyEntity, float>>();
        internal ICollection<MyEntity> obsList = new List<MyEntity>();
        internal List<long> entityIDList = new List<long>();
        internal static List<GridComp> shutdownList = new List<GridComp>();

        internal void StartComps()
        {
            try
            {
                _startGrids.ApplyAdditions();
                for (int i = 0; i < _startGrids.Count; i++)
                {
                    var grid = _startGrids[i];

                    if (grid.IsPreview)
                        continue;

                    var gridComp = _gridCompPool.Count > 0 ? _gridCompPool.Pop() : new GridComp();
                    gridComp.Init(grid, this);

                    GridList.Add(gridComp);
                    GridMap[grid] = gridComp;
                    grid.OnClose += OnGridClose;
                }
                _startGrids.ClearImmediate();

                _startBlocks.ApplyAdditions();
                for (int i = 0; i < _startBlocks.Count; i++)
                {
                    var block = _startBlocks[i];

                    if (block?.CubeGrid?.Physics == null || block.CubeGrid.IsPreview)
                        continue;

                    GridComp gridComp;
                    if (!GridMap.TryGetValue(block.CubeGrid, out gridComp))
                        continue;

                    var thruster = block as IMyThrust;
                    if (thruster != null)
                    {
                        gridComp.FatBlockAdded(block);
                        continue;
                    }
                }
                _startBlocks.ClearImmediate();
            }
            catch (Exception ex)
            { }
        }
        private void Clean()
        {
            _gridCompPool.Clear();
            _startBlocks.ClearImmediate();
            _startGrids.ClearImmediate();
            GridList.Clear();
            GridMap.Clear();
        }
        private void OnEntityCreate(MyEntity entity)
        {
            var grid = entity as MyCubeGrid;
            if (grid != null)
            {
                grid.AddedToScene += addToStart => _startGrids.Add(grid);
            }

            var thruster = entity as MyThrust;
            if (thruster != null)
            {
                entity.AddedToScene += addToStart => _startBlocks.Add(thruster);
            }
        }

        private void OnGridClose(IMyEntity entity)
        {
            var grid = entity as MyCubeGrid;

            GridComp comp;
            if (GridMap.TryRemove(grid, out comp))
            {
                GridList.Remove(comp);

                comp.Clean();
                _gridCompPool.Push(comp);
            }
        }

    }
}
