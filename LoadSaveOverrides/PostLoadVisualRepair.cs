using System;
using UnityEngine;

namespace KaCMultiplayer.LoadSaveOverrides
{
    internal static class PostLoadVisualRepair
    {
        private static int remaining;
        private static int nextTick;
        private static bool rebuiltVillagers;

        public static void Schedule()
        {
            remaining = 4;
            nextTick = Main.FixedUpdateInterval + 5;
            rebuiltVillagers = false;
        }

        public static void Reset()
        {
            remaining = 0;
            rebuiltVillagers = false;
        }

        public static void Tick()
        {
            if (remaining <= 0 || Main.FixedUpdateInterval < nextTick) return;

            try
            {
                SessionPlayer session = null;
                Player local = Player.inst;
                if (Main.kCPlayers.TryGetValue(Main.PlayerSteamID, out session)
                    && session != null && session.inst != null)
                    local = session.inst;

                if (local == null || local.PlayerLandmassOwner == null || World.inst == null)
                {
                    nextTick = Main.FixedUpdateInterval + 30;
                    return;
                }

                int pass = 5 - remaining;
                remaining--;
                nextTick = Main.FixedUpdateInterval + (pass < 2 ? 30 : 120);
                LandmassOwner owner = local.PlayerLandmassOwner;
                int banner = owner.bannerIdx;
                if (banner < 0 && session != null) banner = session.banner;
                if (banner < 0) banner = Main.localChosenBanner;
                if (banner < 0 || banner >= World.inst.liverySets.Count) banner = 0;

                // Force the runtime material references even when the saved banner number already
                // matches. Player.SetIndexedBanner deliberately no-ops on an equal number.
                Main.SetKingdomBanner(owner, banner, "post-load guest visual repair");

                int buildings = 0;
                if (local.Buildings != null)
                    for (int i = 0; i < local.Buildings.Count; i++)
                    {
                        Building b = local.Buildings.data[i];
                        if (b == null) continue;
                        try { b.UpdateMaterialSelection(); buildings++; } catch { }
                    }

                if (BuildUI.inst != null)
                {
                    try { BuildUI.inst.UpdateMaterials(banner); } catch { }
                    BuildMenuMaterials.Refresh(pass == 1 || remaining == 0);
                }

                try { local.RefreshVisibility(true); } catch { }

                int villagers = 0;
                if (!rebuiltVillagers && pass >= 2)
                {
                    rebuiltVillagers = TryRebuildVillagerInstances(out villagers);
                }

                Main.MarkBannersDirty();
                Main.helper.Log("[LOADVIS] pass " + pass + ": team=" + owner.teamId
                    + " banner=" + banner + " buildings=" + buildings
                    + " villagersRebatched=" + villagers);
            }
            catch (Exception ex) { Main.LogEx("post-load guest visual repair", ex); }
        }

        private static bool TryRebuildVillagerInstances(out int count)
        {
            count = 0;
            if (VillagerSystem.inst == null || VillagerSystem.inst.instances == null) return false;

            for (int i = 0; i < VillagerSystem.inst.instances.Count; i++)
                if (VillagerSystem.inst.instances.data[i] != null)
                    VillagerSystem.inst.instances.data[i].ResetInstances();

            var all = Villager.villagers;
            if (all == null) return false;

            for (int i = 0; i < all.Count; i++)
            {
                Villager v = all.data[i];
                if (v == null) continue;
                int type = (int)v.MeshType;
                if (type < 0 || type >= VillagerSystem.inst.instances.Count) type = 0;

                VillagerSystem.VillagerInstanceSystem instances = VillagerSystem.inst.instances.data[type];
                if (instances == null) continue;
                Vector3 scale = KaCMultiplayer.Net.PrivateField.Get<Vector3>(v, "fullScale", Vector3.one);
                instances.AddInstance(v, v.Pos, v.Rot, scale);
                instances.headColors.data[v.s][v.idx] = v.headColor;
                instances.bodyColors.data[v.s][v.idx] = v.bodyColor;
                count++;
            }
            return true;
        }
    }
}
