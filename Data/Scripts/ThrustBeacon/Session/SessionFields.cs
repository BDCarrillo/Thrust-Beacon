using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace ThrustBeacon
{
    public partial class Session : MySessionComponentBase
    {
        private readonly Stack<GridComp> _gridCompPool = new Stack<GridComp>(128);
        private readonly ConcurrentCachingList<MyCubeBlock> _startBlocks = new ConcurrentCachingList<MyCubeBlock>();
        private readonly ConcurrentCachingList<MyCubeGrid> _startGrids = new ConcurrentCachingList<MyCubeGrid>();
        internal readonly List<GridComp> GridList = new List<GridComp>();
        internal readonly ConcurrentDictionary<IMyCubeGrid, GridComp> GridMap = new ConcurrentDictionary<IMyCubeGrid, GridComp>();
        internal readonly List<IMyPlayer> PlayerList = new List<IMyPlayer>();
        internal static List<SignalComp> SignalList = new List<SignalComp>();
        internal static MyConcurrentList<SignalComp> ReceivedSignalList = new MyConcurrentList<SignalComp>();
        internal readonly List<SignalComp> DrawList = new List<SignalComp>();


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
