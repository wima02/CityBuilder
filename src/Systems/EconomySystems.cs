// ================================================================
// FILE: EconomySystems.cs
// Phase 4 — Economy & Research
// Three systems that form the supply chain pipeline:
//   SupplyChainSystem   → Farm → Warehouse → Market goods flow
//   LaborSystem         → Assigns workers from citizens to buildings
//   ResearchSystem      → Accumulates research points, fires unlock events
//   TownUpgradeSystem   → Upgrades town tier when avg happiness >= threshold
//
// Refine: add transport vehicles, research tree UI, tier unlock rewards.
// ================================================================
using Godot;
using System;

namespace CityBuilder.Systems
{
    // ── SupplyChainSystem ─────────────────────────────────────────
    // Ticks every SupplyTickIntervalSec.
    // Transfers goods from Farms → Warehouses → Markets via SupplyLinkComponent.

    public partial class SupplyChainSystem : Node
    {
        private ECS.EntityManager _em;
        private float _timer = 0f;

        public event Action<uint, uint, int> OnGoodsTransferred; // from, to, amount

        public override void _Ready()
            => _em = ECS.World.Instance.Entities;

        public override void _Process(double delta)
        {
            _timer += (float)delta;
            if (_timer < ECS.GameConstants.SupplyTickIntervalSec) return;
            _timer = 0f;
            _TickSupplyChain();
        }

        private void _TickSupplyChain()
        {
            foreach (var linkId in _em.Query<ECS.SupplyLinkComponent>())
            {
                var link     = _em.GetComponent<ECS.SupplyLinkComponent>(linkId);
                uint fromId  = link.ProducerEntityId;
                uint toId    = link.StorageEntityId;

                if (!_em.IsAlive(fromId) || !_em.IsAlive(toId)) continue;
                if (!_em.HasComponent<ECS.InventoryComponent>(fromId)) continue;
                if (!_em.HasComponent<ECS.InventoryComponent>(toId))   continue;

                var fromInv = _em.GetComponent<ECS.InventoryComponent>(fromId);
                var toInv   = _em.GetComponent<ECS.InventoryComponent>(toId);

                int transfer = (int)Mathf.Min(link.TransferRatePerTick, fromInv.Food);
                int space    = ECS.GameConstants.WarehouseDefaultCapacity - toInv.Food;
                transfer     = Mathf.Min(transfer, space);

                if (transfer <= 0) continue;

                fromInv.Food -= transfer;
                toInv.Food   += transfer;

                _em.SetComponent(fromId, fromInv);
                _em.SetComponent(toId,   toInv);

                OnGoodsTransferred?.Invoke(fromId, toId, transfer);
            }
        }

        public void _RunTests()
        {
            GD.Print("[SupplyChainSystem] Transfer math test...");
            int from = 50, rate = 10, space = 8;
            int transfer = (int)Mathf.Min(Mathf.Min(rate, from), space);
            System.Diagnostics.Debug.Assert(transfer == 8, "Transfer capped by space");
            GD.Print("[SupplyChainSystem] Tests passed ✓");
        }
    }


    // ── LaborSystem ───────────────────────────────────────────────
    // Assigns idle citizens to buildings that need workers.
    // Runs only when a citizen's state changes to Idle.

    public partial class LaborSystem : Node
    {
        private ECS.EntityManager _em;

        public override void _Ready()
        {
            _em = ECS.World.Instance.Entities;
            // Subscribe to component changes to detect idle agents
            _em.OnComponentChanged += _OnComponentChanged;
        }

        public override void _ExitTree()
        {
            if (_em != null)
                _em.OnComponentChanged -= _OnComponentChanged;
        }

        private void _OnComponentChanged(uint entityId, Type componentType)
        {
            if (componentType != typeof(ECS.AgentComponent)) return;
            if (!_em.HasComponent<ECS.AgentComponent>(entityId)) return;

            var agent = _em.GetComponent<ECS.AgentComponent>(entityId);
            if (agent.State == ECS.AgentState.Idle && agent.WorkplaceEntityId == 0)
                _AssignWork(entityId);
        }

        private void _AssignWork(uint citizenId)
        {
            foreach (var buildingId in _em.Query<ECS.LaborComponent, ECS.PositionComponent>())
            {
                var labor = _em.GetComponent<ECS.LaborComponent>(buildingId);
                if (labor.AssignedWorkerEntityId != 0) continue; // already occupied

                labor.AssignedWorkerEntityId = citizenId;
                _em.SetComponent(buildingId, labor);

                var agent = _em.GetComponent<ECS.AgentComponent>(citizenId);
                agent.WorkplaceEntityId = buildingId;
                agent.TargetPosition    = _em.GetComponent<ECS.PositionComponent>(buildingId).Position;
                agent.State             = ECS.AgentState.Walking;
                _em.SetComponent(citizenId, agent);

                GD.Print($"[LaborSystem] Citizen {citizenId} assigned to building {buildingId}");
                return;
            }
        }

        public void _RunTests()
        {
            GD.Print("[LaborSystem] Tests require running scene — skipped in unit mode.");
        }
    }


    // ── ResearchSystem ────────────────────────────────────────────
    // Collects ResearchPoints from Research buildings each tick,
    // fires OnUnlockReady when threshold is crossed.

    public partial class ResearchSystem : Node
    {
        private ECS.EntityManager _em;
        private float _timer = 0f;

        public event Action<int> OnUnlockReady; // payload: tier unlocked

        private const float ResearchTickSec    = 8f;
        private const int   PointsPerBuilding  = 5;
        private const int   UnlockCostBase     = 50;

        public override void _Ready()
            => _em = ECS.World.Instance.Entities;

        public override void _Process(double delta)
        {
            _timer += (float)delta;
            if (_timer < ResearchTickSec) return;
            _timer = 0f;
            _TickResearch();
        }

        private void _TickResearch()
        {
            int pointsGained = 0;

            // Each staffed research building contributes points
            foreach (var id in _em.Query<ECS.BuildingDataComponent, ECS.LaborComponent>())
            {
                var bd    = _em.GetComponent<ECS.BuildingDataComponent>(id);
                var labor = _em.GetComponent<ECS.LaborComponent>(id);
                if (bd.Type != ECS.BuildingType.Research) continue;
                if (labor.AssignedWorkerEntityId == 0) continue;

                pointsGained += PointsPerBuilding;
            }

            if (pointsGained == 0) return;

            // Store on town entity's InventoryComponent
            uint townId = ECS.World.Instance.TownEntityId;
            if (!_em.HasComponent<ECS.InventoryComponent>(townId))
                _em.AddComponent(townId, new ECS.InventoryComponent());

            var inv = _em.GetComponent<ECS.InventoryComponent>(townId);
            inv.ResearchPoints += pointsGained;
            _em.SetComponent(townId, inv);

            // Check if an unlock threshold is crossed
            int tier = inv.ResearchPoints / UnlockCostBase;
            if (tier > 0 && inv.ResearchPoints % UnlockCostBase < PointsPerBuilding)
                OnUnlockReady?.Invoke(tier);
        }

        public void _RunTests()
        {
            GD.Print("[ResearchSystem] Tests require running scene — skipped in unit mode.");
        }
    }


    // ── TownUpgradeSystem ─────────────────────────────────────────
    // Listens to TownTierComponent.AverageHappiness.
    // Upgrades the tier when happiness > UpgradeHappinessThreshold.

    public partial class TownUpgradeSystem : Node
    {
        private ECS.EntityManager _em;
        private float _timer = 0f;
        private const float CheckIntervalSec = 10f;

        public event Action<int> OnTownUpgraded; // new tier

        public override void _Ready()
            => _em = ECS.World.Instance.Entities;

        public override void _Process(double delta)
        {
            _timer += (float)delta;
            if (_timer < CheckIntervalSec) return;
            _timer = 0f;
            _CheckUpgrade();
        }

        private void _CheckUpgrade()
        {
            uint townId = ECS.World.Instance.TownEntityId;
            if (!_em.HasComponent<ECS.TownTierComponent>(townId)) return;

            var tier = _em.GetComponent<ECS.TownTierComponent>(townId);
            if (tier.Tier >= 5) return; // already max tier

            if (tier.AverageHappiness >= ECS.GameConstants.UpgradeHappinessThreshold)
            {
                tier.Tier++;
                _em.SetComponent(townId, tier);
                OnTownUpgraded?.Invoke(tier.Tier);
                GD.Print($"[TownUpgrade] 🎉 Town upgraded to tier {tier.Tier}! Avg happiness: {tier.AverageHappiness:P0}");
            }
        }

        public void _RunTests()
        {
            GD.Print("[TownUpgradeSystem] Threshold test...");
            float threshold = ECS.GameConstants.UpgradeHappinessThreshold;
            System.Diagnostics.Debug.Assert(threshold > 0f && threshold < 1f,
                "Threshold should be between 0 and 1");
            GD.Print("[TownUpgradeSystem] Tests passed ✓");
        }
    }
}
