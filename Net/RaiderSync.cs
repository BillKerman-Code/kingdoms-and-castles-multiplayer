using System;
using System.Collections.Generic;
using UnityEngine;
using KaCMultiplayer.Net.Messages;

namespace KaCMultiplayer.Net
{
    /// <summary>The host runs Viking AI; guests display host-positioned transport ships.</summary>
    public static class RaiderSync
    {
        private const int SendEvery = 15;
        private static int tick;
        private static bool hostHadBoats;
        private static readonly HashSet<Guid> mirrored = new HashSet<Guid>();
        private static readonly HashSet<Guid> live = new HashSet<Guid>();
        private static readonly List<Guid> stale = new List<Guid>();

        public static void Reset() { tick=0; hostHadBoats=false; mirrored.Clear(); }

        public static void Tick()
        {
            if (!Main.RaidsEnabled || !NetRouter.IsServer || ShipSystem.inst == null) return;
            if (++tick < SendEvery) return;
            tick=0;
            var m=new RaiderBoatsMessage();
            for(int i=0;i<ShipSystem.inst.ships.Count;i++)
            {
                ShipBase s=ShipSystem.inst.ships.data[i];
                if(s==null || s.type!=ShipBase.ShipType.VikingTroopTransport) continue;
                m.Boats.Add(s.guid); m.Positions.Add(s.GetPos()); m.Rotations.Add(s.GetRot());
            }
            if(m.Boats.Count>0 || hostHadBoats) NetRouter.Broadcast(m);
            hostHadBoats=m.Boats.Count>0;
        }

        public static void Apply(RaiderBoatsMessage m)
        {
            if(NetRouter.IsServer || m==null || ShipSystem.inst==null || RaiderSystem.inst==null) return;
            int count=Math.Min(m.Boats.Count,Math.Min(m.Positions.Count,m.Rotations.Count));
            live.Clear();
            for(int i=0;i<count;i++)
            {
                live.Add(m.Boats[i]);
                ShipBase boat=Find(m.Boats[i]);
                if(boat==null)
                {
                    GameObject prefab=RaiderSystem.inst.raiderTransportShipPrefab;
                    if(prefab==null) continue;
                    boat=UnityEngine.Object.Instantiate(prefab).GetComponent<ShipBase>();
                    if(boat==null) continue;
                    boat.Init(1); boat.guid=m.Boats[i];
                    mirrored.Add(boat.guid);
                    NetLog.Info("Viking boat mirror: "+boat.guid);
                }
                boat.InitializePos(m.Positions[i]);
                boat.transform.rotation=m.Rotations[i];
            }

            stale.Clear();
            foreach(Guid id in mirrored) if(!live.Contains(id)) stale.Add(id);
            for(int i=0;i<stale.Count;i++)
            {
                ShipBase gone=Find(stale[i]);
                if(gone!=null) UnityEngine.Object.Destroy(gone.gameObject);
                mirrored.Remove(stale[i]);
            }
        }

        private static ShipBase Find(Guid id)
        {
            for(int i=0;i<ShipSystem.inst.ships.Count;i++)
            { ShipBase s=ShipSystem.inst.ships.data[i]; if(s!=null && s.guid==id) return s; }
            return null;
        }

        public static bool ShouldTickShip(ShipBase ship)
        {
            if(!Main.RaidsEnabled || !NetRouter.IsConnected || NetRouter.IsServer || ship==null) return true;
            // Ogre transports are not included in RaiderBoatsMessage yet. Let vanilla simulate
            // them on guests instead of freezing an invisible or permanently stationary ship.
            return ship.type!=ShipBase.ShipType.VikingTroopTransport;
        }
    }
}
