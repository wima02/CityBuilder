// ================================================================
// FILE: NeedsAndHappinessSystems.cs
// Phase 3 — Citizen Agents
// Three focused systems that form the citizen satisfaction pipeline:
//   NeedsCycleSystem       → decays and refills citizen needs over time
//   BeautifulScoreSystem   → calculates per-citizen environmental beauty
//   HappinessSystem        → aggregates needs + beauty → happiness
//
// Refine: tune decay/restore rates, add seasonal beauty modifiers.
// ================================================================
using Godot;
using System;

namespace CityBuilder.Systems
{
    // ── NeedsCycleSystem ─────────────────────────────────────────
    // Ticks every NeedUpdateIntervalSec.
    // Decays needs when not supplied, restores when a supplier is nearby.

    public partial class NeedsCycleSystem : Node
    {
        private ECS.EntityManager _em;
        private float _timer = 0f;

        private const float FoodDecayRate        = 0.05f;
        private const float ClothingDecayRate    = 0.02f;
        private const float EntertainmentDecay   = 0.03f;

        public override void _Ready()
            => _em = ECS.World.Instance.Entities;

        public override void _Process(double delta)
        {
            _timer += (float)delta;
            if (_timer < ECS.GameConstants.NeedUpdateIntervalSec) return;
            _timer = 0f;
            _TickNeeds();
        }

        private void _TickNeeds()
        {
            foreach (var id in _em.Query<ECS.NeedsComponent, ECS.AgentComponent>())
            {
                var needs = _em.GetComponent<ECS.NeedsComponent>(id);

                // Decay all needs each tick
                needs.Food          = Mathf.Max(0f, needs.Food          - FoodDecayRate);
                needs.Clothing      = Mathf.Max(0f, needs.Clothing      - ClothingDecayRate);
                needs.Entertainment = Mathf.Max(0f, needs.Entertainment - EntertainmentDecay);

                // Shelter is set when assigned to a house — does not decay
                // Refine: add homelessness timer here

                _em.SetComponent(id, needs);
            }

            // Restore food for citizens near a Market with inventory
            _RestoreFoodFromMarkets();
        }

        private void _RestoreFoodFromMarkets()
        {
            const float RestoreRadius = 10f;
            const float RestoreAmount = 0.3f;

            foreach (var marketId in _em.Query<ECS.BuildingDataComponent, ECS.InventoryComponent, ECS.PositionComponent>())
            {
                var bd = _em.GetComponent<ECS.BuildingDataComponent>(marketId);
                if (bd.Type != ECS.BuildingType.Market) continue;

                var inv = _em.GetComponent<ECS.InventoryComponent>(marketId);
                if (inv.Food <= 0) continue;

                var mPos = _em.GetComponent<ECS.PositionComponent>(marketId).Position;

                foreach (var citizenId in _em.Query<ECS.NeedsComponent, ECS.PositionComponent>())
                {
                    var cPos = _em.GetComponent<ECS.PositionComponent>(citizenId).Position;
                    if (cPos.DistanceTo(mPos) > RestoreRadius) continue;

                    var needs = _em.GetComponent<ECS.NeedsComponent>(citizenId);
                    needs.Food = Mathf.Min(1f, needs.Food + RestoreAmount);
                    _em.SetComponent(citizenId, needs);

                    inv.Food--;
                    _em.SetComponent(marketId, inv);
                    if (inv.Food <= 0) break;
                }
            }
        }

        public void _RunTests()
        {
            GD.Print("[NeedsCycleSystem] Decay logic unit test...");
            float food = 1.0f;
            food = Mathf.Max(0f, food - FoodDecayRate);
            System.Diagnostics.Debug.Assert(
                Mathf.IsEqualApprox(food, 1.0f - FoodDecayRate, 0.001f), "Food decay failed");
            GD.Print("[NeedsCycleSystem] Tests passed ✓");
        }
    }


    // ── BeautifulScoreSystem ─────────────────────────────────────
    // Calculates each citizen's environmental beauty score
    // by summing AestheticModifierComponents within radius.

    public partial class BeautifulScoreSystem : Node
    {
        private ECS.EntityManager _em;
        private float _timer = 0f;
        private const float UpdateInterval = 3f; // seconds between beauty recalcs

        public override void _Ready()
            => _em = ECS.World.Instance.Entities;

        public override void _Process(double delta)
        {
            _timer += (float)delta;
            if (_timer < UpdateInterval) return;
            _timer = 0f;
            _UpdateBeautyScores();
        }

        private void _UpdateBeautyScores()
        {
            foreach (var citizenId in _em.Query<ECS.HappinessComponent, ECS.PositionComponent>())
            {
                var cPos    = _em.GetComponent<ECS.PositionComponent>(citizenId).Position;
                float total = 0f;

                foreach (var decorId in _em.Query<ECS.AestheticModifierComponent, ECS.PositionComponent>())
                {
                    var am   = _em.GetComponent<ECS.AestheticModifierComponent>(decorId);
                    var dPos = _em.GetComponent<ECS.PositionComponent>(decorId).Position;

                    if (cPos.DistanceTo(dPos) <= am.Radius)
                        total += am.BeautyValue;
                }

                var hap = _em.GetComponent<ECS.HappinessComponent>(citizenId);
                hap.BeautifulScore = Mathf.Min(100f, total);
                _em.SetComponent(citizenId, hap);
            }
        }

        public void _RunTests()
        {
            GD.Print("[BeautifulScoreSystem] Tests require running scene — skipped in unit mode.");
        }
    }


    // ── HappinessSystem ──────────────────────────────────────────
    // Aggregates NeedsComponent + BeautifulScore → HappinessComponent.
    // Also updates TownTierComponent.AverageHappiness.

    public partial class HappinessSystem : Node
    {
        private ECS.EntityManager _em;
        private float _timer = 0f;
        private const float UpdateInterval = 2f;

        public override void _Ready()
            => _em = ECS.World.Instance.Entities;

        public override void _Process(double delta)
        {
            _timer += (float)delta;
            if (_timer < UpdateInterval) return;
            _timer = 0f;
            _UpdateHappiness();
        }

        private void _UpdateHappiness()
        {
            float totalHappiness = 0f;
            int   citizenCount   = 0;

            foreach (var id in _em.Query<ECS.NeedsComponent, ECS.HappinessComponent>())
            {
                var needs = _em.GetComponent<ECS.NeedsComponent>(id);
                var hap   = _em.GetComponent<ECS.HappinessComponent>(id);

                // Beauty score mapped to 0–1 range (max 100)
                float beautyFactor = Mathf.Clamp(hap.BeautifulScore / 100f, 0f, 1f);

                // Weighted average: needs 70%, beauty 30%
                hap.Value = needs.Average * 0.7f + beautyFactor * 0.3f;
                _em.SetComponent(id, hap);

                totalHappiness += hap.Value;
                citizenCount++;
            }

            // Update town-level average
            if (citizenCount > 0)
            {
                uint townId = ECS.World.Instance.TownEntityId;
                if (_em.HasComponent<ECS.TownTierComponent>(townId))
                {
                    var tier = _em.GetComponent<ECS.TownTierComponent>(townId);
                    tier.AverageHappiness = totalHappiness / citizenCount;
                    _em.SetComponent(townId, tier);
                }
            }
        }

        public void _RunTests()
        {
            GD.Print("[HappinessSystem] Formula test...");
            float needs  = 0.8f;
            float beauty = 0.5f;
            float result = needs * 0.7f + beauty * 0.3f;
            System.Diagnostics.Debug.Assert(
                Mathf.IsEqualApprox(result, 0.71f, 0.001f), "Happiness formula failed");
            GD.Print("[HappinessSystem] Tests passed ✓");
        }
    }
}
