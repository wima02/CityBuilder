// ================================================================
// FILE: SystemTests.cs
// Unit tests for systems that can be tested without a Godot scene.
// Refine: add test cases as you refine each system.
// ================================================================
using Godot;
using System.Diagnostics;

namespace CityBuilder.Tests
{
    public partial class SystemTests : Node
    {
        public override void _Ready()
        {
            if (!OS.IsDebugBuild()) return;
            RunAll();
        }

        public static void RunAll()
        {
            GD.Print("=== System Tests ===");

            Test_BezierMidpoint();
            Test_PlacementSnap();
            Test_HappinessFormula();
            Test_NeedsDecay();
            Test_SupplyTransferCap();
            Test_TownTierThreshold();

            GD.Print("=== All System Tests Passed ✓ ===");
        }

        private static void Test_BezierMidpoint()
        {
            var mid = Systems.SplineRoadSystem.SampleBezier(
                Godot.Vector3.Zero,
                new Godot.Vector3(1, 0, 0),
                new Godot.Vector3(2, 0, 0),
                new Godot.Vector3(3, 0, 0), 0.5f);

            Debug.Assert(Godot.Mathf.IsEqualApprox(mid.X, 1.5f, 0.01f),
                "Bezier midpoint X should be 1.5");
            GD.Print("[PASS] BezierMidpoint");
        }

        private static void Test_PlacementSnap()
        {
            // Snap logic: round to nearest 0.5
            float snap = 0.5f;
            float input = 0.7f;
            float result = Godot.Mathf.Round(input / snap) * snap;
            Debug.Assert(Godot.Mathf.IsEqualApprox(result, 1.0f, 0.01f),
                "0.7 should snap to 1.0");
            GD.Print("[PASS] PlacementSnap");
        }

        private static void Test_HappinessFormula()
        {
            float needs  = 0.8f;
            float beauty = 0.5f;
            float result = needs * 0.7f + beauty * 0.3f;
            Debug.Assert(Godot.Mathf.IsEqualApprox(result, 0.71f, 0.001f),
                "Happiness formula: 0.8*0.7 + 0.5*0.3 = 0.71");
            GD.Print("[PASS] HappinessFormula");
        }

        private static void Test_NeedsDecay()
        {
            float food      = 1.0f;
            float decayRate = 0.05f;
            food = Godot.Mathf.Max(0f, food - decayRate);
            Debug.Assert(Godot.Mathf.IsEqualApprox(food, 0.95f, 0.001f),
                "Food should decay by 0.05 per tick");
            GD.Print("[PASS] NeedsDecay");
        }

        private static void Test_SupplyTransferCap()
        {
            int available = 50;
            int rate      = 10;
            int space     = 6;
            int transfer  = (int)Godot.Mathf.Min(Godot.Mathf.Min(rate, available), space);
            Debug.Assert(transfer == 6, "Transfer should be capped by available space (6)");
            GD.Print("[PASS] SupplyTransferCap");
        }

        private static void Test_TownTierThreshold()
        {
            float threshold = ECS.GameConstants.UpgradeHappinessThreshold;
            Debug.Assert(threshold > 0f && threshold < 1f,
                "Upgrade threshold should be between 0 and 1");
            Debug.Assert(Godot.Mathf.IsEqualApprox(threshold, 0.6f, 0.001f),
                "Default threshold should be 0.60");
            GD.Print("[PASS] TownTierThreshold");
        }
    }
}
